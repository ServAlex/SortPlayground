namespace LargeFileSort.FileSorting.MergeChunks;

public readonly struct MergeKey(MergeItem item) : IComparable<MergeKey>
{
	private readonly string _line = item.Line;
	private readonly int _textOffset = item.TextOffset;
	private readonly int _textLength = item.TextLength;
	private readonly int _number = item.Number;

	public int CompareTo(MergeKey other)
	{
		var comparison = string.CompareOrdinal(
			_line, _textOffset, 
			other._line, other._textOffset, 
			Math.Max(_textLength, other._textLength));
		
		if (comparison != 0) 
			return comparison;
		
		return _number.CompareTo(other._number);
	}
}
