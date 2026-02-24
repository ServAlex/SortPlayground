using System.ComponentModel.DataAnnotations;

namespace LargeFileSort.Configurations;

public sealed class FileGenerationOptions
{
	[Required]
	public required bool Enabled { get; set; }
	
	[Required]
	public required bool Reuse { get; set; }
	
	[Required]
	[Range(1, 100)]
	public required int FileSizeGb { get; set; }
}