namespace FileGenerator.FileSorter.MergeFiles;

internal readonly struct SimpleMergeKey : IComparable<SimpleMergeKey>
{
	public readonly string Text;
	public readonly int Number;

	public SimpleMergeKey(string text, int number)
	{
		Text = text;
		Number = number;
	}

	public int CompareTo(SimpleMergeKey other)
	{
		var comparison = string.Compare(Text, other.Text, StringComparison.Ordinal);
		if (comparison != 0) return comparison;
		
		return Number.CompareTo(other.Number);
	}
}
