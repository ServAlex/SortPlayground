namespace LargeFileSort.FileSorting.MergeChunks;

internal readonly struct MergeKey : IComparable<MergeKey>
{
	private readonly string _line;
	private readonly int _textOffset;
	private readonly int _textLength;
	private readonly int _number;

	public MergeKey(MergeItem item)
	{
		_line = item.Line;
		_textOffset = item.TextOffset;
		_textLength = item.TextLength;
		_number = item.Number;
	}

	public int CompareTo(MergeKey other)
	{
		var comparison = string.CompareOrdinal(
			_line, _textOffset, 
			other._line, other._textOffset, 
			Math.Min(_textLength, other._textLength));
		
		if (comparison != 0) 
			return comparison;

		if (_textLength != other._textLength)
			return _textLength - other._textLength;
		
		return _number.CompareTo(other._number);

	}
}
