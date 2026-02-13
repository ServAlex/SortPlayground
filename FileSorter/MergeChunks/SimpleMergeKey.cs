namespace FileGenerator.FileSorter.MergeChunks;

internal readonly struct SimpleMergeKey : IComparable<SimpleMergeKey>
{
	private readonly string _line;
	private readonly int _textOffset;
	private readonly int _textLength;
	private readonly int _number;

	public SimpleMergeKey(SimpleMergeItem item)
	{
		_line = item.Line;
		_textOffset = item.TextOffset;
		_textLength = item.TextLength;
		_number = item.Number;
	}

	public int CompareTo(SimpleMergeKey other)
	{
		var comparison = string.CompareOrdinal(
			_line, _textOffset, 
			other._line, other._textOffset, 
			Math.Min(_textLength, other._textLength));
		
		if (comparison != 0) 
			return comparison;
		
		return _number.CompareTo(other._number);

	}
}
