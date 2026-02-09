namespace FileGenerator.FileSorter.MergeFiles;

internal readonly struct SimpleMergeKey : IComparable<SimpleMergeKey>
{
	public readonly string Line;
	public readonly int TextOffset;
	public readonly int TextLength;
	public readonly int Number;

	public SimpleMergeKey(SimpleMergeItem item)
	{
		Line = item.Line;
		TextOffset = item.TextOffset;
		TextLength = item.TextLength;
		Number = item.Number;
	}

	public int CompareTo(SimpleMergeKey other)
	{
		var comparison = string.CompareOrdinal(
			Line, TextOffset, 
			other.Line, other.TextOffset, 
			Math.Min(TextLength, other.TextLength));
		
		if (comparison != 0) 
			return comparison;
		
		return Number.CompareTo(other.Number);

	}
}
