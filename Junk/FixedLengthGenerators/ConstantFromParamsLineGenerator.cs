namespace LargeFileSort.Junk.FixedLengthGenerators;

public class ConstantFromParamsLineGenerator: ILineGenerator
{
	public Line GenerateLine(Random random, int minLenght, int maxLenght, string[] words)
	{
		return new Line(minLenght, words[0]);
	}
}