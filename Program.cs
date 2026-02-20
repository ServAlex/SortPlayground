using LargeFileSort.Configurations;
using LargeFileSort.FileDeletion;
using LargeFileSort.FileGeneration;
using LargeFileSort.FileSorting;
using LargeFileSort.FileSorting.ChunkInputFile;
using LargeFileSort.FileSorting.MergeChunks;
using LargeFileSort.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddCommandLine(args, OptionsHelper.GetSwitchMappings());
//Console.WriteLine(builder.Configuration.GetDebugView());

builder.Services.AddTransient<FileGenerator>();
builder.Services.AddTransient<FileSorter>();
builder.Services.AddTransient<FileChunker>();
builder.Services.AddTransient<SortedFilesMerger>();
builder.Services.AddTransient<LeftoversRemover>();
builder.Services.AddTransient<LiveProgressLogger>();

builder.Services.AddOptions<FileGenerationOptions>()
	.Bind(builder.Configuration.GetSection(nameof(FileGenerationOptions)))
	.ValidateDataAnnotations();
builder.Services.AddOptions<GeneralOptions>()
	.Bind(builder.Configuration.GetSection(nameof(GeneralOptions)))
	.ValidateDataAnnotations();
builder.Services.AddOptions<SortOptions>()
	.Bind(builder.Configuration.GetSection(nameof(SortOptions)))
	.ValidateDataAnnotations();

using var host = builder.Build();

try
{
	OptionsHelper.ValidateConfiguration(host.Services);

	host.Services.GetRequiredService<FileGenerator>().GenerateFile();
	host.Services.GetRequiredService<FileSorter>().SortFile();
	host.Services.GetRequiredService<LeftoversRemover>().Remove();
}
catch (Exception e)
{
	switch (e)
	{
		case InvalidConfigurationException:
			Console.Error.WriteLine(e.Message);
			Console.WriteLine(OptionsHelper.GetHelpText());
			return 1;

		case FileNotFoundException:
			Console.Error.WriteLine(e.Message);
			return 1;

		default:
			Console.Error.WriteLine(e);
			return 1;
	}
}

return 0;
