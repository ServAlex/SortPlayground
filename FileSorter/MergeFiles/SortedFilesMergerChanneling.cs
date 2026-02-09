using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Threading.Channels;

namespace FileGenerator.FileSorter.MergeFiles;

public class SortedFilesMergerChanneling
{
	private static void Parse(string line, out int number, out string text)
	{
		var comma = line.IndexOf('.');
		number = int.Parse(line.AsSpan(0, comma));
		text = line[(comma + 1)..];
	}

	public static long MergeSortedFiles(
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
		var batchSize = 100000;
		
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
		
		var finalMergeTask = Task.Run(() => FinalMerge(intermediateChannels, writerBufferSize));
		
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

		Task.WaitAll(tasks);
		
		Console.WriteLine($"Total lines written to final file {finalMergeTask.Result}, time: {sw.ElapsedMilliseconds} ms");
		
		return 0;
	}

	private static void MergeFiles(
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
		
		// populate queue
		for (var i = 0; i < readers.Length; i++)
		{
			var line = readers[i].ReadLine();
			if (string.IsNullOrEmpty(line)) 
				continue;
			
			Parse(line, out var num, out var text);
			pq.Enqueue(new SimpleMergeItem(text, num, i), new SimpleMergeKey(text, num));
		}
		
		var batch = new MergeBatch(new SimpleMergeItem[batchSize]);
		
		// run until the queue is empty
		while (pq.TryDequeue(out var item, out _))
		{
			if (batch.Count == batchSize)
			{
				while (!intermediateResultsChannel.Writer.TryWrite(batch))
				{
					Thread.Yield(); // or SpinWait
				}
				batch = new MergeBatch(new SimpleMergeItem[batchSize]);
			}
			else
			{
				batch.Add(new SimpleMergeItem(item.Text, item.Number, mergerIndex));
			}

			var reader = readers[item.SourceIndex];
			var next = reader.ReadLine();
			
			if (string.IsNullOrEmpty(next)) 
				continue;
			
			Parse(next, out var num, out var text);
			pq.Enqueue(
				new SimpleMergeItem(text, num, item.SourceIndex),
				new SimpleMergeKey(text, num));
		}

		if (batch.Count > 0)
		{
			while (!intermediateResultsChannel.Writer.TryWrite(batch))
			{
				Thread.Yield(); // or SpinWait
			}
		}
			
		intermediateResultsChannel.Writer.Complete();
	}

	private static long FinalMerge(Channel<MergeBatch>[] intermediateResultsChannels, int writerBufferSize)
	{
		Console.WriteLine($"Final merge merging {intermediateResultsChannels.Length} sources");
		
		using var writer = new StreamWriter(
			"sorted.txt",
			Encoding.UTF8, 
			new FileStreamOptions
			{
				BufferSize = writerBufferSize, 
				Mode = FileMode.Create, 
				Access = FileAccess.Write
			});	
		
		var pq = new PriorityQueue<SimpleMergeItem, SimpleMergeKey>();
		long totalLines = 0;
		var batches = new MergeBatch[intermediateResultsChannels.Length];
		var completed = new bool[intermediateResultsChannels.Length];
		
		// populate queue
		for (var i = 0; i < intermediateResultsChannels.Length; i++)
		{
			while (!intermediateResultsChannels[i].Reader.TryRead(out batches[i]))
			{
				if (intermediateResultsChannels[i].Reader.Completion.IsCompleted)
				{
					completed[i] = true;
					break;
				}
				Thread.Yield();
			}
			
			//var batch = batches[i] = intermediateResultsChannels[i].Reader.ReadAsync().GetAwaiter().GetResult();
			var item = batches[i].Items[batches[i].ReaderIndex++];
			pq.Enqueue(new SimpleMergeItem(item.Text, item.Number, i), new SimpleMergeKey(item.Text, item.Number));
		}
		
		// run until the queue is empty
		while (pq.TryDequeue(out var item, out _))
		{
			totalLines++;
			writer.WriteLine($"{item.Number}.{item.Text}");
			var batch = batches[item.SourceIndex];

			if (batch.ReaderIndex == batch.Count)
			{
				// load batch or complete
				while (!intermediateResultsChannels[item.SourceIndex].Reader.TryRead(out batches[item.SourceIndex]))
				{
					if (intermediateResultsChannels[item.SourceIndex].Reader.Completion.IsCompleted)
					{
						completed[item.SourceIndex] = true;
						break;
					}
					Thread.Yield();
				}
				
				if (completed[item.SourceIndex])
				{
					continue;
				}
			}
			
			batch = batches[item.SourceIndex];
			
			var next = batch.Items[batch.ReaderIndex++];
			pq.Enqueue(new SimpleMergeItem(next.Text, next.Number, item.SourceIndex), new SimpleMergeKey(next.Text, next.Number));
		}

		return totalLines;
	}
}