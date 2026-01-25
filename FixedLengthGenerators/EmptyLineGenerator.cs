namespace FileGenerator.FixedLengthGenerators;

public class EmptyLineGenerator: ILineGenerator
{
	public Line GenerateLine(Random random, int minLenght, int maxLenght, string[] words)
	{
		return new Line(0, string.Empty);
	}
}