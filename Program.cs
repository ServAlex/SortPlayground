
using System;
using System.Diagnostics;
using BenchmarkDotNet.Running;
using FileGenerator;
using FileGenerator.FileSorter;
using FileGenerator.FileWriterBenchmark;
using FileGenerator.FixedLengthGenerators;
using FileGenerator.FullGeneratorBenchmark;
using FileGenerator.Generators;

//var summary = BenchmarkRunner.Run<GenerationBenchmark>();
//var summary = BenchmarkRunner.Run<GivenLengthLineGeneratorBenchmark>();
// var summary = BenchmarkRunner.Run<ChunkFileWriterBenchmark>();
//var summary = BenchmarkRunner.Run<MultiGbFileWriterBenchmark>();
//var summary = BenchmarkRunner.Run<FullGeneratorBenchmark>();

//Console.WriteLine(summary.Table.ToString());

var fileSizeMb = 1024 * 20;
var generateNewFile = false;

var bufferSizeB = 1024 * 1024;
var wrokerCount = 4; //Environment.ProcessorCount - 2;
var queueLength = 6;
var chunkSizeB = 63 * 1024 * 1024;
var mergeMaxStoredSizeMb = 2 * 1000;

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

var sorter = new LargeFileSorter(bufferSizeB, wrokerCount, queueLength, chunkSizeB, lineMaxLength:113, fileMaxLength:mergeMaxStoredSizeMb * 1024 * 1024);

//Console.WriteLine(sorter.DataLengthToRank(500 * (1 << 20), 2 * 1000 * (1 << 20)));


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
