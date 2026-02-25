using LargeFileSort.Infrastructure;

namespace LargeFileSort.Tests.Fakes;

public class FakeFileSystem : IFileSystem
{
	public bool HasEnoughSpaceResult { get; set; } = true;

	public bool HasEnoughFreeSpace(string path, long requiredBytes)
		=> HasEnoughSpaceResult;
}