using System.Diagnostics;
using LargeFileSort.Configurations;
using LargeFileSort.FileSorter;
using LargeFileSort.FileGeneration;
using LargeFileSort.FileSorter.ChunkInputFile;
using LargeFileSort.FileSorter.MergeChunks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var switchMappings = new Dictionary<string, string>
{
	{ "-m", $"{nameof(LargeFileSorterOptions)}:{nameof(LargeFileSorterOptions.MemoryBudgetGb)}" },
};

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json", optional:false, reloadOnChange:false);
builder.Configuration.AddCommandLine(args, switchMappings);

builder.Services.AddScoped<FileGenerator>();
builder.Services.AddScoped<LargeFileSorter>();
builder.Services.AddScoped<FileChunker>();
builder.Services.AddScoped<SortedFilesMergerChanneling>();
builder.Services.AddScoped<FileProgressLogger>();

builder.Services.Configure<LargeFileSorterOptions>(builder.Configuration.GetSection(nameof(LargeFileSorterOptions)));
builder.Services.Configure<FileGenerationOptions>(builder.Configuration.GetSection(nameof(FileGenerationOptions)));
builder.Services.Configure<PathOptions>(builder.Configuration.GetSection(nameof(PathOptions)));
// validate, especially sortOptions.IntermediateFileSizeMaxMb <= 2047
builder.Services.Configure<SortOptions>(builder.Configuration.GetSection(nameof(SortOptions)));

using var host = builder.Build();

var generator = host.Services.GetRequiredService<FileGenerator>();
generator.GenerateFileSingleThreadedBatched();

var sorter = host.Services.GetRequiredService<LargeFileSorter>();
await sorter.SortFile();

return;

/*
var config = new ConfigurationBuilder()
	.AddJsonFile("appsettings.json")
	.Build();

var options = config.GetSection(nameof(LargeFileSorterOptions)).Get<LargeFileSorterOptions>() 
              ?? throw new InvalidOperationException("Config not found");
options.SortWorkerCount = Environment.ProcessorCount - 2 - options.MergeWorkerCount;

const bool generateNewFile = false;
const int sizeGb = 10;
const int fileSizeMb = 1024 * sizeGb;
var sw = Stopwatch.StartNew();

var inputFile = $"test{sizeGb}.txt";

if (generateNewFile)
{
	var generator = new LargeFileSort.FileGeneration.LargeFileSort();
	generator.GenerateFileSingleThreadedBatched(inputFile, fileSizeMb);
	Console.WriteLine($"file generated in {sw.ElapsedMilliseconds} ms");
}
else
{
	Console.WriteLine("using old file");
}

var sorter = new LargeFileSorter(options, new FileProgressLogger{MemoryBudgetMb = options.MemoryBudgetGb*1024}); 

sw.Restart();
//"sorted_channeling.txt"
await sorter.SortFile(inputFile, $"Chunks{sizeGb}", "sorted.txt");
Console.WriteLine($"direct sync read to channel in {sw.ElapsedMilliseconds} ms");

*/