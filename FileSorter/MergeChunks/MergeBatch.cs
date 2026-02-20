namespace LargeFileSort.FileSorter.MergeChunks;

internal sealed class MergeBatch(MergeItem[] items)
{
	public readonly MergeItem[] Items = items;
	public int Count;
	public int CurrentReadIndex;

	public void Add(MergeItem item)
	{
		Items[Count++] = item;
	}
}