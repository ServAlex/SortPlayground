using System.Text;

namespace LargeFileSort.Junk.FixedLengthGenerators;

public class RandomSharedSbLineGenerator
{
	public Line GenerateLine(Random random, StringBuilder sb, int minLenght, int maxLength, string[] words)
	{
		sb.Clear();
		sb.Append(words[random.Next(words.Length)]);

		while (sb.Length < maxLength)
		{
			sb.Append(' ').Append(words[random.Next(words.Length)]);
		}
	
		return new Line(random.Next(), sb.ToString());	
	}
}