namespace FileGenerator.FileSorter;

public class LargeFileSorterOptions
{
	public int BufferSize;
	public int SortWorkerCount;
	public int MergeWorkerCount;
	public int QueueLength;
	public int ChunkSize;
	public int FileMaxLengthMb;
	public int MemoryBudgetMb;
	public int EmpiricalConservativeLineLength = 50;
}