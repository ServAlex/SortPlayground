using System.Diagnostics;
using FileGenerator.FileSorter;
using FileGenerator.FullGeneratorBenchmark;

//var summary = BenchmarkRunner.Run<GenerationBenchmark>();
//var summary = BenchmarkRunner.Run<GivenLengthLineGeneratorBenchmark>();
//var summary = BenchmarkRunner.Run<ChunkFileWriterBenchmark>();
//var summary = BenchmarkRunner.Run<MultiGbFileWriterBenchmark>();
//var summary = BenchmarkRunner.Run<FullGeneratorBenchmark>();


var fileSizeMb = 1024 * 20;
var generateNewFile = false;

var bufferSizeB = 1024 * 1024;
var mergeWorkerCount = 2;
var sortWorkerCount = Environment.ProcessorCount - 2 - mergeWorkerCount;
var queueLength = 6;
var chunkSizeB = 63 * 1024 * 1024;
var mergeMaxStoredSizeMb = 2 * 1000;
var memoryBudgetMb = 16 * 1024;

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

var sorter = new LargeFileSorter(
	bufferSize: bufferSizeB,
	sortWorkerCount: sortWorkerCount,
	mergeWorkerCount: mergeWorkerCount,
	queueLength: queueLength,
	chunkSize: chunkSizeB,
	fileMaxLength: mergeMaxStoredSizeMb * 1024 * 1024,
	memoryBudgetMb: memoryBudgetMb);


sw.Restart();
await sorter.SortFile("test.txt");
Console.WriteLine($"direct sync read to channel in {sw.ElapsedMilliseconds} ms");

/*
var gcMemoryInfo = GC.GetGCMemoryInfo();
var installedMemoryKb = gcMemoryInfo.TotalAvailableMemoryBytes / 1024;
var usedMemoryKb = GC.GetTotalMemory(true) / 1024;
var availableMemoryKb = installedMemoryKb - usedMemoryKb;

Console.WriteLine("");
Console.WriteLine($"used memory: {usedMemoryKb} KB, available memory: {availableMemoryKb} KB");
*/
