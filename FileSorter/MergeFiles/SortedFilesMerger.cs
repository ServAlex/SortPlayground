using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Threading.Channels;

namespace FileGenerator.FileSorter.MergeFiles;

public class SortedFilesMerger
{
	public static long MergeSortedFiles(
		string directoryName, 
		string destinationFileName, 
		int readerBufferSize,
		int writerBufferSize)
	{
		using var writer = new StreamWriter(
			destinationFileName,
			Encoding.UTF8, 
			new FileStreamOptions
			{
				BufferSize = writerBufferSize, 
				Mode = FileMode.Create, 
				Access = FileAccess.Write
			});

		var sw = Stopwatch.StartNew();
		Console.WriteLine($"Merging to final file {destinationFileName}");
		
		var files = new DirectoryInfo(directoryName).GetFiles();
		var readers = files.Select(f =>
			new SortedFileReader(
				new StreamReader(
					//Path.Combine(directoryName, f.Name),
					f.FullName,
					Encoding.UTF8,
					true,
					new FileStreamOptions
					{
						BufferSize = readerBufferSize,
						Options = FileOptions.SequentialScan
					})
			)
		).ToList();
		
		var pq = new PriorityQueue<SortedFileReader, PriorityQueueKey>();
		foreach (var reader in readers)
		{
			pq.Enqueue(reader, new PriorityQueueKey(reader));
		}
		
		long totalLines = 0;

		//while (readers.Any(r => !r.EndOfFile))
		while (pq.TryDequeue(out var reader, out var priorityKey))
		{
			writer.WriteLine(reader.LineString);
			totalLines++;
			if (reader.ReadLine())
			{
				pq.Enqueue(reader, priorityKey);
			}
		}

		Console.WriteLine($"Total lines written to final file {totalLines}, time: {sw.ElapsedMilliseconds} ms");
		return writer.BaseStream.Length;
	}
	
	
	public static long MergeSortedFiles_Simple(
		string directoryName, 
		string destinationFileName, 
		int readerBufferSize,
		int writerBufferSize)
	{
		using var writer = new StreamWriter(
			destinationFileName,
			Encoding.UTF8, 
			new FileStreamOptions
			{
				BufferSize = writerBufferSize, 
				Mode = FileMode.Create, 
				Access = FileAccess.Write
			});

		var sw = Stopwatch.StartNew();
		Console.WriteLine($"Merging to final file {destinationFileName}");
		
		var files = new DirectoryInfo(directoryName).GetFiles();
		
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
			).ToList();
		
		long totalLines = 0;	
		var pq = new PriorityQueue<SimpleMergeItem, SimpleMergeKey>();
		
		// populate queue
		for (var i = 0; i < readers.Count; i++)
		{
			var line = readers[i].ReadLine();
			if (string.IsNullOrEmpty(line)) 
				continue;
			
			Parse(line, out var num, out var text);
			pq.Enqueue(new SimpleMergeItem(text, num, i), new SimpleMergeKey(text, num));
		}
		
		// run until the queue is empty
		while (pq.TryDequeue(out var item, out _))
		{
			writer.WriteLine($"{item.Number}.{item.Text}");
			totalLines++;

			var reader = readers[item.SourceIndex];
			var next = reader.ReadLine();
			
			if (string.IsNullOrEmpty(next)) 
				continue;
			
			Parse(next, out var num, out var text);
			pq.Enqueue(
				new SimpleMergeItem(text, num, item.SourceIndex),
				new SimpleMergeKey(text, num));
		}

