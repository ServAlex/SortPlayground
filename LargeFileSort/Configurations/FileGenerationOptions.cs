using System.ComponentModel.DataAnnotations;
using LargeFileSort.Domain;

namespace LargeFileSort.Configurations;

public sealed class FileGenerationOptions
{
	[Required]
	public required bool Enabled { get; set; }
	
	[Required]
	public required bool Reuse { get; set; }
	
	public DataSize FileSize { get; set; } = null!;
}