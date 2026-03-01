using System.ComponentModel.DataAnnotations;
using LargeFileSort.Domain;

namespace LargeFileSort.Configurations;

public sealed class GeneralOptions
{
	[Required]
	public required bool DeleteAllCreatedFiles { get; set; }
	
	[Required]
	public required string UnsortedFileName { get; set; }
	
	[Required]
	public required string SortedFileName { get; set; }
	
	public required string FilesLocation { get; set; }
	
	[Required]
	public required string ChunksDirectoryBaseName { get; set; }

	public DataSize MemoryBudget { get; set; } = null!;
	
	[Required]
	public required bool KeepChunks { get; set; }
}