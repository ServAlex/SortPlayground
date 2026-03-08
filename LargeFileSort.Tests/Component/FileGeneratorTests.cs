using System.Text.RegularExpressions;
using FluentAssertions;
using LargeFileSort.Common;
using LargeFileSort.Domain;
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

public partial class FileGeneratorTests
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
	
	[Fact]
	public void GenerateFile_ShouldGenerateProperlyFormattedFile()
	{
		// arrange
		var services = new ServiceCollection();
		services.AddSingleton<FileGenerator>();
		services.AddSingleton<FileChunker>();
		services.AddSingleton<SortedFilesMerger>();
		services.AddSingleton<LeftoversRemover>();
		services.AddSingleton<LiveProgressLogger>();
		
		services.AddSingleton(TestOptionsFactory.FileGeneration(o =>
		{
			o.Enabled = true;
			o.FileSize = DataSize.Parse("100kb");
		}));
		services.AddSingleton(TestOptionsFactory.General(""));
		
		var memoryStream = new MemoryStream();
		var writer = new StreamWriter(memoryStream, leaveOpen: true);
		
		var fileSystemMock = Substitute.For<IFileSystem>();
		fileSystemMock.HasEnoughFreeSpace(Arg.Any<string>(), Arg.Any<long>()).Returns(true);
		fileSystemMock.GetFileWriter(Arg.Any<string>(), Arg.Any<int>()).Returns(writer);

		services.AddSingleton(fileSystemMock);

		var provider = services.BuildServiceProvider();
		var generator = provider.GetRequiredService<FileGenerator>();
		
		// act
		generator.GenerateFile();
		
		// assert
		memoryStream.Position = 0;
		var lines = new StreamReader(memoryStream)
			.ReadToEnd()
			.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

		lines
			.Should().AllSatisfy(line => line
				.Should().MatchRegex(StringValueRegex()));
	}
	
	[GeneratedRegex(@"^\d+\.(?: [a-zA-Z]+)+$")]
	private static partial Regex StringValueRegex();
}