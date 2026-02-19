using LargeFileSort.Configurations;
using Microsoft.Extensions.Options;

namespace LargeFileSort.FileDeletion;

public class LeftoversRemover
{
	private PathOptions PathOptions { get; }
	
	public LeftoversRemover(IOptions<PathOptions> pathOptions)
	{
		PathOptions = pathOptions.Value;
	}

	public void Remove()
	{
		var chunksDirectoryPath = Path.Combine(PathOptions.FilesLocation, PathOptions.ChunksDirectoryBaseName);
		if ((!PathOptions.KeepChunks || PathOptions.DeleteAllCreatedFiles) && Directory.Exists(chunksDirectoryPath))
		{
			Console.WriteLine($"Deleted chunks directory at {chunksDirectoryPath}");
			Directory.Delete(chunksDirectoryPath, true);
		}
		
		if (!PathOptions.DeleteAllCreatedFiles)
		{
			return;
		}
		
		var unsortedFilePath = Path.Combine(PathOptions.FilesLocation, PathOptions.UnsortedFileName);
		if (File.Exists(unsortedFilePath))
		{
			Console.WriteLine($"Deleted unsorted file at {unsortedFilePath}");
			File.Delete(unsortedFilePath);
		}
			
		var sortedFilePath = Path.Combine(PathOptions.FilesLocation, PathOptions.SortedFileName);
		if (File.Exists(sortedFilePath))
		{
			Console.WriteLine($"Deleted sorted file at {sortedFilePath}");
			File.Delete(sortedFilePath);
		}
	}
}