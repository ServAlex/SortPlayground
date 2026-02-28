namespace LargeFileSort.Infrastructure;

public interface IFileSystem
{
	bool HasEnoughFreeSpace(string path, long requiredBytes);
	bool FileExists(string path);
	long GetFileSize(string path);
	bool DirectoryExists(string path);
	void DeleteDirectory(string path, bool recursive);
	void CreateDirectory(string path);
	void DeleteFile(string path);
	FileInfo[] GetFiles(string path);
	StreamWriter GetFileWriter(string path, int bufferSize);
	StreamReader GetFileReader(string path, int bufferSize);
	
}