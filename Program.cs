
using System;
using System.Diagnostics;
using BenchmarkDotNet.Running;
using FileGenerator;
using FileGenerator.FileWriterBenchmark;
using FileGenerator.FixedLengthGenerators;
using FileGenerator.FullGeneratorBenchmark;
using FileGenerator.Generators;
using FileGenerator.ReadingBenchmark;

//var summary = BenchmarkRunner.Run<GenerationBenchmark>();
//var summary = BenchmarkRunner.Run<GivenLengthLineGeneratorBenchmark>();
// var summary = BenchmarkRunner.Run<ChunkFileWriterBenchmark>();
//var summary = BenchmarkRunner.Run<MultiGbFileWriterBenchmark>();
//var summary = BenchmarkRunner.Run<FullGeneratorBenchmark>();

//Console.WriteLine(summary.Table.ToString());

var fileSizeMb = 1024 * 10;
var generateNewFile = false;

var bufferSizeB = 1024 * 1024;
var wrokerCount = 4; //Environment.ProcessorCount;
var chunkSizeB = 256 * 1024 * 1024;

var sw = Stopwatch.StartNew();

if (generateNewFile)
{
	var generator = new FullGeneratorBenchmark();
	//generator.GenerateFileSingleThreadedLineByLine();
	//Console.WriteLine(sw.ElapsedMilliseconds);
	//sw.Restart();
	generator.GenerateFileSingleThreadedBatched(fileSizeMb);
	Console.WriteLine($"file generated in {sw.ElapsedMilliseconds} ms");
}
else
{
	Console.WriteLine("using old file");
}


var reader = new ReadBenchmark(bufferSizeB, chunkSizeB, wrokerCount, lineMaxLength:113);

/*
sw.Restart();
reader.ReadFullFile("test.txt");
Console.WriteLine($"read in {sw.ElapsedMilliseconds} ms");
*/
sw.Restart();
reader.ReadBlocked("test.txt");
Console.WriteLine($"read blocked in {sw.ElapsedMilliseconds} ms");
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
await reader.ReadToChannelSyncNoBuffer("test.txt");
Console.WriteLine($"direct sync read to channel in {sw.ElapsedMilliseconds} ms");

/*
var gcMemoryInfo = GC.GetGCMemoryInfo();
var installedMemoryKb = gcMemoryInfo.TotalAvailableMemoryBytes / 1024;
var usedMemoryKb = GC.GetTotalMemory(true) / 1024;
var availableMemoryKb = installedMemoryKb - usedMemoryKb;

Console.WriteLine("");
Console.WriteLine($"used memory: {usedMemoryKb} KB, available memory: {availableMemoryKb} KB");
*/