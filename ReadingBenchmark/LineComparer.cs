namespace FileGenerator.ReadingBenchmark;

public readonly struct LineComparer(char[] buffer) : IComparer<Line>
{
	public int Compare(Line a, Line b)
	{
		var prefixComparison = a.Prefix.CompareTo(b.Prefix);
		if (prefixComparison != 0)
			return prefixComparison;

		var spanA = buffer.AsSpan(a.StringOffset, a.StringLength);
		var spanB = buffer.AsSpan(b.StringOffset, b.StringLength);

		var sequenceCompare = spanA.SequenceCompareTo(spanB);
		if (sequenceCompare != 0)
			return sequenceCompare;

		return a.Number.CompareTo(b.Number);
	}
}