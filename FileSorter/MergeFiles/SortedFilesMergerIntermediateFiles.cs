using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Threading.Channels;

namespace FileGenerator.FileSorter.MergeFiles;

public class SortedFilesMergerIntermediateFiles
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

		Console.WriteLine($"Intermediate merges time: {sw.ElapsedMilliseconds} ms");
		Task.WaitAll(tasks);
		
		MergeFiles(
			new DirectoryInfo(intermediateDirectoryName).GetFiles(),
			"",
			"sorted",
			readerBufferSize,
			writerBufferSize,
			1
		);
		
		Console.WriteLine($"Total time: {sw.ElapsedMilliseconds} ms");
		//Console.WriteLine($"Total lines written to final file {fileWritingTask.Result}, time: {sw.ElapsedMilliseconds} ms");
		
		return 0;
	}

	private static void MergeFiles(
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
			writer.WriteLine($"{item.Number}.{item.Text}");

			var reader = readers[item.SourceIndex];
			var next = reader.ReadLine();
			
			if (string.IsNullOrEmpty(next)) 
				continue;
			
			Parse(next, out var num, out var text);
			pq.Enqueue(
				new SimpleMergeItem(text, num, item.SourceIndex),
				new SimpleMergeKey(text, num));
		}
	}
}