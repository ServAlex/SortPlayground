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
		if (!PathOptions.DeleteAllCreatedFiles)
		{
			return;
		}
		
		var unsortedFilePath = Path.Combine(PathOptions.FilesLocation, PathOptions.UnsortedFileName);
		if (File.Exists(unsortedFilePath))
		{
			File.Delete(unsortedFilePath);
		}
			
		var sortedFilePath = Path.Combine(PathOptions.FilesLocation, PathOptions.SortedFileName);
		if (File.Exists(sortedFilePath))
		{
			File.Delete(sortedFilePath);
		}
			
		var chunksDirectoryPath = Path.Combine(PathOptions.FilesLocation, PathOptions.ChunksDirectoryBaseName);
		if (Directory.Exists(chunksDirectoryPath))
		{
			Directory.Delete(chunksDirectoryPath, true);
		}
			
		Console.WriteLine($"Delete flag enabled, deleted unsorted file, chunks directory and sorted file at {PathOptions.FilesLocation}");
	}
}