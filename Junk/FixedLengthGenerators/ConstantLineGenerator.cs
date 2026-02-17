namespace LargeFileSort.Junk.FixedLengthGenerators;

public class ConstantLineGenerator: ILineGenerator
{
	public Line GenerateLine(Random random, int minLenght, int maxLenght, string[] words)
	{
		return new Line(4, "some constant string");
	}
}