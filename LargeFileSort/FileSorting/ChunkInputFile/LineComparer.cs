namespace LargeFileSort.FileSorting.ChunkInputFile;

/// <summary>
/// Compares two lines in the buffer, located by their metadata
/// </summary>
/// <param name="buffer"></param>
public readonly struct LineComparer(char[] buffer) : IComparer<LineMetadata>
{
	/// <summary>
	/// LineMetadata locates the lines in the buffer
	/// </summary>
	/// <returns>
	/// 0 if the values are equal
	/// Negative number if _value is less than value
	/// Positive number if _value is more than value
	/// </returns>
	public int Compare(LineMetadata a, LineMetadata b)
	{
		var prefixComparison = a.Prefix.CompareTo(b.Prefix);
		if (prefixComparison != 0)
			return prefixComparison;

		var spanA = buffer.AsSpan(a.LineOffset + a.StringOffsetInLine, a.StringLength);
		var spanB = buffer.AsSpan(b.LineOffset + b.StringOffsetInLine, b.StringLength);

		var sequenceCompare = spanA.SequenceCompareTo(spanB);
		if (sequenceCompare != 0)
			return sequenceCompare;

		return a.Number.CompareTo(b.Number);
	}
}