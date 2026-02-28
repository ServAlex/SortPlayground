using LargeFileSort;
using LargeFileSort.Common;
using LargeFileSort.Configurations;
using LargeFileSort.FileDeletion;
using LargeFileSort.FileGeneration;
using LargeFileSort.FileSorting;
using LargeFileSort.FileSorting.ChunkInputFile;
using LargeFileSort.FileSorting.MergeChunks;
using LargeFileSort.Infrastructure;
using LargeFileSort.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using var host = CreateAppBuilder(args).Build();

try
{
	OptionsHelper.ValidateConfiguration(host.Services);
	host.Services.GetRequiredService<ApplicationRunner>().Run();
}
catch (InvalidConfigurationException e)
{
	Console.Error.WriteLine(e.Message);
	Console.WriteLine(OptionsHelper.GetHelpText());
	return 1;
}
catch (FileNotFoundException e)
{
	Console.Error.WriteLine(e.Message);
	return 1;
}
catch (InsufficientFreeMemoryException e)
{
	Console.Error.WriteLine($"{e.Message}");
	Console.Error.WriteLine("you may reduce --memoryBudgetGb and maybe --chunkFileSizeMb in options.");
	return 1;
}
catch (InsufficientFreeDiskException e)
{
	Console.Error.WriteLine(e.Message);
	Console.Error.WriteLine("you may reduce --sizeGb in options - generate smaller input file.");
	return 1;
}
catch (Exception e)
{
	Console.Error.WriteLine(e);
	return 1;
}

return 0;

public abstract partial class Program
{
	public static HostApplicationBuilder CreateAppBuilder(string[] strings)
	{
		var builder = Host.CreateApplicationBuilder(strings);
		builder.Configuration.AddCommandLine(strings, OptionsHelper.GetSwitchMappings());
		//Console.WriteLine(builder.Configuration.GetDebugView());

		builder.Services.AddTransient<FileGenerator>();
		builder.Services.AddTransient<FileSorter>();
		builder.Services.AddTransient<FileChunker>();
		builder.Services.AddTransient<SortedFilesMerger>();
		builder.Services.AddTransient<LeftoversRemover>();
		builder.Services.AddTransient<LiveProgressLogger>();
		builder.Services.AddTransient<IFileSystem, FileSystem>();
		builder.Services.AddSingleton<ApplicationRunner>();

		builder.Services.AddOptions<FileGenerationOptions>()
			.Bind(builder.Configuration.GetSection(nameof(FileGenerationOptions)))
			.ValidateDataAnnotations();
		builder.Services.AddOptions<GeneralOptions>()
			.Bind(builder.Configuration.GetSection(nameof(GeneralOptions)))
			.ValidateDataAnnotations();
		builder.Services.AddOptions<SortOptions>()
			.Bind(builder.Configuration.GetSection(nameof(SortOptions)))
			.ValidateDataAnnotations();
		return builder;
	}
}