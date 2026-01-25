
using BenchmarkDotNet.Running;
using FileGenerator;
using FileGenerator.FixedLengthGenerators;
using FileGenerator.Generators;

//var summary = BenchmarkRunner.Run<GenerationBenchmark>();
var summary = BenchmarkRunner.Run<GivenLengthLineGeneratorBenchmark>();


