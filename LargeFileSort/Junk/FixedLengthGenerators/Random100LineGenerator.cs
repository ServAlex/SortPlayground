using System.Text;

namespace LargeFileSort.Junk.FixedLengthGenerators;

public class Random100LineGenerator: ILineGenerator
{
	public Line GenerateLine(Random random, int minLenght, int maxLength, string[] words)
	{
		var sb = new StringBuilder(maxLength + 30);
		sb.Append(words[random.Next(words.Length)]);

		while (sb.Length < maxLength)
		{
			sb.Append(' ').Append(words[random.Next(words.Length)]);
		}
	
		return new Line(random.Next(), sb.ToString());
	}
}