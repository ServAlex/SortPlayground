using FluentAssertions;
using LargeFileSort.Common;
using LargeFileSort.Domain;
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
	
	[Fact]
	public void FileChunker_ShouldSplitFileIntoSortedChunks()
	{
		// arrange
		var services = new ServiceCollection();
		services.AddSingleton<FileChunker>();
		services.AddSingleton<LiveProgressLogger>();
		
		services.AddSingleton(TestOptionsFactory.General(""));
		services.AddSingleton(TestOptionsFactory.Sort(o =>
		{
			o.Enabled = true;
			o.ReadChunkSize = DataSize.FromBytes(50);
			o.ChunkFileSizeMax = DataSize.FromBytes(100);
			o.BufferSize = DataSize.FromBytes(10);
			o.QueueLength = 1;
			o.SortWorkerCount = 1;
			o.MergeWorkerCount = 1;
			o.MergeToFileWorkerCount = 1;
		}));
		
		var memoryStream = new MemoryStream();
		var inputLines = 0;
		var writer = new StreamWriter(memoryStream, leaveOpen: true);
		for(var i = 0; i < 100; i++)
		{
			writer.WriteLine("10. apple");
			writer.WriteLine("5. apple");
			writer.WriteLine("2. banana");
			writer.WriteLine("12. banana");
			inputLines += 4;
		}
		writer.Flush();
		writer.Close();
		writer.Dispose();
		memoryStream.Position = 0;

		var outputFiles = new Dictionary<string, MemoryStream>();
		
		var fileSystemMock = Substitute.For<IFileSystem>();
		fileSystemMock.HasEnoughFreeSpace(Arg.Any<string>(), Arg.Any<long>()).Returns(true);
		fileSystemMock.GetFileWriter(Arg.Any<string>(), Arg.Any<int>()).Returns(q =>
		{
			var fileName = q.Arg<string>();
			if(outputFiles.ContainsKey(fileName))
				throw new Exception("File already exists");
			
			return new StreamWriter(outputFiles[fileName] = new MemoryStream(), leaveOpen: true);
		});
		fileSystemMock
			.GetFileReader(Arg.Any<string>(), Arg.Any<int>())
			.Returns(new StreamReader(memoryStream));
		fileSystemMock.FileExists(Arg.Any<string>()).Returns(true);
		fileSystemMock.GetFileSize(Arg.Any<string>()).Returns(100);

		services.AddSingleton(fileSystemMock);
		
		var provider = services.BuildServiceProvider();
		var chunker = provider.GetRequiredService<FileChunker>();
		
		// act
		chunker.SplitFileIntoSortedChunkFiles();
		
		// assert
		var outputLines = 0;
		foreach (var (_, file) in outputFiles)
		{
			file.Position = 0;
			using var reader = new StreamReader(file);
			var lines = reader.ReadToEnd().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
			lines.Should().BeInAscendingOrder(l => l, new FullLineComparer());
			outputLines += lines.Length;
		}
		
		outputLines.Should().Be(inputLines);
	}
		
	private class FullLineComparer : IComparer<string>
	{
		public int Compare(string? a, string? b)
		{
			if(a == null) return -1;
			if(b == null) return 1;
				
			var aNumber = int.Parse(a.Substring(0, a.IndexOf('.')));
			var aText = a.Substring(a.IndexOf('.') + 1);
			
			var bNumber = int.Parse(b.Substring(0, b.IndexOf('.')));
			var bText = b.Substring(b.IndexOf('.') + 1);
			
			var comparison = string.CompareOrdinal(aText, bText);
			if (comparison != 0) 
				return comparison;
		
			return aNumber.CompareTo(bNumber);
		}
	}

	[Fact]
	public void FileMerger_ShouldMergeSortedFilesToSingleSortedFile()
	{
		// arrange
		var services = new ServiceCollection();
		services.AddSingleton<SortedFilesMerger>();
		services.AddSingleton<LiveProgressLogger>();
		
		services.AddSingleton(TestOptionsFactory.General(""));
		services.AddSingleton(TestOptionsFactory.Sort(o =>
		{
			o.Enabled = true;
			o.BufferSize = DataSize.FromBytes(10);
		}));

		var items = new List<(int number, string text)>();

		for (var i = 0; i < 30; i++)
			items.AddRange([
				(10, "apple"),
				(5, "apple"),
				(1, "fig"),
				(11, "fig"),
				(2, "banana"),
				(12, "banana"),
				(1, "cherry")
			]);

		var memoryStreams = items
			.Chunk(20)
			.Select(g =>
		{
			g.Sort(new TupleComparer());
			var ms = new MemoryStream();
			using var writer = new StreamWriter(ms, leaveOpen: true);
			foreach (var (number, text) in g)
			{
				writer.WriteLine($"{number}. {text}");
			}
			ms.Position = 0;
			return ms;
		}).ToArray();

		foreach (var ms in memoryStreams)
		{
			ms.Position = 0;
		}
		
		var fileInfos = 
			memoryStreams.Select((_, i) => new FileInfo($"file{i}.txt")).ToArray();
		
		var outputMemoryStream = new MemoryStream();
		
		var readerIndex = 0;
		var fileSystemMock = Substitute.For<IFileSystem>();
		fileSystemMock.HasEnoughFreeSpace(Arg.Any<string>(), Arg.Any<long>()).Returns(true);
		fileSystemMock.GetFileWriter(Arg.Any<string>(), Arg.Any<int>())
			.Returns(_ => new StreamWriter(outputMemoryStream, leaveOpen: true) );
		fileSystemMock.GetFiles(Arg.Any<string>()).Returns(fileInfos);
		fileSystemMock
			.GetFileReader(Arg.Any<string>(), Arg.Any<int>())
			.Returns(_ => new StreamReader(memoryStreams[readerIndex++]));
		fileSystemMock.FileExists(Arg.Any<string>()).Returns(true);
		fileSystemMock.GetFileSize(Arg.Any<string>()).Returns(100);
		fileSystemMock.GetFileSize(Arg.Any<FileInfo>()).Returns(100);
		fileSystemMock.DirectoryExists(Arg.Any<string>()).Returns(true);

		services.AddSingleton(fileSystemMock);
		
		var provider = services.BuildServiceProvider();
		var merger = provider.GetRequiredService<SortedFilesMerger>();
		
		// act
		merger.MergeSortedFiles();
		
		// assert
		outputMemoryStream.Position = 0;
		using var reader = new StreamReader(outputMemoryStream);
		var lines = reader.ReadToEnd().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
		lines.Should().BeInAscendingOrder(l => l, new FullLineComparer());
		
		var expectedLines = items.Select(i => $"{i.number}. {i.text}").ToArray();
		lines.Should().BeEquivalentTo(expectedLines);
	}
	
	private class TupleComparer : IComparer<(int number, string text)>
	{
		public int Compare((int number, string text) a, (int number, string text) b)
		{
			var comparison = string.CompareOrdinal(a.text, b.text);
			if (comparison != 0) 
				return comparison;
		
			return a.number.CompareTo(b.number);
		}
	}
}