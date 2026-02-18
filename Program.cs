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
	{ "--generate", $"{nameof(FileGenerationOptions)}:{nameof(FileGenerationOptions.Enabled)}" },
	{ "--reuse", $"{nameof(FileGenerationOptions)}:{nameof(FileGenerationOptions.Reuse)}" },
	{ "--size", $"{nameof(FileGenerationOptions)}:{nameof(FileGenerationOptions.FileSizeGb)}" },
	
	{ "--sort", $"{nameof(SortOptions)}:{nameof(SortOptions.Enabled)}" },
	{ "--chunkFileSizeMb", $"{nameof(SortOptions)}:{nameof(SortOptions.IntermediateFileSizeMaxMb)}" },
	{ "--baseChunkSizeMb", $"{nameof(SortOptions)}:{nameof(SortOptions.BaseChunkSizeMb)}" },
	{ "--memoryBudgetGb", $"{nameof(SortOptions)}:{nameof(LargeFileSorterOptions.MemoryBudgetGb)}" },
	
	{ "--path", $"{nameof(PathOptions)}:{nameof(PathOptions.FilesLocation)}" },
	{ "--delete", $"{nameof(PathOptions)}:{nameof(PathOptions.DeleteAllCreatedFiles)}" },
};
var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings()
{
	DisableDefaults = true,
	ContentRootPath = Directory.GetCurrentDirectory(),
	Args = args
});
//var builder = Host.CreateApplicationBuilder(args);
//Console.WriteLine(builder.Configuration.GetDebugView());

builder.Configuration.AddJsonFile("appsettings.json", optional:false, reloadOnChange:false);
builder.Configuration.AddCommandLine(args, switchMappings);

// builder.Configuration.AddInMemoryCollection(new List<KeyValuePair<string, string?>>
// {
// 	new( $"{nameof(SortOptions)}:{nameof(LargeFileSorterOptions.MemoryBudgetGb)}", "6")
// });

Console.WriteLine(builder.Configuration.GetDebugView());
//Console.WriteLine(string.Join(',', args));

builder.Services.AddTransient<FileGenerator>();
builder.Services.AddTransient<LargeFileSorter>();
builder.Services.AddTransient<FileChunker>();
builder.Services.AddTransient<SortedFilesMergerChanneling>();
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

OptionsValidator.Validate(host.Services);

var generator = host.Services.GetRequiredService<FileGenerator>();
generator.GenerateFileSingleThreadedBatched();

var sorter = host.Services.GetRequiredService<LargeFileSorter>();
await sorter.SortFile();
