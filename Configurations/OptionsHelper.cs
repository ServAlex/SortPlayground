using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LargeFileSort.Configurations;

public static class OptionsHelper
{
	public static void Validate(IServiceProvider serviceProvider)
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
			Environment.Exit(1);
		}
	}

	public static Dictionary<string, string> GetSwitchMappings()
	{
		return  new Dictionary<string, string>
		{
			{ "--generate", $"{nameof(FileGenerationOptions)}:{nameof(FileGenerationOptions.Enabled)}" },
			{ "--reuse", $"{nameof(FileGenerationOptions)}:{nameof(FileGenerationOptions.Reuse)}" },
			{ "--size", $"{nameof(FileGenerationOptions)}:{nameof(FileGenerationOptions.FileSizeGb)}" },
	
			{ "--sort", $"{nameof(SortOptions)}:{nameof(SortOptions.Enabled)}" },
			{ "--chunkFileSizeMb", $"{nameof(SortOptions)}:{nameof(SortOptions.IntermediateFileSizeMaxMb)}" },
			{ "--baseChunkSizeMb", $"{nameof(SortOptions)}:{nameof(SortOptions.BaseChunkSizeMb)}" },
			{ "--memoryBudgetGb", $"{nameof(SortOptions)}:{nameof(SortOptions.MemoryBudgetGb)}" },
	
			{ "--path", $"{nameof(PathOptions)}:{nameof(PathOptions.FilesLocation)}" },
			{ "--delete", $"{nameof(PathOptions)}:{nameof(PathOptions.DeleteAllCreatedFiles)}" },
		};
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