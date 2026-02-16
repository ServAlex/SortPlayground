using System.Diagnostics;
using System.Text;

namespace LargeFileSort.FileSorter;

public class FileProgressLogger
{
	public long LinesWritten { get; set;}
	public long BytesWritten { get; set;}
	public long BytesRead { get; set;}
	public int MemoryBudgetMb { get; set; }
	public bool IsCountersSynchronized { get; set; }
	
	private DateTime StartTime { get; set;}
	
	private StringBuilder _sb = new();

	public void LogState(DateTime startTime, Func<string>? getCustomLine, CancellationToken cancellationToken)
	{
		Thread.Sleep(1500);
		var lastBytesWritten = 0L;
		var lastBytesRead = 0L;
		var lastUpdateTime = DateTime.Now;
		StartTime = startTime;
		
		Console.WriteLine();

		var linesLogged = 0;
		while (!cancellationToken.WaitHandle.WaitOne(200))
		{
			// move cursor, cross-platform way
			Console.Write($"\e[{linesLogged}A");
			linesLogged = 0;
			if (_sb.Length > 0)
			{
				Console.Write(_sb);
				_sb.Clear();
			}

			var sb = new StringBuilder();
			sb.Append("\e[2K");
			StringBuilderWriteAndReset(sb, ref linesLogged);
			
			if (getCustomLine is not null)
			{
				sb.Append(getCustomLine());
				StringBuilderWriteAndReset(sb, ref linesLogged);
			}

			sb.Append($"   Written:   {BytesWritten/1024/1024,6:N0} MB");
			StringBuilderWriteAndReset(sb, ref linesLogged);

			var newTime = DateTime.Now;
			sb.Append($"   R/W speed: {(BytesRead-lastBytesRead)/(newTime - lastUpdateTime).TotalSeconds/1024/1024,6:N1} MB/s");
			sb.Append($"  / {(BytesWritten-lastBytesWritten)/(newTime - lastUpdateTime).TotalSeconds/1024/1024,6:N1} MB/s");
			lastUpdateTime = newTime;
			lastBytesWritten = BytesWritten;
			lastBytesRead = BytesRead;
			StringBuilderWriteAndReset(sb, ref linesLogged);

			var secondsPassed = (newTime - StartTime).TotalSeconds;
			sb.Append($"   Avg R/W:   {BytesRead/secondsPassed/1024/1024,6:N1} MB/s");
			sb.Append($"  / {BytesWritten/secondsPassed/1024/1024,6:N1} MB/s");
			StringBuilderWriteAndReset(sb, ref linesLogged);

			sb.Append($"   Time:       {secondsPassed,5:F1} s");
			StringBuilderWriteAndReset(sb, ref linesLogged);

			var workingSetGb = Process.GetCurrentProcess().WorkingSet64 / 1024d / 1024 / 1024;
			var gcInfo = GC.GetGCMemoryInfo();
			var memoryLoad = gcInfo.MemoryLoadBytes / 1024d / 1024 / 1024;
			var totalSystemMemoryGb = gcInfo.TotalAvailableMemoryBytes / 1024d / 1024 / 1024;
			sb.Append($"   RAM budget:{workingSetGb,5:F1} / {MemoryBudgetMb / 1024d:F1} GB");
			sb.Append($"    System memory load: {memoryLoad,5:F1} GB");
			sb.Append($"    Total RAM: {totalSystemMemoryGb,5:F1} GB");
			sb.Append($"    Pause: {gcInfo.PauseTimePercentage,5:F1}%");
			StringBuilderWriteAndReset(sb, ref linesLogged);

			//Console.SetCursorPosition(0, startLine);
			//Thread.Sleep(200);
		}
		Console.WriteLine();
		Console.WriteLine();
	}

	public void LogSingleMessage(string message)
	{
		_sb.AppendLine(message);
	}

	public FileProgressLogger Reset()
	{
		StartTime = DateTime.Now;
		_sb.Clear();
		LinesWritten = 0;
		BytesWritten = 0;
		BytesRead = 0;
		return this;
	}
	
	private static void StringBuilderWriteAndReset(StringBuilder sb, ref int lines)
	{
		Console.WriteLine(sb);
		sb.Clear();
		// clear line, cross-platform way
		sb.Append("\e[2K");
		// todo: actually count lines in sb
		lines++;
	}

	public void LogCompletion()
	{
		var synchronizedNote = IsCountersSynchronized ? "" : ", MAY BE OFF due to unsynchronized counter increments";
		Console.WriteLine($"Written total: {LinesWritten} lines, {BytesWritten} bytes, time: {(DateTime.Now - StartTime).TotalMilliseconds} ms{synchronizedNote}");
		Console.WriteLine();
	}
}
