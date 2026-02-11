using System.Text;

namespace FileGenerator.GenerationBenchmark;

public class ConstantSbLineGenerator: ILineGenerator
{
	public Line GenerateLine(Random random, int minLenght, int maxLenght, string[] words)
	{
		var sb = new StringBuilder(maxLenght + 30);
		sb.Append(words[0]);
	
		return new Line(random.Next(), sb.ToString());
	}
}