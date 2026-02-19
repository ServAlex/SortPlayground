using System.Diagnostics;
using LargeFileSort.Configurations;
using LargeFileSort.FileSorter.ChunkInputFile;
using LargeFileSort.FileSorter.MergeChunks;
using Microsoft.Extensions.Options;

namespace LargeFileSort.FileSorter;

public class LargeFileSorter(
	FileChunker fileChunker,
	SortedFilesMerger sortedFilesMerger,
	IOptions<SortOptions> sortOptions)
{
	private readonly SortOptions _sortOptions = sortOptions.Value;

	public void SortFile()
	{
		if (!_sortOptions.Enabled)
		{
			Console.WriteLine("Sorting is not enabled in options, skipping");
			return;
		}
		
		var sw = Stopwatch.StartNew();
		var inputFileSize = fileChunker.SplitFileIntoSortedChunkFiles();
		var outputFileSizeChanneling = sortedFilesMerger.MergeSortedFiles();
		
		Console.WriteLine($"Full sort took: {sw.ElapsedMilliseconds/1000.0:F1} s");
		Console.WriteLine($"Unsorted file size: {inputFileSize} B");
		Console.WriteLine($"Sorted file size: {outputFileSizeChanneling} B");
	}
}
