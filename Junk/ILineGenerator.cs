namespace FileGenerator;

public interface ILineGenerator
{
	Line GenerateLine(Random random, int minLenght, int maxLength, string[] words);
}