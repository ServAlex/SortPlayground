using System.Text;
using BenchmarkDotNet.Attributes;

namespace FileGenerator.FileWriterBenchmark;

[MemoryDiagnoser]
public class MultiGbFileWriterBenchmark
{
	
	[Params(1 << 16, 1 << 18, 1 << 20, 1 << 22)] public int BufferSize { get; set; }
	//[Params(1 << 18)] public int BufferSize { get; set; }
	[Params(1 << (20 + 10 + 2 - 7))] public int LinesCount { get; set; }
	
	[Benchmark]
	public void WriteWithStringBuilderNoFlush()
	{
		using var writer = new StreamWriter(
			$"test_{BufferSize}.txt", 
			Encoding.UTF8, 
			new FileStreamOptions
			{
				BufferSize = BufferSize, 
				Mode = FileMode.Create, 
				Access = FileAccess.Write
			});

		var sb = new StringBuilder(128);
		for (var i = 0; i < LinesCount; i++)
		{
			sb.Clear();
			// 128 char long counting \n
			sb.Append("0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456");
			writer.WriteLine(sb);
		}
		
		// check if needed
		//writer.Flush();
		Console.WriteLine($"file created with length {writer.BaseStream.Length / 1024 / 1024 } MB");
	}
}