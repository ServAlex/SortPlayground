using System.Text;
using BenchmarkDotNet.Attributes;
using FileGenerator.Generators;

namespace FileGenerator.Generators;

[MemoryDiagnoser]
public class GenerationBenchmark
{
	private const int MinLenght = 5;
	private const int MaxLenght = 100;
	private const int DesiredFileSizeMb = (int) (0.01 * 1024);

	private readonly Random _rng = new();

	private void Run<T>(T generator) where T:ILineGenerator
	{
		var desiredFileSizeBytes = (long)DesiredFileSizeMb * 1024 * 1024;

		var wordArray = new [] { "apple", "something" };

		using var writer = new StreamWriter(
			"test.txt", 
			Encoding.UTF8, 
			new FileStreamOptions
			{
				BufferSize = 1 << 20, 
				Mode = FileMode.Create, 
				Access = FileAccess.Write
			});

		while (writer.BaseStream.Length < desiredFileSizeBytes)
		{
			//var line = GenerateLine(rng, minLenght, maxLenght, wordArray);
			var line = generator.GenerateLine(_rng, MinLenght, MaxLenght, wordArray);
			writer.WriteLine(line.ToString());
		}

		writer.Flush();
		Console.WriteLine($"file created with length {writer.BaseStream.Length / 1024 / 1024} MB");
	}
	
	[Benchmark]
	public void RunConstant() => Run(new ConstantLineGenerator());
	
	[Benchmark]
	public void RunRandom() => Run(new RandomLineGenerator());
	
	//[Benchmark]
	//public void RunConstantWord() => Run(new ConstantWordLineGenerator());
	
	[Benchmark]
	public void RunNoRandom5() => Run(new NoRandom5LineGenerator());
	
	[Benchmark]
	public void RunNoRandom30() => Run(new NoRandom30LineGenerator());
	
	[Benchmark]
	public void RunNoRandom100() => Run(new NoRandom100LineGenerator());
	
	[Benchmark]
	public void RunConstantSb() => Run(new ConstantSbLineGenerator());
	
	//[Benchmark]
	//public void RunConstantIntSb() => Run(new ConstantIntSbLineGenerator());
}