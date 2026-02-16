namespace LargeFileSort.Configurations;

public class SortOptions
{
	public bool Enabled { get; set; }
	public int IntermediateFileSizeMaxMb { get; set; }
	public int BaseChunkSizeMb { get; set; }
	public int MemoryBudgetGb { get; set; }
	public int QueueLength { get; set; }
	public int MergeWorkerCount { get; set; }
	public int SortWorkerCount { get; set; }
	public int BufferSizeMb { get; set; }
}