using System.Diagnostics;
using LargeFileSort.Configurations;
using LargeFileSort.FileSorter.ChunkInputFile;
using LargeFileSort.FileSorter.MergeChunks;
using Microsoft.Extensions.Options;

namespace LargeFileSort.FileSorter;

public class LargeFileSorter
{
	private readonly FileChunker _fileChunker;
	private readonly SortedFilesMergerChanneling _sortedFilesMerger;
	private readonly SortOptions _sortOptions;
	
	public LargeFileSorter(FileChunker fileChunker, SortedFilesMergerChanneling sortedFilesMerger, IOptions<SortOptions> sortOptions)
	{
		_fileChunker = fileChunker;
		_sortedFilesMerger = sortedFilesMerger;
		_sortOptions = sortOptions.Value;
	}

	public async Task SortFile()
	{
		if (!_sortOptions.Enabled)
		{
			Console.WriteLine("Sorting is not enabled in options, skipping");
			return;
		}
		
		var sw = Stopwatch.StartNew();
		var inputFileSize = _fileChunker.ChunkFileAsync();
		var outputFileSizeChanneling = _sortedFilesMerger.MergeSortedFiles();
		
		Console.WriteLine($"Full sort took: {sw.ElapsedMilliseconds/1000.0:F1} s");
		Console.WriteLine($"Unsorted file size: {inputFileSize} B");
		Console.WriteLine($"Sorted file size: {outputFileSizeChanneling} B");
	}
}
