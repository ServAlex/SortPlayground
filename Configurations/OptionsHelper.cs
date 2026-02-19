using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LargeFileSort.Configurations;

public static class OptionsHelper
{
	public static bool Validate(IServiceProvider serviceProvider)
	{
		var errors = new List<string>();

		var generationOptions = TryInstantiate<FileGenerationOptions>(serviceProvider, errors);
		var pathOptions = TryInstantiate<PathOptions>(serviceProvider, errors);
		var sortOptions = TryInstantiate<SortOptions>(serviceProvider, errors);

		if (errors.Count != 0)
		{
			Console.WriteLine(string.Join(Environment.NewLine, errors));
			Environment.Exit(1);
		}

		if (generationOptions is { Enabled: false } 
		    && sortOptions is { Enabled: false } 
		    && pathOptions is { DeleteAllCreatedFiles: false })
		{
			Console.WriteLine(
				"No action was specified. Use at least one of '--generate true', '--sort true' or '--delete true'");
			return false;
		}
		
		return true;
	}

	public static Dictionary<string, string> GetSwitchMappings()
	{
		return  new Dictionary<string, string>
		{
			{ "--generate", $"{nameof(FileGenerationOptions)}:{nameof(FileGenerationOptions.Enabled)}" },
			{ "--reuse", $"{nameof(FileGenerationOptions)}:{nameof(FileGenerationOptions.Reuse)}" },
			{ "--sizeGb", $"{nameof(FileGenerationOptions)}:{nameof(FileGenerationOptions.FileSizeGb)}" },
	
			{ "--sort", $"{nameof(SortOptions)}:{nameof(SortOptions.Enabled)}" },
			{ "--reuseChunks", $"{nameof(SortOptions)}:{nameof(SortOptions.ReuseChunks)}" },
			{ "--chunkFileSizeMb", $"{nameof(SortOptions)}:{nameof(SortOptions.IntermediateFileSizeMaxMb)}" },
			{ "--baseChunkSizeMb", $"{nameof(SortOptions)}:{nameof(SortOptions.BaseChunkSizeMb)}" },
			{ "--memoryBudgetGb", $"{nameof(SortOptions)}:{nameof(SortOptions.MemoryBudgetGb)}" },
	
			{ "--path", $"{nameof(PathOptions)}:{nameof(PathOptions.FilesLocation)}" },
			{ "--delete", $"{nameof(PathOptions)}:{nameof(PathOptions.DeleteAllCreatedFiles)}" },
		};
	}
	
	public static string GetHelpText()
	{
		var mappings = GetSwitchMappings();
		var sb = new StringBuilder();
		sb.AppendLine("Options:");
		foreach (var (key, value) in mappings)
		{
			sb.AppendLine($"  {key}");
		}

		sb.AppendLine();
		sb.AppendLine("Example: dotnet run --property:Configuration=Release --generate true --sizeGb 10 --path ./SortWorkDir/ --sort true\n");
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