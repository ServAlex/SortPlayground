using LargeFileSort.Common;
using LargeFileSort.Configurations;
using LargeFileSort.FileDeletion;
using LargeFileSort.FileSorting;
using LargeFileSort.FileSorting.ChunkInputFile;
using LargeFileSort.FileSorting.MergeChunks;
using LargeFileSort.Infrastructure;
using LargeFileSort.Logging;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LargeFileSort.Tests.Component;

public class FileSorterTests
{
	[Fact]
	public void SortFile_ShouldThrowInsufficientDiskSpaceException_WhenDiskSpaceIsInsufficient()
	{
		var services = new ServiceCollection();
		
		var fileSystemMock = Substitute.For<IFileSystem>();
		fileSystemMock.HasEnoughFreeSpace(Arg.Any<string>(), Arg.Any<long>()).Returns(false);
		fileSystemMock.FileExists(Arg.Any<string>()).Returns(true);
		fileSystemMock.GetFileSize(Arg.Any<string>()).Returns(100);
		
		services.AddSingleton(fileSystemMock);

		// Register real services
		//services.AddSingleton<FileGenerator>();
		services.AddSingleton<FileSorter>();
		services.AddSingleton<FileChunker>();
		services.AddSingleton<SortedFilesMerger>();
		services.AddSingleton<LeftoversRemover>();
		services.AddSingleton<LiveProgressLogger>();

		services.Configure<SortOptions>(options =>
		{
			options.Enabled = true;
			options.ReuseChunks = false;
			options.IntermediateFileSizeMaxMb = 100;
			options.BaseChunkSizeMb = 10;
			options.SortWorkerCount = 1;
			options.MergeWorkerCount = 1;
			options.MergeToFileWorkerCount = 1;
			options.BufferSizeMb = 1;
			options.QueueLength = 1;
		});

		services.Configure<GeneralOptions>(options =>
		{
			options.FilesLocation = "";
			options.UnsortedFileName = "unsorted.txt";
			options.SortedFileName = "sorted.txt";
			options.ChunksDirectoryBaseName = "chunks";
			options.DeleteAllCreatedFiles = true;
			options.KeepChunks = false;
			options.MemoryBudgetGb = 10;
		});

		var provider = services.BuildServiceProvider();
		var sorter = provider.GetRequiredService<FileSorter>();
		Assert.Throws<InsufficientFreeDiskException>(() => { sorter.SortFile(); });
	}
}