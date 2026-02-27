using LargeFileSort.Common;
using LargeFileSort.Configurations;
using LargeFileSort.FileDeletion;
using LargeFileSort.FileGeneration;
using LargeFileSort.FileSorting.ChunkInputFile;
using LargeFileSort.FileSorting.MergeChunks;
using LargeFileSort.Infrastructure;
using LargeFileSort.Logging;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LargeFileSort.Tests.Component;

public class FileGeneratorTests
{
	[Fact]
	public void GenerateFile_ShouldThrowInsufficientDiskSpaceException_WhenDiskSpaceIsInsufficient()
	{
		var services = new ServiceCollection();
		var fileSystemMock = Substitute.For<IFileSystem>();
		fileSystemMock.HasEnoughFreeSpace(Arg.Any<string>(), Arg.Any<long>()).Returns(false);

		services.AddSingleton(fileSystemMock);
		
		// Register real services
		services.AddSingleton<FileGenerator>();
		//services.AddSingleton<FileSorter>();
		services.AddSingleton<FileChunker>();
		services.AddSingleton<SortedFilesMerger>();
		services.AddSingleton<LeftoversRemover>();
		services.AddSingleton<LiveProgressLogger>();

		// Add configuration if needed
		services.Configure<FileGenerationOptions>(options =>
		{
			options.Enabled = true;
			options.Reuse = true;
			options.FileSizeGb = 10;
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
		var generator = provider.GetRequiredService<FileGenerator>();

		Assert.Throws<InsufficientFreeDiskException>(() =>
		{
			generator.GenerateFile();
		});
	}
}