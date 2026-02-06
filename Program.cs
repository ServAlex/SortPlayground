using System.Diagnostics;
using FileGenerator.FileSorter;
using FileGenerator.FullGeneratorBenchmark;
using Microsoft.Extensions.Configuration;

//var summary = BenchmarkRunner.Run<GenerationBenchmark>();
//var summary = BenchmarkRunner.Run<GivenLengthLineGeneratorBenchmark>();
//var summary = BenchmarkRunner.Run<ChunkFileWriterBenchmark>();
//var summary = BenchmarkRunner.Run<MultiGbFileWriterBenchmark>();
//var summary = BenchmarkRunner.Run<FullGeneratorBenchmark>();

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


//Console.WriteLine($"max rank {sorter.MaxRank((1 << 30) - 1 + (1 << 30), 63 * (1 << 20))}");
