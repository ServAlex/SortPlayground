namespace LargeFileSort.Infrastructure;

public class FileSystem: IFileSystem
{
	public bool HasEnoughFreeSpace(string path, long requiredBytes)
	{
		var root = Path.GetPathRoot(Path.GetFullPath(path));

		var drive = new DriveInfo(root!);

		return drive.AvailableFreeSpace >= requiredBytes;
	}

	public bool FileExists(string path)
	{
		return File.Exists(path);
	}

	public long GetFileSize(string path)
	{
		return new FileInfo(path).Length;
	}
}