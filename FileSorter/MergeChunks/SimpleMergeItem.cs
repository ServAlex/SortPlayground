namespace FileGenerator.FileSorter.MergeChunks;

public readonly struct SimpleMergeItem
{
	public readonly string Line;
	public readonly int TextOffset;
	public readonly int TextLength;
	public readonly int Number;
	public readonly int SourceIndex;

	public SimpleMergeItem(string line, int fileIndex)
	{
		Line = line;
		SourceIndex = fileIndex;
		
		var data = line.AsSpan();
		var i = 0;
		Number = 0;
		while (data[i] != '.' && i < data.Length)
			Number = Number * 10 + (data[i++] - '0');

		TextOffset = i + 1;
		TextLength = line.Length - (i + 1);	
	}

	public SimpleMergeItem(SimpleMergeItem oldItem, int fileIndex)
	{
		Line = oldItem.Line;
		SourceIndex = fileIndex;
		Number = oldItem.Number;
		TextOffset = oldItem.TextOffset;
		TextLength = oldItem.TextLength;
	}
}