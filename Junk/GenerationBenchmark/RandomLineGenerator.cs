using System.Text;

namespace FileGenerator.GenerationBenchmark;

public class RandomLineGenerator: ILineGenerator
{
	public Line GenerateLine(Random random, int minLenght, int maxLenght, string[] words)
	{
		var length = random.Next(minLenght, maxLenght);
		var sb = new StringBuilder(maxLenght + 30);
		sb.Append(words[random.Next(words.Length)]);

		while (sb.Length < length)
		{
			sb.Append(' ').Append(words[random.Next(words.Length)]);
		}
	
		return new Line(random.Next(), sb.ToString());

		//var key = new string(Enumerable.Repeat(wordArray[rng.Next(0, wordArray.Length)], length).Select(s => s[rng.Next(0, s.Length)]).ToArray());
		//return new Line(rng.Next(), key);
	}
}