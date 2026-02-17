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

builder.Services.AddTransient<FileGenerator>();
builder.Services.AddTransient<LargeFileSorter>();
builder.Services.AddTransient<FileChunker>();
builder.Services.AddTransient<SortedFilesMergerChanneling>();
builder.Services.AddTransient<FileProgressLogger>();

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
