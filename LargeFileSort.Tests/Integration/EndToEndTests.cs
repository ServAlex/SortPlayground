using LargeFileSort.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LargeFileSort.Tests.Integration;

public class EndToEndTests : IDisposable
{
	private readonly string _tempDir;
	public EndToEndTests()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), "LargeFileSortE2ETest");
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, true);
		}
		Directory.CreateDirectory(_tempDir);
	}
	
	[Fact]
	[Trait("Category", "Integration")]	
	public void Application_ShouldOutputSortedFile_WhenRun()
	{
		const string outputFileName = "sorted.txt";

		var builder = Program.CreateAppBuilder([]);

		builder.Configuration.Sources.Clear();
		builder.Configuration.AddInMemoryCollection(
			new Dictionary<string, string?>
			{
				["FileGenerationOptions:Enabled"] = "true",
				["FileGenerationOptions:FileSizeGb"] = "1",

				["SortOptions:Enabled"] = "true",
				["SortOptions:ReuseChunks"] = "false",
				["SortOptions:IntermediateFileSizeMaxMb"] = "128",
				["SortOptions:BaseChunkSizeMb"] = "16",
				["SortOptions:QueueLength"] = "4",
				["SortOptions:SortWorkerCount"] = "4",
				["SortOptions:MergeWorkerCount"] = "2",
				["SortOptions:MergeToFileWorkerCount"] = "1",
				["SortOptions:BufferSizeMb"] = "1",

				["GeneralOptions:DeleteAllCreatedFiles"] = "false",
				["GeneralOptions:UnsortedFileName"] = "unsorted.txt",
				["GeneralOptions:SortedFileName"] = outputFileName,
				["GeneralOptions:FilesLocation"] = _tempDir,
				["GeneralOptions:ChunksDirectoryBaseName"] = "Chunks",
				["GeneralOptions:MemoryBudgetGb"] = "4",
				["GeneralOptions:KeepChunks"] = "true",
			});

		var host = builder.Build();

		OptionsHelper.ValidateConfiguration(host.Services);
		host.Services.GetRequiredService<ApplicationRunner>().Run();

		var lines = File.ReadAllLines(Path.Combine(_tempDir, outputFileName));

		var lastLine = ParseLine(lines[0]);
		for (var i = 1; i < lines.Length; i++)
		{
			var newLine = ParseLine(lines[i]);
			Assert.True(Compare(lastLine, newLine) <= 0);
			lastLine = newLine;
		}
	}

	private record Line(string FullLine, int Number, int TextOffset, int TextLength);
	
	private static int Compare(Line a, Line b)
	{
		var comparison = string.CompareOrdinal(
			a.FullLine, a.TextOffset, 
			b.FullLine, b.TextOffset, 
			Math.Min(a.TextLength, b.TextLength));
		
		if (comparison != 0) 
			return comparison;
		
		if(a.TextLength != b.TextLength)
			return a.TextLength - b.TextLength;
		
		return a.Number.CompareTo(b.Number);
	}

	private static Line ParseLine(string line)
	{
		var data = line.AsSpan();
		var i = 0;
		var number = 0;
		while (data[i] != '.' && i < data.Length)
			number = number * 10 + (data[i++] - '0');

		var textOffset = i + 1;
		var textLength = line.Length - (i + 1);	
		return new Line(line, number, textOffset, textLength);
	}

	public void Dispose()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, true);
		}
	}
}