using System.Text;

namespace LargeFileSort.Junk.FixedLengthGenerators;

public class FirstElement100LineGenerator: ILineGenerator
{
	public Line GenerateLine(Random random, int minLenght, int maxLength, string[] words)
	{
		var sb = new StringBuilder(maxLength + 30);
		sb.Append(words[0]);

		while (sb.Length < maxLength)
		{
			sb.Append(' ').Append(words[0]);
		}
	
		return new Line(random.Next(), sb.ToString());
	}
}