namespace LargeFileSort.FileSorter.MergeChunks;

sealed class MergeBatch
{
	public SimpleMergeItem[] Items;
	public int Count;
	public int CurrentReadIndex;

	public MergeBatch(SimpleMergeItem[] items)
	{
		Items = items;
	}
	
	public void Add(SimpleMergeItem item)
	{
		Items[Count++] = item;
	}
}