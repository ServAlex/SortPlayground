using System;
using System.IO;
using System.Text;

namespace FileGenerator.ReadingBenchmark;

public class ReadBenchmark
{
	public void ReadFullFile(string fileName = "text.txt")
	{
		using var reader = new StreamReader(
			fileName,
			Encoding.UTF8,
			true,
			new FileStreamOptions
			{
				BufferSize = 1 << 18,
				//Options = FileOptions.SequentialScan
			});
		
		while (!reader.EndOfStream)
		{
			var line = reader.ReadLine();
		}
	}
	
	public void ReadFileToEnd(string fileName = "text.txt")
	{
		using var reader = new StreamReader(
			fileName,
			Encoding.UTF8,
			true,
			new FileStreamOptions
			{
				BufferSize = 1 << 18,
				//Options = FileOptions.SequentialScan
			});

		reader.ReadToEnd();
	}
	
	public void ReadBlocked(string fileName = "text.txt")
	{
		//new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read));
		using var reader = new StreamReader(
			fileName,
			Encoding.UTF8,
			true,
			new FileStreamOptions
			{
				BufferSize = 1 << 18,
				//Options = FileOptions.SequentialScan
			});

		var len = reader.BaseStream.Length;
		Console.WriteLine($"file length: {len / 1024 / 1024} MB");
		
		var buffer = new char[1024*10];
		var red = 0;
		
		while (!reader.EndOfStream)
		{
			red += reader.ReadBlock(buffer, 0, buffer.Length);
		}
		
		//reader.ReadBlock()
	}
	
	public void ReadBlockedSequentialLargeBuf(string fileName = "text.txt")
	{
		var bufferSize = 1 << 20;
		//new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read));
		using var reader = new StreamReader(
			fileName,
			Encoding.UTF8,
			true,
			new FileStreamOptions
			{
				BufferSize = bufferSize,
				Options = FileOptions.SequentialScan
			});

		var len = reader.BaseStream.Length;
		Console.WriteLine($"file length: {len / 1024 / 1024} MB");
		
		var buffer = new char[bufferSize];
		var red = 0;
		
		while (!reader.EndOfStream)
		{
			red += reader.ReadBlock(buffer, 0, buffer.Length);
		}
		
		//reader.ReadBlock()
	}
}