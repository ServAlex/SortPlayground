using System.Text;
using BenchmarkDotNet.Attributes;

namespace LargeFileSort.Junk.FullGeneratorBenchmark;

[MemoryDiagnoser]
[MinWarmupCount(1)]
[MaxWarmupCount(3)]
[MaxIterationCount(16)]
public class FullGeneratorBenchmark
{
	private const string FileName = "test.txt";
	private const int DesiredFileSizeMb = 1024;
	private const int StringPartMaxLength = 100;
	
	//[Params(8, 16, 32, 64, 128, 256, 512, 1024)]
	[Params(512, 1024)]
	public int BatchSize = 512;
	
	private static readonly string[] Words = [
		"berry", 
		"apple", 
		"banana", 
		"cherry", 
		"date", 
		"fig", 
		"grape", 
		"kiwi", 
		"lemon", 
		"mango", 
		"orange", 
		"pear", 
		"strawberry", 
		"watermelon", 
		"yogurt", 
		"zucchini", 
		"pineapple", 
		"peach"
	];
	
	//[Benchmark]
	public void GenerateFileSingleThreadedLineByLine()
	{
		using var writer = new StreamWriter(
			FileName, 
			Encoding.UTF8, 
			new FileStreamOptions
			{
				BufferSize = 1 << 18, 
				//BufferSize = 1 << 22, 
				Mode = FileMode.Create, 
				Access = FileAccess.Write
			});

		var random = new Random();
		var stringBuilder = new StringBuilder(StringPartMaxLength + 20);
		
		while (writer.BaseStream.Length < (long)DesiredFileSizeMb * 1024 * 1024)
		{
			stringBuilder.Clear();
			stringBuilder.Append(random.Next()).Append(". ");
			var headerLength = stringBuilder.Length;
			
			var stringPartSoftLength = random.Next(StringPartMaxLength);
			stringBuilder.Append(Words[random.Next(Words.Length)]);
			var nextWord = Words[random.Next(Words.Length)];
			
			while (stringBuilder.Length - headerLength + 1 + nextWord.Length <= stringPartSoftLength)
			{
				stringBuilder.Append(' ').Append(nextWord);
				nextWord = Words[random.Next(Words.Length)];
			}
				
			writer.WriteLine(stringBuilder);
		}
	}
	
	[Benchmark]
	[ArgumentsSource("DesiredFileSizeMb")]
	public void GenerateFileSingleThreadedBatched(int fileSizeMb)
	{
		using var writer = new StreamWriter(
			FileName, 
			Encoding.UTF8, 
			new FileStreamOptions
			{
				BufferSize = 1 << 22, 
				Mode = FileMode.Create, 
				Access = FileAccess.Write
			});

		var random = new Random();
		var stringBuilder = new StringBuilder((StringPartMaxLength + 20) * BatchSize);
		
		while (writer.BaseStream.Length < (long)fileSizeMb * 1024 * 1024)
		{
			stringBuilder.Clear();

			for (int i = 0; i < BatchSize; i++)
			{
				stringBuilder.Append(random.Next()).Append(". ");
				
				var stringPartSoftLength = random.Next(StringPartMaxLength);
				var nextWord = Words[random.Next(Words.Length)];
				var stringLength = nextWord.Length;
				stringBuilder.Append(nextWord);
				nextWord = Words[random.Next(Words.Length)];
				
				while(stringLength + nextWord.Length <= stringPartSoftLength)
				{
					stringBuilder.Append(' ').Append(nextWord);
					stringLength += nextWord.Length + 1;
					nextWord = Words[random.Next(Words.Length)];
				}
				stringBuilder.AppendLine();
			}
			//Console.WriteLine(stringBuilder.Length);
				
			writer.Write(stringBuilder);
		}
	}
	
}