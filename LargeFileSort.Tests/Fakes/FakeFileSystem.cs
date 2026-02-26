using LargeFileSort.Infrastructure;

namespace LargeFileSort.Tests.Fakes;

public class FakeFileSystem : IFileSystem
{
	public bool HasEnoughSpaceResult { get; set; } = true;
	public bool FileExistsResult { get; set; } = true;
	public long GetFileSizeResult { get; set; } = 0;

	public bool HasEnoughFreeSpace(string path, long requiredBytes) => HasEnoughSpaceResult;

	public bool FileExists(string path) => FileExistsResult;
	public long GetFileSize(string path) => GetFileSizeResult;
}