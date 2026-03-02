using System.ComponentModel.DataAnnotations;
using LargeFileSort.Domain;

namespace LargeFileSort.Configurations;

public sealed class SortOptions
{
	[Required]
	public required bool Enabled { get; set; }
	
	[Required]
	public required bool ReuseChunks { get; set; }

	public DataSize ChunkFileSizeMax { get; set; } = null!;
	
	[Required]
	[Range(1, 2047)]
	public required int BaseChunkSizeMb { get; set; }
	
	[Required]
	[Range(1, 100)]
	public required int QueueLength { get; set; }
	
	[Required]
	[Range(1, 10)]
	public required int MergeWorkerCount { get; set; }
	
	[Required]
	[Range(1, 10)]
	public required int MergeToFileWorkerCount { get; set; }
	
	[Required]
	[Range(1, 20)]
	public required int SortWorkerCount { get; set; }
	
	[Required]
	[Range(1, 32)]
	public required int BufferSizeMb { get; set; }
}