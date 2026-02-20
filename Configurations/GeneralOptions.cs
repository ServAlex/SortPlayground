using System.ComponentModel.DataAnnotations;

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
	
	[Required]
	[Range(8, 128)]
	public required int MemoryBudgetGb { get; set; }
	
	[Required]
	public required bool KeepChunks { get; set; }
}