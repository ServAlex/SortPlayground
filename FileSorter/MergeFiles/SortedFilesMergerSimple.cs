using System.Diagnostics;
using System.Text;

namespace FileGenerator.FileSorter.MergeFiles;

public class SortedFilesMergerSimple
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

			var newItem = new SimpleMergeItem(line, i);
			pq.Enqueue(newItem, new SimpleMergeKey(newItem));
		}
		
		// run until the queue is empty
		while (pq.TryDequeue(out var item, out _))
		{
			writer.WriteLine(item.Line);
			totalLines++;

			var reader = readers[item.SourceIndex];
			var next = reader.ReadLine();
			
			if (string.IsNullOrEmpty(next)) 
				continue;
			
			var newItem = new SimpleMergeItem(next, item.SourceIndex);
			pq.Enqueue(newItem, new SimpleMergeKey(newItem));
		}

		Console.WriteLine($"Total lines written to final file {totalLines}, time: {sw.ElapsedMilliseconds} ms");
		return writer.BaseStream.Length;
	}
}