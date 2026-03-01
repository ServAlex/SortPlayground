using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using LargeFileSort.Common;
using LargeFileSort.Configurations;
using LargeFileSort.Infrastructure;
using LargeFileSort.Logging;
using Microsoft.Extensions.Options;

namespace LargeFileSort.FileSorting.MergeChunks;

public class SortedFilesMerger(
	IOptions<GeneralOptions> generalOptions, 
	IOptions<SortOptions> sortOptions, 
	LiveProgressLogger logger,
	IFileSystem fileSystem)
{
	private readonly GeneralOptions _generalOptions = generalOptions.Value;
	private readonly SortOptions _sortOptions = sortOptions.Value;

	public long MergeSortedFiles()
	{
		var chunksDirectoryPath = Path.Combine(_generalOptions.FilesLocation, _generalOptions.ChunksDirectoryBaseName);
		var sortedFilePath = Path.Combine(_generalOptions.FilesLocation, _generalOptions.SortedFileName);
		
		Console.WriteLine();
		Console.WriteLine($"SORT STEP 2: merging chunks to final file {sortedFilePath}: " +
		                  $"several Stage 1 mergers merge files to batches in parallel, " +
		                  $"feed batches to a single Stage 2 merger which writes to final file");
		Console.WriteLine();

		var files = ValidatedChunkFiles(chunksDirectoryPath);
		CheckIfEnoughSpace(files, sortedFilePath);
		
		var sw = Stopwatch.StartNew();
		var chunksCount = files.Length;
		
		var intermediateMergeThreads =
			Math.Min(
				(int)Math.Floor(Math.Sqrt(chunksCount)), 
				Environment.ProcessorCount - 1);
		if(chunksCount is 2 or 3) 
		 	intermediateMergeThreads = 2;
		
		const int batchSize = 100_000;
		const int empiricalConstant = 600;
		var intermediateChannelCapacity = (int)Math.Min(
			100, 
			_generalOptions.MemoryBudget/batchSize/intermediateMergeThreads/empiricalConstant);
		
		var intermediateChannels = Enumerable
			.Range(0, intermediateMergeThreads)
			.Select(_ => Channel.CreateBounded<MergeBatch>(new BoundedChannelOptions(intermediateChannelCapacity)
			{
				SingleWriter = true,
				SingleReader = true,
				FullMode = BoundedChannelFullMode.Wait
			})).ToArray();

		var tasks = new List<Task>();
		tasks.AddRange(
			Enumerable
				.Range(0, intermediateMergeThreads)
				.Select(q =>
					Task.Run(() =>
						MergeFiles(
							files.Where((_, i) => i % intermediateMergeThreads == q).ToArray(),
							intermediateChannels[q],
							_sortOptions.BufferSizeMb * 1024 * 1024,
							batchSize,
							q)
					)
				)
		);
		
		var finalMergeTask = Task.Run(() => FinalMerge(
			intermediateChannels, 
			sortedFilePath, 
			_sortOptions.BufferSizeMb * 1024 * 1024));
		tasks.Add(finalMergeTask);
		
		var loggerCancellationTokenSource = new CancellationTokenSource();
		// ReSharper disable once MethodSupportsCancellation
		Task.Run(() => logger.LogState(DateTime.Now, () =>
		{
			var stringBuilder = new StringBuilder("   Stage 1 output Qs:");
			intermediateChannels
				.Select(c => $"{c.Reader.Count,4:D}/{intermediateChannelCapacity}")
				.Aggregate(stringBuilder, (sb, str) => sb.Append(' ').Append(str));
			
			return stringBuilder.ToString();
		}, loggerCancellationTokenSource.Token));

		// ReSharper disable once MethodSupportsCancellation
		Task.WaitAll(tasks);
		loggerCancellationTokenSource.Cancel();

		Console.WriteLine();
		Console.WriteLine($"Merged in {sw.ElapsedMilliseconds/1000.0:F1} s");
		Console.WriteLine();
		
		return finalMergeTask.Result;
	}

	private FileInfo[] ValidatedChunkFiles(string chunksDirectoryPath)
	{
		if(!fileSystem.DirectoryExists(chunksDirectoryPath))
		{
			throw new FileNotFoundException($"Chunks directory '{chunksDirectoryPath}' does not exist. " +
			                                $"Nothing to merge.");
		}

		var files = fileSystem.GetFiles(chunksDirectoryPath);
		if (files.Length == 0)
		{
			throw new FileNotFoundException($"No chunk files found in '{chunksDirectoryPath}'. Nothing to merge.");
		}

		return files;
	}

	private void CheckIfEnoughSpace(FileInfo[] files, string path)
	{
		var totalSize = files.Sum(f => f.Length);

		if (!fileSystem.HasEnoughFreeSpace(path, totalSize))
		{
			throw new InsufficientFreeDiskException($"Not enough free space on disk to merge {files.Length} " +
			                                        $"chunk files into final file {path}");
		}
	}

	private async Task MergeFiles(
		FileInfo[] files, 
		Channel<MergeBatch> intermediateResultsChannel,
		int readerBufferSize, 
		int batchSize,
		int mergerIndex)
	{
		var sb = new StringBuilder();
		sb.AppendLine($"Stage 1 merger #{mergerIndex} is merging {files.Length} files:");
		foreach (var file in files)
		{
			sb.AppendLine($"Reading {file.Name}");
		}
		Console.WriteLine(sb.ToString());
		
		var readers = files
			.Select(f => fileSystem.GetFileReader(f.FullName, readerBufferSize))
			.ToArray();
		
		var pq = new PriorityQueue<MergeItem, MergeKey>();
		var lineEndLength = Environment.NewLine.Length;
		
		// populate queue
		for (var i = 0; i < readers.Length; i++)
		{
			var line = readers[i].ReadLine();
			if (string.IsNullOrEmpty(line)) 
				continue;
			logger.BytesRead += line.Length + lineEndLength;
			
			var newItem = new MergeItem(line, i);
			pq.Enqueue(newItem, new MergeKey(newItem));
		}
		
		var batch = new MergeBatch(ArrayPool<MergeItem>.Shared.Rent(batchSize));
		
		// run until the queue is empty
		while (pq.TryDequeue(out var item, out _))
		{
			if (batch.Count == batchSize)
			{
				await intermediateResultsChannel.Writer.WriteAsync(batch);
				batch = new MergeBatch(ArrayPool<MergeItem>.Shared.Rent(batchSize));
			}
			
			batch.Add(new MergeItem(item, mergerIndex));

			var reader = readers[item.SourceIndex];
			var nextLine = reader.ReadLine();
			
			if (string.IsNullOrEmpty(nextLine)) 
				continue;
			
			logger.BytesRead += nextLine.Length + lineEndLength;
			
			var newItem = new MergeItem(nextLine, item.SourceIndex);
			pq.Enqueue(newItem, new MergeKey(newItem));
		}

		if (batch.Count > 0)
		{
			var sw = new SpinWait();
			while (!intermediateResultsChannel.Writer.TryWrite(batch))
			{
				sw.SpinOnce();
			}
		}
			
		intermediateResultsChannel.Writer.Complete();
	}

	private async Task<long> FinalMerge(
		Channel<MergeBatch>[] intermediateResultsChannels, 
		string fileName, 
		int writerBufferSize)
	{
		var sb = new StringBuilder($"Stage 2 merges {intermediateResultsChannels.Length} sources");
		sb.AppendLine();
		Console.WriteLine(sb);

		await using var writer = fileSystem.GetFileWriter(fileName, writerBufferSize);
		
		var priorityQueue = new PriorityQueue<MergeItem, MergeKey>();
		var batches = new MergeBatch[intermediateResultsChannels.Length];
		
		// populate queue
		for (var i = 0; i < intermediateResultsChannels.Length; i++)
		{
			if (intermediateResultsChannels[i].Reader.Completion.IsCompleted)
			{
				continue;
			}

			var batch = batches[i] = await intermediateResultsChannels[i].Reader.ReadAsync();
			//var batch = batches[i] = intermediateResultsChannels[i].Reader.ReadAsync().GetAwaiter().GetResult();
			var item = batch.Items[batch.CurrentReadIndex++];
			priorityQueue.Enqueue(new MergeItem(item, i), new MergeKey(item));
		}
		
		// run until the queue is empty
		while (priorityQueue.TryDequeue(out var item, out _))
		{
			writer.WriteLine(item.Line);
			
			logger.LinesWritten++;
			logger.BytesWritten = writer.BaseStream.Position;
			
			var batch = batches[item.SourceIndex];

			if (batch.CurrentReadIndex == batch.Count)
			{
				// no more items in batch, return and load next
				ArrayPool<MergeItem>.Shared.Return(batch.Items, clearArray: false);

				if (intermediateResultsChannels[item.SourceIndex].Reader.Completion.IsCompleted)
				{
					continue;
				}

				batch = 
					batches[item.SourceIndex] = 
						await intermediateResultsChannels[item.SourceIndex].Reader.ReadAsync();
			}
			
			var next = batch.Items[batch.CurrentReadIndex++];
			priorityQueue.Enqueue(new MergeItem(next, item.SourceIndex), new MergeKey(next));
		}
		
		foreach (var b in batches)
		{
			Debug.Assert(b == null || b.CurrentReadIndex == b.Count);
		}

		writer.Flush();
		return writer.BaseStream.Position;
	}
}