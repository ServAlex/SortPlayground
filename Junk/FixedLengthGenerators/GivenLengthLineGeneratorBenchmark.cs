using System.Text;
using BenchmarkDotNet.Attributes;

namespace LargeFileSort.Junk.FixedLengthGenerators;

[MemoryDiagnoser]
public class GivenLengthLineGeneratorBenchmark
{
	const int MinLenght = 5;
	const int MaxLength = 100;
	private readonly string[] _words = new[] { "apple", "cherry", "grape", "banana", "pear" };
	
	private readonly Random _rng = new();

	private void Run<T>(T generator) where T : ILineGenerator
	{
		for (var i = 0; i < 1000; i++)
		{
			generator.GenerateLine(_rng, MinLenght, MaxLength, _words);
		}
	}

//	[Benchmark]
	public void RunEmpty() => Run(new EmptyLineGenerator());
	
//	[Benchmark]
	public void RunConstant() => Run(new ConstantLineGenerator());
	
//	[Benchmark]
	public void RunConstantFromParams() => Run(new ConstantFromParamsLineGenerator());

	[Benchmark]
	public void RunFirstElement() => Run(new FirstElement100LineGenerator());
	
	[Benchmark]
	public void RunRandom() => Run(new Random100LineGenerator());

	[Benchmark]
	public void RunRandomSharedSb()
	{
		var sb = new StringBuilder(MaxLength + 30);
		var generator = new RandomSharedSbLineGenerator();
		
		for (var i = 0; i < 1000; i++)
		{
			generator.GenerateLine(_rng, sb, MinLenght, MaxLength, _words);
		}
	}
	
	[Benchmark]
	public void RunRandomSbJoin()
	{
		var sb = new StringBuilder(MaxLength + 30);
		var generator = new RandomSbJoinLineGenerator();
		
		for (var i = 0; i < 1000; i++)
		{
			generator.GenerateLine(_rng, sb, MinLenght, MaxLength, _words);
		}
	}
	
}