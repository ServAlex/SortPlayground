
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

var sw = Stopwatch.StartNew();

var generator = new FullGeneratorBenchmark();
//generator.GenerateFileSingleThreadedLineByLine();
Console.WriteLine(sw.ElapsedMilliseconds);
sw.Restart();
generator.GenerateFileSingleThreadedBatched();
Console.WriteLine(sw.ElapsedMilliseconds);
Console.WriteLine("file generated");


var reader = new ReadBenchmark();

sw.Restart();
reader.ReadFullFile("test.txt");
Console.WriteLine($"read in {sw.ElapsedMilliseconds} ms");

sw.Restart();
reader.ReadBlocked("test.txt");
Console.WriteLine($"read blocked in {sw.ElapsedMilliseconds} ms");

sw.Restart();
reader.ReadBlockedSequentialLargeBuf("test.txt");
Console.WriteLine($"read blocked large buf in {sw.ElapsedMilliseconds} ms");

sw.Restart();
await reader.ReadToChannel("test.txt");
Console.WriteLine($"read to channel in {sw.ElapsedMilliseconds} ms");

sw.Restart();
await reader.ReadToChannelSync("test.txt");
Console.WriteLine($"read to channel in {sw.ElapsedMilliseconds} ms");


var gcMemoryInfo = GC.GetGCMemoryInfo();
var installedMemoryKb = gcMemoryInfo.TotalAvailableMemoryBytes / 1024;
var usedMemoryKb = GC.GetTotalMemory(true) / 1024;
var availableMemoryKb = installedMemoryKb - usedMemoryKb;

Console.WriteLine($"used memory: {usedMemoryKb} KB, available memory: {availableMemoryKb} KB");
