using LargeFileSort.Configurations;
using Microsoft.Extensions.Options;

namespace LargeFileSort.Tests.Helpers;

public static class TestOptionsFactory
{
	public static IOptions<FileGenerationOptions> FileGeneration(Action<FileGenerationOptions>? configure = null)
	{
		var options = new FileGenerationOptions
		{
			Enabled = false,
			Reuse = false,
			FileSizeGb = 1
		};

		configure?.Invoke(options);
		return Options.Create(options);
	}

	public static IOptions<SortOptions> Sort(Action<SortOptions>? configure = null)
	{
		var options = new SortOptions
		{
			Enabled = false,
			ReuseChunks = false,
			IntermediateFileSizeMaxMb = 128,
			BaseChunkSizeMb = 16,
			QueueLength = 4,
			SortWorkerCount = 4,
			MergeWorkerCount = 2,
			MergeToFileWorkerCount = 1,
			BufferSizeMb = 1
		};

		configure?.Invoke(options);
		return Options.Create(options);
	}

	public static IOptions<GeneralOptions> General(string tempDir, Action<GeneralOptions>? configure = null)
	{
		var options = new GeneralOptions
		{
			DeleteAllCreatedFiles = false,
			UnsortedFileName = "unsorted.txt",
			SortedFileName = "sorted.txt",
			FilesLocation = tempDir,
			ChunksDirectoryBaseName = "Chunks",
			MemoryBudgetGb = 4,
			KeepChunks = false
		};

		configure?.Invoke(options);
		return Options.Create(options);
	}
}