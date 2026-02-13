using System.Text;

namespace FileGenerator.FileSorter.MergeChunks;

public class SortedFilesMergerSimple
{
	private FileProgressLogger _logger;

	public SortedFilesMergerSimple(FileProgressLogger logger)
	{
		_logger = logger;
	}

	public long MergeSortedFiles(
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

		var startTime = DateTime.Now;
		Console.WriteLine($"Merging to final file {destinationFileName}");
		Console.WriteLine();
		
		var loggerCancellationTokenSource = new CancellationTokenSource();
		// ReSharper disable once MethodSupportsCancellation
		Task.Run(() => _logger.LogState(startTime, null, loggerCancellationTokenSource.Token));
		
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
		
		var pq = new PriorityQueue<SimpleMergeItem, SimpleMergeKey>();
		
		// populate queue
		for (var i = 0; i < readers.Count; i++)
		{
			var line = readers[i].ReadLine();
			if (string.IsNullOrEmpty(line)) 
				continue;
			_logger.BytesRead += line.Length;

			var newItem = new SimpleMergeItem(line, i);
			pq.Enqueue(newItem, new SimpleMergeKey(newItem));
		}
		
		// run until the queue is empty
		while (pq.TryDequeue(out var item, out _))
		{
			writer.WriteLine(item.Line);
			_logger.LinesWritten++;
			_logger.BytesWritten = writer.BaseStream.Position;

			var reader = readers[item.SourceIndex];
			var next = reader.ReadLine();
			
			if (string.IsNullOrEmpty(next)) 
				continue;
			_logger.BytesRead += next.Length;
			
			var newItem = new SimpleMergeItem(next, item.SourceIndex);
			pq.Enqueue(newItem, new SimpleMergeKey(newItem));
		}
		
		loggerCancellationTokenSource.Cancel();

		_logger.LogCompletion();
		return writer.BaseStream.Length;
	}
}