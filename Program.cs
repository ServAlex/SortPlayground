
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
var chunkSizeB = 250 * 1024 * 1024;
var mergeMaxStoredSizeMB = 2 * 1000;

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


var sorter = new LargeFileSorter(bufferSizeB, chunkSizeB, wrokerCount, lineMaxLength:113, fileMaxLength:mergeMaxStoredSizeMB * 1024 * 1024);

/*
sw.Restart();
reader.ReadFullFile("test.txt");
Console.WriteLine($"read in {sw.ElapsedMilliseconds} ms");
*/
/*
sw.Restart();
reader.ReadBlocked("test.txt");
Console.WriteLine($"read blocked in {sw.ElapsedMilliseconds} ms");
*/
/*
sw.Restart();
reader.ReadBlockedSequentialLargeBuf("test.txt");
Console.WriteLine($"read blocked large buf in {sw.ElapsedMilliseconds} ms");

sw.Restart();
await reader.ReadToChannel("test.txt");
Console.WriteLine($"read to channel in {sw.ElapsedMilliseconds} ms");

sw.Restart();
await reader.ReadToChannelSync("test.txt");
Console.WriteLine($"sync read to channel in {sw.ElapsedMilliseconds} ms");
*/

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