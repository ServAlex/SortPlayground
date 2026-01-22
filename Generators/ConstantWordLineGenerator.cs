using System.Text;

namespace FileGenerator.Generators;

public class ConstantWordLineGenerator: ILineGenerator
{
	public Line GenerateLine(Random random, int minLenght, int maxLenght, string[] words)
	{
		return new Line(4, words[0]);
	}
}