namespace FileGenerator.FileSorter.MergeFiles;

sealed class MergeBatch
{
	public SimpleMergeItem[] Items;
	public int Count;
	public int ReaderIndex;

	public MergeBatch(SimpleMergeItem[] items)
	{
		Items = items;
	}
	
	public void Add(SimpleMergeItem item)
	{
		Items[Count++] = item;
	}
}