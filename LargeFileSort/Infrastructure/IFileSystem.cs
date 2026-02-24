namespace LargeFileSort.Infrastructure;

public interface IFileSystem
{
	bool HasEnoughFreeSpace(string path, long requiredBytes);
}