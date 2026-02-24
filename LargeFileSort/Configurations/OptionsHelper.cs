using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LargeFileSort.Configurations;

public static class OptionsHelper
{
	public static void ValidateConfiguration(IServiceProvider serviceProvider)
	{
		var errors = new List<string>();

		var generationOptions = TryInstantiate<FileGenerationOptions>(serviceProvider, errors);
		var pathOptions = TryInstantiate<GeneralOptions>(serviceProvider, errors);
		var sortOptions = TryInstantiate<SortOptions>(serviceProvider, errors);

		if (errors.Count != 0)
		{
			throw new InvalidConfigurationException(string.Join(Environment.NewLine, errors));
		}

		if (generationOptions is { Enabled: false } 
		    && sortOptions is { Enabled: false } 
		    && pathOptions is { DeleteAllCreatedFiles: false })
		{
			throw new InvalidConfigurationException(
				"No action was specified. Use at least one of '--generate true', '--sort true' or '--delete true'");
		}
	}

	public static Dictionary<string, string> GetSwitchMappings()
	{
		return  new Dictionary<string, string>
		{
			{ "--generate", $"{nameof(FileGenerationOptions)}:{nameof(FileGenerationOptions.Enabled)}" },
			{ "--reuseUnsorted", $"{nameof(FileGenerationOptions)}:{nameof(FileGenerationOptions.Reuse)}" },
			{ "--sizeGb", $"{nameof(FileGenerationOptions)}:{nameof(FileGenerationOptions.FileSizeGb)}" },
	
			{ "--sort", $"{nameof(SortOptions)}:{nameof(SortOptions.Enabled)}" },
			{ "--reuseChunks", $"{nameof(SortOptions)}:{nameof(SortOptions.ReuseChunks)}" },
			{ "--chunkFileSizeMb", $"{nameof(SortOptions)}:{nameof(SortOptions.IntermediateFileSizeMaxMb)}" },
			{ "--baseChunkSizeMb", $"{nameof(SortOptions)}:{nameof(SortOptions.BaseChunkSizeMb)}" },
	
			{ "--path", $"{nameof(GeneralOptions)}:{nameof(GeneralOptions.FilesLocation)}" },
			{ "--delete", $"{nameof(GeneralOptions)}:{nameof(GeneralOptions.DeleteAllCreatedFiles)}" },
			{ "--keepChunks", $"{nameof(GeneralOptions)}:{nameof(GeneralOptions.KeepChunks)}" },
			{ "--memoryBudgetGb", $"{nameof(GeneralOptions)}:{nameof(GeneralOptions.MemoryBudgetGb)}" },
		};
	}
	
	public static string GetHelpText()
	{
		var sb = new StringBuilder();
		sb.AppendLine("Options:");
		//var mappings = GetSwitchMappings();
		// foreach (var (key, value) in mappings)
		// {
		// 	sb.AppendLine($"  {key}");
		// }
		sb.AppendLine("");
		sb.AppendLine("  --generate            - bool,	generate the random file, default: false");
		sb.AppendLine("  --reuseUnsorted       - bool,	reuse random file at path if size matches, default: true");
		sb.AppendLine("  --sizeGb              - int,	size of the file to be generated, default: 10");
		sb.AppendLine("");
		sb.AppendLine("  --sort                - bool,	sort unsorted file, default: false");
		sb.AppendLine("  --reuseChunks         - bool,	reuse partially sorted chunks if exist, default: false");
		sb.AppendLine("  --chunkFileSizeMb     - int,	default: 1024");
		sb.AppendLine("  --baseChunkSizeMb     - int,	size of chunk sorted directly, default: 63");
		sb.AppendLine("");
		sb.AppendLine("  --path                - string, default: .");
		sb.AppendLine("  --delete              - bool,	delete all created files, has priority over keepChunks, default: false");
		sb.AppendLine("  --keepChunks          - bool,	keep chunks after run, default: true");
		sb.AppendLine("  --memoryBudgetGb      - int,	default: 16");

		sb.AppendLine();
		sb.AppendLine("Example: dotnet run -c Release --generate true --sizeGb 10 --path ./SortTemp/ --sort true\n");
		return sb.ToString();
	}

	private static T? TryInstantiate<T>(IServiceProvider serviceProvider, List<string> errors) where T : class
	{
		try
		{
			return serviceProvider.GetRequiredService<IOptions<T>>().Value;
		}
		catch (OptionsValidationException e)
		{
			errors.AddRange(e.Message.Split("; "));
			return null;
		}
	}
}