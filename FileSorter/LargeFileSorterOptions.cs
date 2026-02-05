namespace FileGenerator.FileSorter;

public class LargeFileSorterOptions
{
	public int BufferSizeMb { get; set; }
	public int SortWorkerCount { get; set; }
	public int MergeWorkerCount { get; set; }
	public int QueueLength { get; set; }
	public int ChunkSizeMb { get; set; }
	public int FileMaxLengthMb { get; set; }
	public int MemoryBudgetGb { get; set; }
	public int EmpiricalConservativeLineLength { get; set; } = 50;
}