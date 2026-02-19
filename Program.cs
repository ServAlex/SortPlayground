using LargeFileSort.Configurations;
using LargeFileSort.FileDeletion;
using LargeFileSort.FileSorter;
using LargeFileSort.FileGeneration;
using LargeFileSort.FileSorter.ChunkInputFile;
using LargeFileSort.FileSorter.MergeChunks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddCommandLine(args, OptionsHelper.GetSwitchMappings());
//Console.WriteLine(builder.Configuration.GetDebugView());

builder.Services.AddTransient<FileGenerator>();
builder.Services.AddTransient<LargeFileSorter>();
builder.Services.AddTransient<FileChunker>();
builder.Services.AddTransient<SortedFilesMergerChanneling>();
builder.Services.AddTransient<LeftoversRemover>();
builder.Services.AddTransient<FileProgressLogger>();

builder.Services.AddOptions<FileGenerationOptions>()
	.Bind(builder.Configuration.GetSection(nameof(FileGenerationOptions)))
	.ValidateDataAnnotations();
builder.Services.AddOptions<PathOptions>()
	.Bind(builder.Configuration.GetSection(nameof(PathOptions)))
	.ValidateDataAnnotations();
builder.Services.AddOptions<SortOptions>()
	.Bind(builder.Configuration.GetSection(nameof(SortOptions)))
	.ValidateDataAnnotations();

using var host = builder.Build();

OptionsHelper.Validate(host.Services);

var generator = host.Services.GetRequiredService<FileGenerator>();
generator.GenerateFile();

var sorter = host.Services.GetRequiredService<LargeFileSorter>();
await sorter.SortFile();

var leftOversRemover = host.Services.GetRequiredService<LeftoversRemover>();
leftOversRemover.Remove();
