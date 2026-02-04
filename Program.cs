using System.Diagnostics;
using System.Reflection;
using FileGenerator.FileSorter;
using FileGenerator.FullGeneratorBenchmark;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

//var summary = BenchmarkRunner.Run<GenerationBenchmark>();
//var summary = BenchmarkRunner.Run<GivenLengthLineGeneratorBenchmark>();
//var summary = BenchmarkRunner.Run<ChunkFileWriterBenchmark>();
//var summary = BenchmarkRunner.Run<MultiGbFileWriterBenchmark>();
//var summary = BenchmarkRunner.Run<FullGeneratorBenchmark>();


var asm = Assembly.GetExecutingAssembly();
var config = new ConfigurationBuilder()
	.AddJsonFile(new EmbeddedFileProvider(asm), "config.json", false, false)
	//.AddJsonFile("config.json")
	.Build();

LargeFileSorterOptions options = new();
config.GetSection(nameof(LargeFileSorterOptions)).Bind(options);


Console.WriteLine($"sort options: {options}");

var fileSizeMb = 1024 * 20;
var generateNewFile = false;

options.BufferSize = 1024 * 1024;
options.SortWorkerCount = Environment.ProcessorCount - 2 - options.MergeWorkerCount;
options.ChunkSize = 63 * 1024 * 1024;
options.FileMaxLengthMb = 2 * 1000;
options.MemoryBudgetMb = 16 * 1024;

var sw = Stopwatch.StartNew();

if (generateNewFile)
{
	var generator = new FullGeneratorBenchmark();
	generator.GenerateFileSingleThreadedBatched(fileSizeMb);
	Console.WriteLine($"file generated in {sw.ElapsedMilliseconds} ms");
}
else
{
	Console.WriteLine("using old file");
}

var sorter = new LargeFileSorter(options); 

sw.Restart();
await sorter.SortFile("test.txt");
Console.WriteLine($"direct sync read to channel in {sw.ElapsedMilliseconds} ms");
