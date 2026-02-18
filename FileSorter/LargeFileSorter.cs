using System.Diagnostics;
using LargeFileSort.FileSorter.ChunkInputFile;
using LargeFileSort.FileSorter.MergeChunks;

namespace LargeFileSort.FileSorter;

public class LargeFileSorter
{
	private readonly FileChunker _fileChunker;
	private readonly SortedFilesMergerChanneling _sortedFilesMerger;
	
	public LargeFileSorter(FileChunker fileChunker, SortedFilesMergerChanneling sortedFilesMerger)
	{
		_fileChunker = fileChunker;
		_sortedFilesMerger = sortedFilesMerger;
	}

	public async Task SortFile()
	{
		var sw = Stopwatch.StartNew();
		var inputFileSize = _fileChunker.ChunkFileAsync();

		//var outputFileSize = SortedFilesMerger.MergeSortedFiles("Chunks", "sorted.txt", 512 * 1024, 512 * 1024);
		//var outputFileSize = SortedFilesMerger.MergeSortedFiles_Threaded("Chunks", "sorted.txt", 4 * 1024 * 1024, 4 * 1024 * 1024);
		//var outputFileSizeSimple = new SortedFilesMergerSimple().MergeSortedFiles("Chunks", "sorted_simple.txt", 512 * 1024, 512 * 1024);
		//var outputFileSize2Stage = new SortedFilesMergerIntermediateFiles().MergeSortedFiles("Chunks", "sorted_2stage.txt", 40 * 1024 * 1024, 40 * 1024 * 1024);
		var outputFileSizeChanneling = _sortedFilesMerger.MergeSortedFiles();
		
		Console.WriteLine($"Full sort took: {sw.ElapsedMilliseconds/1000.0:F1} s");
		Console.WriteLine($"Unsorted file size: {inputFileSize} B");
		Console.WriteLine($"Sorted file size: {outputFileSizeChanneling} B");
	}
}
