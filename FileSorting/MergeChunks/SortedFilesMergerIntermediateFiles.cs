using System.Text;

namespace LargeFileSort.FileSorting.MergeChunks;

public class SortedFilesMergerIntermediateFiles
{
	private FileProgressLogger _logger;

	public SortedFilesMergerIntermediateFiles(FileProgressLogger logger)
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
				availableThreads - 2);
		var filesPerThread = (int)Math.Ceiling((float)chunksCount / intermediateMergeThreads);

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
							intermediateDirectoryName,
							"intermediate_chunk",
							readerBufferSize,
							writerBufferSize,
							q)
					)
				)
			);
		
		var loggerCancellationTokenSource = new CancellationTokenSource();
		// ReSharper disable once MethodSupportsCancellation
		Task.Run(() => _logger.LogState(startTime, null, loggerCancellationTokenSource.Token));

		var intermediateMergesTime = DateTime.Now.Subtract(startTime).TotalMilliseconds;
		// ReSharper disable once MethodSupportsCancellation
		Task.WaitAll(tasks);
		
		var bytesWritten = MergeFiles(
			new DirectoryInfo(intermediateDirectoryName).GetFiles(),
			"",
			"sorted",
			readerBufferSize,
			writerBufferSize,
			1
		);
		
		loggerCancellationTokenSource.Cancel();
		
		Console.WriteLine($"Intermediate merges time: {intermediateMergesTime} ms");
		_logger.LogCompletion();
		//Console.WriteLine($"Total time: {Stopwatch.ElapsedMilliseconds} ms");
		//Console.WriteLine($"Total lines written to final file {fileWritingTask.Result}, time: {sw.ElapsedMilliseconds} ms");
		
		return bytesWritten;
	}

	private long MergeFiles(
		FileInfo[] files, 
		string destinationDirectoryName, 
		string filePrefix,
		int readerBufferSize, 
		int writerBufferSize, 
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
		
		using var writer = new StreamWriter(
			Path.Combine(destinationDirectoryName, $"{filePrefix}_{mergerIndex}.txt"),
			Encoding.UTF8, 
			new FileStreamOptions
			{
				BufferSize = writerBufferSize, 
				Mode = FileMode.Create, 
				Access = FileAccess.Write
			});
		
		var pq = new PriorityQueue<MergeItem, MergeKey>();
		
		// populate queue
		for (var i = 0; i < readers.Length; i++)
		{
			var line = readers[i].ReadLine();
			if (string.IsNullOrEmpty(line)) 
				continue;
			
			_logger.BytesRead += line.Length;
			
			var newItem = new MergeItem(line, i);
			pq.Enqueue(newItem, new MergeKey(newItem));
		}
		
		// run until the queue is empty
		while (pq.TryDequeue(out var item, out _))
		{
			writer.WriteLine(item.Line);
			// no interlocking for performance reasons
			_logger.LinesWritten++;
			_logger.BytesWritten += item.Line.Length;

			var reader = readers[item.SourceIndex];
			var next = reader.ReadLine();
			
			if (string.IsNullOrEmpty(next)) 
				continue;
			_logger.BytesRead += next.Length;
			
			var newItem = new MergeItem(next, item.SourceIndex);
			pq.Enqueue(newItem, new MergeKey(newItem));
		}
		
		return writer.BaseStream.Length;
	}
}