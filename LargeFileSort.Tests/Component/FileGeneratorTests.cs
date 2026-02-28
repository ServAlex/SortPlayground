using LargeFileSort.Common;
using LargeFileSort.FileDeletion;
using LargeFileSort.FileGeneration;
using LargeFileSort.FileSorting.ChunkInputFile;
using LargeFileSort.FileSorting.MergeChunks;
using LargeFileSort.Infrastructure;
using LargeFileSort.Logging;
using LargeFileSort.Tests.Helpers;
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
		
		services.AddSingleton<FileGenerator>();
		services.AddSingleton<FileChunker>();
		services.AddSingleton<SortedFilesMerger>();
		services.AddSingleton<LeftoversRemover>();
		services.AddSingleton<LiveProgressLogger>();
		
		services.AddSingleton(TestOptionsFactory.FileGeneration(o => o.Enabled = true));
		services.AddSingleton(TestOptionsFactory.General(""));

		var provider = services.BuildServiceProvider();
		var generator = provider.GetRequiredService<FileGenerator>();

		Assert.Throws<InsufficientFreeDiskException>(() =>
		{
			generator.GenerateFile();
		});
	}
}