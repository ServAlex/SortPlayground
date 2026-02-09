namespace FileGenerator.FileSorter.MergeFiles;

public readonly struct SimpleMergeItem
{
	public readonly string Text;
	public readonly int Number;
	public readonly int SourceIndex;

	public SimpleMergeItem(string text, int number, int fileIndex)
	{
		Text = text;
		Number = number;
		SourceIndex = fileIndex;
	}
}