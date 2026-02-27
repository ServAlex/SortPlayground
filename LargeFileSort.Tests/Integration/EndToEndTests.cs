using LargeFileSort.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LargeFileSort.Tests.Integration;

public class EndToEndTests
{
	[Fact]
	public void Application_ShouldOutputSortedFile_WhenRun()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		var outputFileName = "sorted.txt";
		Directory.CreateDirectory(tempDir);
		
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
				["GeneralOptions:FilesLocation"] = tempDir,
				["GeneralOptions:ChunksDirectoryBaseName"] = "Chunks",
				["GeneralOptions:MemoryBudgetGb"] = "4",
				["GeneralOptions:KeepChunks"] = "true",
			});

		var host = builder.Build();
		
		OptionsHelper.ValidateConfiguration(host.Services);
		host.Services.GetRequiredService<ApplicationRunner>().Run();

		var lines = File.ReadAllLines(Path.Combine(tempDir, outputFileName));

		var lastLine = ParseLine(lines[0]);
		for (var i = 1; i < lines.Length; i++)
		{
			var newLine = ParseLine(lines[i]);
			Assert.True(Compare(lastLine, newLine) <= 0);
			lastLine = newLine;
		}

		Directory.Delete(tempDir, true);
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

}