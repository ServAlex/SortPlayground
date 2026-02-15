using FileGenerator.FileSorter.ChunkInputFile;
using FileGenerator.FileSorter.MergeChunks;

namespace FileGenerator.FileSorter;

public class LargeFileSorter
{
	private readonly int _memoryBudgetMb;
	private readonly int _bufferSize;
	private readonly FileProgressLogger _logger;
	
	private LargeFileSorterOptions _options;
	
	public LargeFileSorter(LargeFileSorterOptions options, FileProgressLogger logger)
	{
		_logger = logger;
		_bufferSize = options.BufferSizeMb * 1024 * 1024;
		_memoryBudgetMb = options.MemoryBudgetGb * 1024;
		_options = options;
	}

	public async Task SortFile(string fileName, string chunksDirectoryName, string outputFileName)
	{
		var fileChunker = new FileChunker(_options, _logger.Reset());
		var inputFileSize = fileChunker.ChunkFileAsync(fileName, chunksDirectoryName);

		//var outputFileSize = SortedFilesMerger.MergeSortedFiles("Chunks", "sorted.txt", 512 * 1024, 512 * 1024);
		//var outputFileSize = SortedFilesMerger.MergeSortedFiles_Threaded("Chunks", "sorted.txt", 4 * 1024 * 1024, 4 * 1024 * 1024);
		//var outputFileSizeSimple = new SortedFilesMergerSimple().MergeSortedFiles("Chunks", "sorted_simple.txt", 512 * 1024, 512 * 1024);
		//var outputFileSize2Stage = new SortedFilesMergerIntermediateFiles().MergeSortedFiles("Chunks", "sorted_2stage.txt", 40 * 1024 * 1024, 40 * 1024 * 1024);
		var outputFileSizeChanneling = new SortedFilesMergerChanneling(_logger.Reset()).MergeSortedFiles(
			chunksDirectoryName,
			outputFileName,
			_bufferSize,
			_bufferSize,
			_memoryBudgetMb);
		
		Console.WriteLine($"Input file size: {inputFileSize} B");
		//Console.WriteLine($"Output file size simple: {outputFileSizeSimple} B");
		//Console.WriteLine($"Output file size 2 stage: {outputFileSize2Stage} B");
		Console.WriteLine($"Output file size channeling: {outputFileSizeChanneling} B");
	}
}
