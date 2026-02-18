using System.ComponentModel.DataAnnotations;

namespace LargeFileSort.Configurations;

public sealed class SortOptions
{
	[Required]
	public required bool Enabled { get; set; }
	
	[Required]
	[Range(128, 4095)]
	public required int IntermediateFileSizeMaxMb { get; set; }
	
	[Required]
	[Range(1, 2047)]
	public required int BaseChunkSizeMb { get; set; }
	
	[Required]
	[Range(8, 128)]
	public required int MemoryBudgetGb { get; set; }
	
	[Required]
	[Range(1, 10)]
	public required int QueueLength { get; set; }
	
	[Required]
	[Range(1, 10)]
	public required int MergeWorkerCount { get; set; }
	
	[Required]
	[Range(1, 20)]
	public required int SortWorkerCount { get; set; }
	
	[Required]
	[Range(1, 32)]
	public required int BufferSizeMb { get; set; }
}