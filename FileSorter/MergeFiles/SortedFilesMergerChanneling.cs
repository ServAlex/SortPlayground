using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;

namespace FileGenerator.FileSorter.MergeFiles;

public class SortedFilesMergerChanneling
{
	private FileProgressLogger _logger;

	public SortedFilesMergerChanneling(FileProgressLogger logger)
	{
		_logger = logger;
	}

	public long MergeSortedFiles(
		string directoryName,
		string destinationFileName,
		int readerBufferSize,
		int writerBufferSize)
	{
		var startTime = DateTime.Now;
		Console.WriteLine($"Merging to final file {destinationFileName}");
		
		var files = new DirectoryInfo(directoryName).GetFiles();
		var chunksCount = files.Length;
		var availableThreads = Environment.ProcessorCount;
		var intermediateMergeThreads =
			Math.Min(
				(int)Math.Floor(Math.Sqrt(chunksCount)), 
				availableThreads - 1);
		var filesPerThread = (int)Math.Ceiling((float)chunksCount / intermediateMergeThreads);
		var intermediateChannelCapacity = 100;
		var batchSize = 100_000;
		
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
							readerBufferSize,
							batchSize,
							q)
					)
				)
			);
		
		var finalMergeTask = Task.Run(() => FinalMerge(intermediateChannels, destinationFileName, writerBufferSize));
		tasks.Add(finalMergeTask);
		
		var loggerCancellationTokenSource = new CancellationTokenSource();
		// ReSharper disable once MethodSupportsCancellation
		Task.Run(() => _logger.LogState(startTime, () =>
		{
			var stringBuilder = new StringBuilder("   Queues states:");
			intermediateChannels
				.Select(c => $"{c.Reader.Count,4:D}/{intermediateChannelCapacity}")
				.Aggregate(stringBuilder, (sb, str) => sb.Append(' ').Append(str));
			
			return stringBuilder.ToString();
		}, loggerCancellationTokenSource.Token));

		// ReSharper disable once MethodSupportsCancellation
		Task.WaitAll(tasks);
		loggerCancellationTokenSource.Cancel();

		_logger.LogCompletion();
		
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
			_logger.BytesRead += line.Length + lineEndLength;
			
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
			
			_logger.BytesRead += next.Length + lineEndLength;
			
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
		Console.WriteLine($"Final merge merging {intermediateResultsChannels.Length} sources");
		
		using var writer = new StreamWriter(
			fileName,
			Encoding.UTF8, 
			new FileStreamOptions
			{
				BufferSize = writerBufferSize, 
				Mode = FileMode.Create, 
				Access = FileAccess.Write,
				PreallocationSize = 20L * 1024 * 1024 * 1024
			});	
		
		var pq = new PriorityQueue<SimpleMergeItem, SimpleMergeKey>();
		var batches = new MergeBatch[intermediateResultsChannels.Length];
		var completed = new bool[intermediateResultsChannels.Length];
		
		// populate queue
		for (var i = 0; i < intermediateResultsChannels.Length; i++)
		{
			var sw = new SpinWait();
			while (!intermediateResultsChannels[i].Reader.TryRead(out batches[i]))
			{
				if (intermediateResultsChannels[i].Reader.Completion.IsCompleted)
				{
					completed[i] = true;
					break;
				}
				sw.SpinOnce();
			}
			var batch = batches[i];
			//var batch = batches[i] = intermediateResultsChannels[i].Reader.ReadAsync().GetAwaiter().GetResult();
			var item = batch.Items[batch.ReaderIndex++];
			pq.Enqueue(new SimpleMergeItem(item, i), new SimpleMergeKey(item));
		}
		
		// run until the queue is empty
		while (pq.TryDequeue(out var item, out _))
		{
			writer.WriteLine(item.Line);
			
			_logger.LinesWritten++;
			_logger.BytesWritten = writer.BaseStream.Position;
			
			var batch = batches[item.SourceIndex];

			if (batch.ReaderIndex == batch.Count)
			{
				// no more items in batch, return and load next
				ArrayPool<SimpleMergeItem>.Shared.Return(batch.Items, clearArray: false);
				
				// load batch or complete
				var sw = new SpinWait();
				while (!intermediateResultsChannels[item.SourceIndex].Reader.TryRead(out batches[item.SourceIndex]))
				{
					if (intermediateResultsChannels[item.SourceIndex].Reader.Completion.IsCompleted)
					{
						completed[item.SourceIndex] = true;
						break;
					}
					sw.SpinOnce();
				}
				
				if (completed[item.SourceIndex])
				{
					continue;
				}
			}
			
			batch = batches[item.SourceIndex];
			
			var next = batch.Items[batch.ReaderIndex++];
			pq.Enqueue(new SimpleMergeItem(next, item.SourceIndex), new SimpleMergeKey(next));
		}
		
		foreach (var b in batches)
		{
			Debug.Assert(b == null || b.ReaderIndex == b.Count);
		}

		return _logger.BytesWritten;
	}
}