namespace FileGenerator.FileSorter.MergeFiles;

internal readonly struct SimpleMergeItem
{
	public readonly string Text;
	public readonly int Number;
	public readonly int FileIndex;

	public SimpleMergeItem(string text, int number, int fileIndex)
	{
		Text = text;
		Number = number;
		FileIndex = fileIndex;
	}
}