using LargeFileSort.Common;
using LargeFileSort.FileDeletion;
using LargeFileSort.FileSorting;
using LargeFileSort.FileSorting.ChunkInputFile;
using LargeFileSort.FileSorting.MergeChunks;
using LargeFileSort.Infrastructure;
using LargeFileSort.Logging;
using LargeFileSort.Tests.Helpers;
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

		services.AddSingleton<FileSorter>();
		services.AddSingleton<FileChunker>();
		services.AddSingleton<SortedFilesMerger>();
		services.AddSingleton<LeftoversRemover>();
		services.AddSingleton<LiveProgressLogger>();
		
		services.AddSingleton(TestOptionsFactory.Sort(o => o.Enabled = true));
		services.AddSingleton(TestOptionsFactory.General(""));

		var provider = services.BuildServiceProvider();
		var sorter = provider.GetRequiredService<FileSorter>();
		Assert.Throws<InsufficientFreeDiskException>(() => { sorter.SortFile(); });
	}
}