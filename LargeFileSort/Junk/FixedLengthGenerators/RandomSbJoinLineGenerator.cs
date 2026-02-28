using System.Text;

namespace LargeFileSort.Junk.FixedLengthGenerators;

public class RandomSbJoinLineGenerator
{
	public Line GenerateLine(Random random, StringBuilder sb, int minLenght, int maxLength, string[] words)
	{
		sb.Clear();
	
		return new Line(
			random.Next(), 
			sb.AppendJoin(' ', random.GetItems(words, 105 / 6)).ToString());	
	}
}