		Console.WriteLine($"Total lines written to final file {totalLines}, time: {sw.ElapsedMilliseconds} ms");
		return writer.BaseStream.Length;
	}

	private static void Parse(string line, out int number, out string text)
	{
		var comma = line.IndexOf('.');
		number = int.Parse(line.AsSpan(0, comma));
		text = line[(comma + 1)..];
	}


	public static long MergeSortedFiles_Threaded(
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
		var intermediateMergeThreads = Math.Min(4, availableThreads - 2);
		var filesPerThread = (int)Math.Ceiling((float)chunksCount / intermediateMergeThreads);
		var intermediateChannelCapacity = 1000;
		var writeChannelCapacity = 10000;

		var intermediateChannels = Enumerable
			.Range(0, intermediateMergeThreads)
			.Select(_ => Channel.CreateBounded<SimpleMergeItem>(new BoundedChannelOptions(intermediateChannelCapacity)
			{
				SingleWriter = true,
				SingleReader = true,
				FullMode = BoundedChannelFullMode.Wait
			})).ToArray();
		var writeChannel = Channel.CreateBounded<SimpleMergeItem>(new BoundedChannelOptions(writeChannelCapacity)
		{
			SingleWriter = true,
			SingleReader = true,
			FullMode = BoundedChannelFullMode.Wait
		});

		var fileWritingTask = Task.Run(() => WriteToFile(writeChannel, destinationFileName, writerBufferSize));
		
		var tasks = new List<Task>()
		{
			Task.WhenAll(
				Enumerable
					.Range(0, intermediateMergeThreads)
					.Select(q =>
						MergeFiles(
							files.Skip(q * filesPerThread).Take(filesPerThread).ToArray(),
							intermediateChannels[q],
							readerBufferSize,
							q)
					).ToArray()
			),
			Task.Run(() => TerminalMerge(intermediateChannels, writeChannel)),
			fileWritingTask,
			
			Task.Run(async () =>
			{
				await Task.Delay(10000);

				var rsw = Stopwatch.StartNew();
				try
				{
					var reader = new StreamReader(
						"test.txt",
						Encoding.UTF8,
						true,
						new FileStreamOptions
						{
							BufferSize = readerBufferSize,
							Options = FileOptions.SequentialScan
						});
					
					char[] buffer = new char[1024 * 1024 * 1024];
					reader.ReadBlock(buffer, 0, buffer.Length);
				
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
					Console.WriteLine(e.InnerException?.Message);
					throw;
				}
				
				Console.WriteLine($"Total time: {rsw.ElapsedMilliseconds} ms");
			})
			
		};
		
		Task.WaitAll(tasks.ToArray());
		
		Console.WriteLine($"Total lines written to final file {fileWritingTask.Result}, time: {sw.ElapsedMilliseconds} ms");
		
		return fileWritingTask.Result;
	}

	private static async Task MergeFiles(FileInfo[] files, Channel<SimpleMergeItem> intermediateResultsChannel, int readerBufferSize, int mergerIndex)
	{
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
		
		// run until the queue is empty
		while (pq.TryDequeue(out var item, out _))
		{
			intermediateResultsChannel.Writer.TryWrite(new SimpleMergeItem(item.Text, item.Number, mergerIndex));

			var reader = readers[item.SourceIndex];
			var next = reader.ReadLine();
			
			if (string.IsNullOrEmpty(next)) 
				continue;
			
			Parse(next, out var num, out var text);
			pq.Enqueue(
				new SimpleMergeItem(text, num, item.SourceIndex),
				new SimpleMergeKey(text, num));
		}
		
		intermediateResultsChannel.Writer.Complete();
	}
	
	private static async Task TerminalMerge(Channel<SimpleMergeItem>[] intermediateChannels, Channel<SimpleMergeItem> fileWriteChannel)
	{
		var pq = new PriorityQueue<SimpleMergeItem, SimpleMergeKey>();
		
		// populate queue
		for (var i = 0; i < intermediateChannels.Length; i++)
		{
			if(!await intermediateChannels[i].Reader.WaitToReadAsync())
				continue;
			
			var item = await intermediateChannels[i].Reader.ReadAsync();
			pq.Enqueue(
				new SimpleMergeItem(item.Text, item.Number, i), 
				new SimpleMergeKey(item.Text, item.Number));
		}
		
		// run until the queue is empty
		while (pq.TryDequeue(out var item, out _))
		{
			await fileWriteChannel.Writer.WriteAsync(item);

			if(!await intermediateChannels[item.SourceIndex].Reader.WaitToReadAsync())
				continue;
			var next = await intermediateChannels[item.SourceIndex].Reader.ReadAsync();

			pq.Enqueue(
				new SimpleMergeItem(next.Text, next.Number, item.SourceIndex),
				new SimpleMergeKey(next.Text, next.Number));
		}
		
		fileWriteChannel.Writer.Complete();
	}

	private static async Task<long> WriteToFile(Channel<SimpleMergeItem> fileWriteChannel, string destinationFileName, int writerBufferSize)
	{
		await using var writer = new StreamWriter(
			destinationFileName,
			Encoding.UTF8, 
			new FileStreamOptions
			{
				BufferSize = writerBufferSize, 
				Mode = FileMode.Create, 
				Access = FileAccess.Write
			});
		
		long totalLines = 0;
		await foreach (var item in fileWriteChannel.Reader.ReadAllAsync())
		{
			writer.WriteLine($"{item.Number}.{item.Text}");
			totalLines++;
		}
		return totalLines;
	}
}