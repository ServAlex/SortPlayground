using System.Text;

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

	public bool DirectoryExists(string path)
	{
		return Directory.Exists(path);
	}

	public void DeleteDirectory(string path, bool recursive)
	{
		Directory.Delete(path, recursive);
	}

	public void CreateDirectory(string path)
	{
		Directory.CreateDirectory(path);
	}

	public void DeleteFile(string path)
	{
		File.Delete(path);
	}

	public FileInfo[] GetFiles(string path)
	{
		return new DirectoryInfo(path).GetFiles();
	}

	public StreamWriter GetFileWriter(string path, int bufferSize)
	{
		return new StreamWriter(
			path,
			Encoding.UTF8,
			new FileStreamOptions
			{
				BufferSize = bufferSize,
				Mode = FileMode.Create,
				Access = FileAccess.Write
			});
	}

	public StreamReader GetFileReader(string path, int bufferSize)
	{
		return new StreamReader(
			path,
			Encoding.UTF8,
			true,
			new FileStreamOptions
			{
				BufferSize = bufferSize,
				Options = FileOptions.SequentialScan
			});			
	}
}