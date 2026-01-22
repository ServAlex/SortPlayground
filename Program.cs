
using BenchmarkDotNet.Running;
using FileGenerator;

var summary = BenchmarkRunner.Run<GenerationBenchmark>();


