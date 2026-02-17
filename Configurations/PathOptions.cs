namespace LargeFileSort.Configurations;

public class PathOptions
{
	public string UnsortedFileName { get; set; }
	public string SortedFileName { get; set; }
	public string FilesLocation { get; set; }
	public string ChunksDirectoryBaseName { get; set; }
	public bool KeepChunks { get; set; }
	public bool DeleteAllCreatedFiles { get; set; }
}