using System.Diagnostics;
using System.Text;

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
		
		for (var i = 0; i < readers.Count; i++)
		{
			var line = readers[i].ReadLine();
			if (string.IsNullOrEmpty(line)) continue;
			
			Parse(line, out var num, out var text);
			pq.Enqueue(new SimpleMergeItem(text, num, i), new SimpleMergeKey(text, num));
		}
		
		while (pq.TryDequeue(out var item, out _))
		{
			writer.WriteLine($"{item.Number}.{item.Text}");
			totalLines++;

			var reader = readers[item.FileIndex];
			var next = reader.ReadLine();
			
			if (string.IsNullOrEmpty(next)) continue;
			
			Parse(next, out var num, out var text);
			pq.Enqueue(
				new SimpleMergeItem(text, num, item.FileIndex),
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
}