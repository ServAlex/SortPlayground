using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;

namespace FileGenerator.FileSorter.MergeFiles;

public class SortedFilesMergerChanneling
{
	private long _linesWritten;
	private long _bytesWritten;
	private long _bytesRead;

	public long MergeSortedFiles(
		string directoryName,
		string destinationFileName,
		int readerBufferSize,
		int writerBufferSize)
	{
		var sw = Stopwatch.StartNew();
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
		
		var finalMergeTask = Task.Run(() => FinalMerge(intermediateChannels, destinationFileName, writerBufferSize));
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
		tasks.Add(finalMergeTask);
		
		var cancellationTokenSource = new CancellationTokenSource();
		Task.Run(() => LogStage(intermediateChannels, intermediateChannelCapacity, sw, cancellationTokenSource.Token));

		// ReSharper disable once MethodSupportsCancellation
		Task.WaitAll(tasks);
		cancellationTokenSource.Cancel();
		
		Console.WriteLine($"Total lines written to final file {finalMergeTask.Result}, time: {sw.ElapsedMilliseconds} ms");
		
		return finalMergeTask.Result;
	}

	private void LogStage(Channel<MergeBatch>[] intermediateResultsChannels, int capacity, Stopwatch sw, CancellationToken cancellationToken)
	{
		Thread.Sleep(1000);
		var lastBytesWritten = 0L;
		var lastBytesRead = 0L;
		var lastUpdateTime = sw.ElapsedMilliseconds;
		var capacityPadding = capacity.ToString().Length+1;
		
		const int lines = 5;
		var startLine = Console.CursorTop;
		for (var i = 0; i < lines; i++)
			Console.WriteLine();
		
		while (!cancellationToken.IsCancellationRequested)
		{
			var sb = new StringBuilder("\rQueues state:");
			foreach (var intermediateResultsChannel in intermediateResultsChannels)
			{
				sb.Append($"{intermediateResultsChannel.Reader.Count}".PadLeft(capacityPadding)).Append($"/{capacity}");
			}
			Console.Write($"\x1b[{lines}A");
			StringBuilderWriteAndReset(sb);
			//sb.Append($"   Lines written: {_linesWritten:N0}");
			sb.Append($"   Written:    {_bytesWritten/1024/1024:N0} MB");
			StringBuilderWriteAndReset(sb);
			sb.Append($"   R/W speed: {(double)(_bytesRead-lastBytesRead)/(sw.ElapsedMilliseconds - lastUpdateTime)*1000/1024/1024,5:N1} MB/s");
			sb.Append($" /{(double)(_bytesWritten-lastBytesWritten)/(sw.ElapsedMilliseconds - lastUpdateTime)*1000/1024/1024,5:N1} MB/s");
			lastUpdateTime = sw.ElapsedMilliseconds;
			lastBytesWritten = _bytesWritten;
			lastBytesRead = _bytesRead;
			StringBuilderWriteAndReset(sb);
			sb.Append($"   Avg R/W:   {(double)_bytesWritten/sw.ElapsedMilliseconds*1000/1024/1024,5:N1} MB/s");
			sb.Append($" /{(double)_bytesRead/sw.ElapsedMilliseconds*1000/1024/1024,5:N1} MB/s");
			StringBuilderWriteAndReset(sb);
			sb.Append($"   Time:       {(double)sw.ElapsedMilliseconds/1000:F1} ms");
			StringBuilderWriteAndReset(sb);
			
			Console.SetCursorPosition(0, startLine);
			//Console.Write(sb.ToString().PadRight(Console.WindowWidth - 1));
			Thread.Sleep(200);
		}
		Console.WriteLine();
	}

	private static void StringBuilderWriteAndReset(StringBuilder sb)
	{
		Console.WriteLine(sb);
		sb.Clear();
		sb.Append("\e[2K");
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
			_bytesRead += line.Length + lineEndLength;
			
			var newItem = new SimpleMergeItem(line, i);
			pq.Enqueue(newItem, new SimpleMergeKey(newItem));
		}
		
		
		var batch = new MergeBatch(ArrayPool<SimpleMergeItem>.Shared.Rent(batchSize));
		//var batch = new MergeBatch(new SimpleMergeItem[batchSize]);
		
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
				//batch = new MergeBatch(new SimpleMergeItem[batchSize]);
			}
			else
			{
				batch.Add(new SimpleMergeItem(item, mergerIndex));
			}

			var reader = readers[item.SourceIndex];
			var next = reader.ReadLine();
			
			if (string.IsNullOrEmpty(next)) 
				continue;
			
			_bytesRead += next.Length + lineEndLength;
			
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
			
			_linesWritten++;
			_bytesWritten = writer.BaseStream.Position;
			
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

		return _linesWritten;
	}
}