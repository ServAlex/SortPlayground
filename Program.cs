
using BenchmarkDotNet.Running;
using FileGenerator;
using FileGenerator.FileWriterBenchmark;
using FileGenerator.FixedLengthGenerators;
using FileGenerator.Generators;

//var summary = BenchmarkRunner.Run<GenerationBenchmark>();
//var summary = BenchmarkRunner.Run<GivenLengthLineGeneratorBenchmark>();
var summary = BenchmarkRunner.Run<ChunkFileWriterBenchmark>();
//var summary = BenchmarkRunner.Run<MultiGbFileWriterBenchmark>();


