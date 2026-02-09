using System.Diagnostics;
using System.Text;

namespace FileGenerator.FileSorter.MergeFiles;

public abstract class SortedFilesMerger
{
	protected long LinesWritten { get; set;}
	protected long BytesWritten { get; set;}
	protected long BytesRead { get; set;}
	protected Stopwatch? Stopwatch { get; set;}

	public abstract long MergeSortedFiles(
		string directoryName,
		string destinationFileName,
		int readerBufferSize,
		int writerBufferSize);

	protected void LogStage(Stopwatch sw, Func<string>? getCustomLine, CancellationToken cancellationToken)
	{
		Thread.Sleep(1000);
		var lastBytesWritten = 0L;
		var lastBytesRead = 0L;
		var lastUpdateTime = sw.ElapsedMilliseconds;
		
		const int lines = 5;
		var startLine = Console.CursorTop;
		for (var i = 0; i < lines; i++)
			Console.WriteLine();
		
		while (!cancellationToken.IsCancellationRequested)
		{
			// move cursor, cross-platform way
			Console.Write($"\e[{lines}A");
			
			var sb = new StringBuilder();
			if (getCustomLine is not null)
			{
				sb.Append(getCustomLine());
				StringBuilderWriteAndReset(sb);
			}
			
			sb.Append($"   Written:    {BytesWritten/1024/1024:N0} MB");
			StringBuilderWriteAndReset(sb);
			
			sb.Append($"   R/W speed: {(double)(BytesRead-lastBytesRead)/(sw.ElapsedMilliseconds - lastUpdateTime)*1000/1024/1024,5:N1} MB/s");
			sb.Append($" /{(double)(BytesWritten-lastBytesWritten)/(sw.ElapsedMilliseconds - lastUpdateTime)*1000/1024/1024,5:N1} MB/s");
			lastUpdateTime = sw.ElapsedMilliseconds;
			lastBytesWritten = BytesWritten;
			lastBytesRead = BytesRead;
			StringBuilderWriteAndReset(sb);
			
			sb.Append($"   Avg R/W:   {(double)BytesRead/sw.ElapsedMilliseconds*1000/1024/1024,5:N1} MB/s");
			sb.Append($" /{(double)BytesWritten/sw.ElapsedMilliseconds*1000/1024/1024,5:N1} MB/s");
			StringBuilderWriteAndReset(sb);
			
			sb.Append($"   Time:       {(double)sw.ElapsedMilliseconds/1000:F1} ms");
			StringBuilderWriteAndReset(sb);
			
			Console.SetCursorPosition(0, startLine);
			Thread.Sleep(200);
		}
		Console.WriteLine();
	}
	
	private static void StringBuilderWriteAndReset(StringBuilder sb)
	{
		Console.WriteLine(sb);
		sb.Clear();
		// clear line, cross-platform way
		sb.Append("\e[2K");
	}
}