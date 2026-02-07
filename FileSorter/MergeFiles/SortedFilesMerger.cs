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
}