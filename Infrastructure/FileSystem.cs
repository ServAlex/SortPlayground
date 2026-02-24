namespace LargeFileSort.Infrastructure;

public class FileSystem: IFileSystem
{
	public bool HasEnoughFreeSpace(string path, long requiredBytes)
	{
		var root = Path.GetPathRoot(Path.GetFullPath(path));

		var drive = new DriveInfo(root!);

		return drive.AvailableFreeSpace >= requiredBytes;
	}	
}