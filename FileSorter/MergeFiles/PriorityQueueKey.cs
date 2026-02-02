namespace FileGenerator.FileSorter.MergeFiles;

public class PriorityQueueKey(SortedFileReader reader) : IComparable<PriorityQueueKey>
{
	public readonly SortedFileReader Reader = reader;

	public int CompareTo(PriorityQueueKey? other)
	{
		// todo: check if -1 works
		if(other is null || other.Reader.EndOfFile)
			return -1;
		
		if(Reader.EndOfFile)
			return 1;

		return Compare(Reader.Line, other.Reader.Line, Reader.LineString, other.Reader.LineString);
	}
	
	private static int Compare(Line a, Line b, string stringA, string stringB)
	{
		var prefixComparison = a.Prefix.CompareTo(b.Prefix);
		if (prefixComparison != 0)
			return prefixComparison;

		var spanA = stringA.AsSpan(a.LineOffset + a.StringOffsetFromLine, a.StringLength);
		var spanB = stringB.AsSpan(b.LineOffset + b.StringOffsetFromLine, b.StringLength);

		var sequenceCompare = spanA.SequenceCompareTo(spanB);
		if (sequenceCompare != 0)
			return sequenceCompare;

		return a.Number.CompareTo(b.Number);
	}
}