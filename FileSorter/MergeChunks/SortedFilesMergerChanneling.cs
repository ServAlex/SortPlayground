using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using LargeFileSort.Configurations;
using Microsoft.Extensions.Options;

namespace LargeFileSort.FileSorter.MergeChunks;

public class SortedFilesMergerChanneling(
	IOptions<PathOptions> pathOptions, 
	IOptions<SortOptions> sortOptions, 
	FileProgressLogger logger)
{
	private readonly PathOptions _pathOptions = pathOptions.Value;
	private readonly SortOptions _sortOptions = sortOptions.Value;
	
	public long MergeSortedFiles()
	{
		var chunksDirectoryPath = Path.Combine(_pathOptions.FilesLocation, pathOptions.Value.ChunksDirectoryBaseName);
		var sortedFilePath = Path.Combine(_pathOptions.FilesLocation, pathOptions.Value.SortedFileName);
		
		var startTime = DateTime.Now;
		Console.WriteLine();
		Console.WriteLine($"Merging to final file {sortedFilePath}");
		Console.WriteLine();
		
		var files = new DirectoryInfo(chunksDirectoryPath).GetFiles();
		var chunksCount = files.Length;
		if (chunksCount == 0)
			throw new InvalidOperationException($"No chunk files found in '{chunksDirectoryPath}'. Nothing to merge.");
		
		var availableThreads = Environment.ProcessorCount;
		var intermediateMergeThreads =
			Math.Min(
				(int)Math.Floor(Math.Sqrt(chunksCount)), 
				availableThreads - 1);
		var filesPerThread = (int)Math.Ceiling((float)chunksCount / intermediateMergeThreads);
		//var intermediateChannelCapacity = 100;
		var batchSize = 100_000;
		const int empiricalConstant = 600;
		var intermediateChannelCapacity = (int)Math.Min(100, (long)_sortOptions.MemoryBudgetGb*1024*1024*1024/batchSize/intermediateMergeThreads/empiricalConstant);
		
		var intermediateChannels = Enumerable
			.Range(0, intermediateMergeThreads)
			.Select(_ => Channel.CreateBounded<MergeBatch>(new BoundedChannelOptions(intermediateChannelCapacity)
			{
				SingleWriter = true,
				SingleReader = true,
				FullMode = BoundedChannelFullMode.Wait
			})).ToArray();

		var intermediateDirectoryName = "ChunksIntermediate";
		if (Directory.Exists(intermediateDirectoryName))
		{
			Directory.Delete(intermediateDirectoryName, true);
		}
		Directory.CreateDirectory(intermediateDirectoryName);
		
		var tasks = new List<Task>();
		tasks.AddRange(
			Enumerable
				.Range(0, intermediateMergeThreads)
				.Select(q =>
					Task.Run(() =>
						MergeFiles(
							files.Skip(q * filesPerThread).Take(filesPerThread).ToArray(),
							intermediateChannels[q],
							_sortOptions.BufferSizeMb * 1024 * 1024,
							batchSize,
							q)
					)
				)
		);
		
		var finalMergeTask = Task.Run(() => FinalMerge(intermediateChannels, sortedFilePath, _sortOptions.BufferSizeMb * 1024 * 1024));
		tasks.Add(finalMergeTask);
		
		var loggerCancellationTokenSource = new CancellationTokenSource();
		// ReSharper disable once MethodSupportsCancellation
		Task.Run(() => logger.LogState(startTime, () =>
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

		//logger.LogCompletion();
		Console.WriteLine();
		
		return finalMergeTask.Result;
	}

	private void MergeFiles(
		FileInfo[] files, 
		Channel<MergeBatch> intermediateResultsChannel,
		int readerBufferSize, 
		int batchSize,
		int mergerIndex)
	{
		var sb = new StringBuilder();
		sb.AppendLine($"Merger {mergerIndex} merging {files.Length} files");
		foreach (var file in files)
		{
			sb.AppendLine($"Reading {file.Name}");
		}
		Console.WriteLine(sb.ToString());
		
		var readers = files.Select(f =>
			new StreamReader(
				f.FullName,
				Encoding.UTF8,
				true,
				new FileStreamOptions
				{
					BufferSize = readerBufferSize,
					Options = FileOptions.SequentialScan
				})
		).ToArray();
		
		var pq = new PriorityQueue<SimpleMergeItem, SimpleMergeKey>();
		var lineEndLength = Environment.NewLine.Length;
		
		// populate queue
		for (var i = 0; i < readers.Length; i++)
		{
			var line = readers[i].ReadLine();
			if (string.IsNullOrEmpty(line)) 
				continue;
			logger.BytesRead += line.Length + lineEndLength;
			
			var newItem = new SimpleMergeItem(line, i);
			pq.Enqueue(newItem, new SimpleMergeKey(newItem));
		}
		
		var batch = new MergeBatch(ArrayPool<SimpleMergeItem>.Shared.Rent(batchSize));
		
		// run until the queue is empty
		while (pq.TryDequeue(out var item, out _))
		{
			if (batch.Count == batchSize)
			{
				var sw = new SpinWait();
				while (!intermediateResultsChannel.Writer.TryWrite(batch))
				{
					sw.SpinOnce();
				}
				batch = new MergeBatch(ArrayPool<SimpleMergeItem>.Shared.Rent(batchSize));
			}
			
			batch.Add(new SimpleMergeItem(item, mergerIndex));

			var reader = readers[item.SourceIndex];
			var next = reader.ReadLine();
			
			if (string.IsNullOrEmpty(next)) 
				continue;
			
			logger.BytesRead += next.Length + lineEndLength;
			
			var newItem = new SimpleMergeItem(next, item.SourceIndex);
			pq.Enqueue(newItem, new SimpleMergeKey(newItem));
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

	private long FinalMerge(Channel<MergeBatch>[] intermediateResultsChannels, string fileName, int writerBufferSize)
	{
		var sb = new StringBuilder($"Stage 2 merges {intermediateResultsChannels.Length} sources");
		sb.AppendLine();
		Console.WriteLine(sb);
		
		using var writer = new StreamWriter(
			fileName,
			Encoding.UTF8, 
			new FileStreamOptions
			{
				BufferSize = writerBufferSize, 
				Mode = FileMode.Create, 
				Access = FileAccess.Write,
			});	
		
		var priorityQueue = new PriorityQueue<SimpleMergeItem, SimpleMergeKey>();
		var batches = new MergeBatch[intermediateResultsChannels.Length];
		var completed = new bool[intermediateResultsChannels.Length];
		
		// populate queue
		for (var i = 0; i < intermediateResultsChannels.Length; i++)
		{
			var spinWait = new SpinWait();
			MergeBatch? nullableBatch;
			while (!intermediateResultsChannels[i].Reader.TryRead(out nullableBatch))
			{
				if (intermediateResultsChannels[i].Reader.Completion.IsCompleted)
				{
					completed[i] = true;
					break;
				}
				spinWait.SpinOnce();
			}

			var batch = batches[i] = nullableBatch ??
			                         throw new InvalidOperationException($"Channel {i} completed without any data.");
			//var batch = batches[i] = intermediateResultsChannels[i].Reader.ReadAsync().GetAwaiter().GetResult();
			var item = batch.Items[batch.CurrentReadIndex++];
			priorityQueue.Enqueue(new SimpleMergeItem(item, i), new SimpleMergeKey(item));
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
				ArrayPool<SimpleMergeItem>.Shared.Return(batch.Items, clearArray: false);
				
				// load batch or complete
				var spinWait = new SpinWait();
				MergeBatch? nullableBatch;
				while (!intermediateResultsChannels[item.SourceIndex].Reader.TryRead(out nullableBatch))
				{
					if (intermediateResultsChannels[item.SourceIndex].Reader.Completion.IsCompleted)
					{
						completed[item.SourceIndex] = true;
						break;
					}
					spinWait.SpinOnce();
				}
				
				if (completed[item.SourceIndex])
				{
					continue;
				}

				batch = batches[item.SourceIndex] = nullableBatch ??
				                                    throw new InvalidOperationException(
					                                    $"Channel {item.SourceIndex} completed without any data.");
			}
			
			var next = batch.Items[batch.CurrentReadIndex++];
			priorityQueue.Enqueue(new SimpleMergeItem(next, item.SourceIndex), new SimpleMergeKey(next));
		}
		
		foreach (var b in batches)
		{
			Debug.Assert(b == null || b.CurrentReadIndex == b.Count);
		}

		writer.Flush();
		return writer.BaseStream.Position;
	}
}