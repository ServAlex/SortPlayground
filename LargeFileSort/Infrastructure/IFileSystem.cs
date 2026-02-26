namespace LargeFileSort.Infrastructure;

public interface IFileSystem
{
	bool HasEnoughFreeSpace(string path, long requiredBytes);
	bool FileExists(string path);
	long GetFileSize(string path);
}