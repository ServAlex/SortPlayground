using System.Diagnostics;
using FileGenerator.FileSorter;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
	.AddJsonFile("appsettings.json")
	.Build();

var options = config.GetSection(nameof(LargeFileSorterOptions)).Get<LargeFileSorterOptions>() 
              ?? throw new InvalidOperationException("Config not found");
options.SortWorkerCount = Environment.ProcessorCount - 2 - options.MergeWorkerCount;

const bool generateNewFile = false;
const int fileSizeMb = 1024 * 20;
var sw = Stopwatch.StartNew();

if (generateNewFile)
{
	var generator = new FileGenerator.FileGeneration.FileGenerator();
	generator.GenerateFileSingleThreadedBatched(fileSizeMb);
	Console.WriteLine($"file generated in {sw.ElapsedMilliseconds} ms");
}
else
{
	Console.WriteLine("using old file");
}

var sorter = new LargeFileSorter(options, new FileProgressLogger()); 

sw.Restart();
await sorter.SortFile("test.txt");
Console.WriteLine($"direct sync read to channel in {sw.ElapsedMilliseconds} ms");

