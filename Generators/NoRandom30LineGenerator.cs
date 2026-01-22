using System.Text;

namespace FileGenerator.Generators;

public class NoRandom30LineGenerator: ILineGenerator
{
	public Line GenerateLine(Random random, int minLenght, int maxLenght, string[] words)
	{
		var length = 30;
		var sb = new StringBuilder(maxLenght + 30);
		sb.Append(words[0]);

		while (sb.Length < length)
		{
			sb.Append(' ').Append(words[0]);
		}
	
		return new Line(random.Next(), sb.ToString());
	}
}