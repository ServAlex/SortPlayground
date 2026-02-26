using LargeFileSort.Configurations;
using LargeFileSort.Infrastructure;
using Microsoft.Extensions.Options;

namespace LargeFileSort.FileDeletion;

public class LeftoversRemover
{
	private GeneralOptions GeneralOptions { get; }
	private IFileSystem FileSystem { get; }
	
	public LeftoversRemover(IOptions<GeneralOptions> pathOptions, IFileSystem fileSystem)
	{
		FileSystem = fileSystem;
		GeneralOptions = pathOptions.Value;
	}

	public void Remove()
	{
		var chunksDirectoryPath = Path.Combine(GeneralOptions.FilesLocation, GeneralOptions.ChunksDirectoryBaseName);
		if ((!GeneralOptions.KeepChunks || GeneralOptions.DeleteAllCreatedFiles) && Directory.Exists(chunksDirectoryPath))
		{
			Console.WriteLine($"Deleted chunks directory at {chunksDirectoryPath}");
			Directory.Delete(chunksDirectoryPath, true);
		}
		
		if (!GeneralOptions.DeleteAllCreatedFiles)
		{
			return;
		}
		
		var unsortedFilePath = Path.Combine(GeneralOptions.FilesLocation, GeneralOptions.UnsortedFileName);
		if (FileSystem.FileExists(unsortedFilePath))
		{
			Console.WriteLine($"Deleted unsorted file at {unsortedFilePath}");
			File.Delete(unsortedFilePath);
		}
			
		var sortedFilePath = Path.Combine(GeneralOptions.FilesLocation, GeneralOptions.SortedFileName);
		if (FileSystem.FileExists(sortedFilePath))
		{
			Console.WriteLine($"Deleted sorted file at {sortedFilePath}");
			File.Delete(sortedFilePath);
		}
	}
}