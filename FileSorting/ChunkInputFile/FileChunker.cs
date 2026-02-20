using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using LargeFileSort.Configurations;
using LargeFileSort.Logging;
using Microsoft.Extensions.Options;

namespace LargeFileSort.FileSorting.ChunkInputFile;

public class FileChunker
{
	private readonly int _sortWorkerCount;
	private readonly int _baseChunkSize;
	
	private readonly int _memoryBudgetGb;
	private readonly string _unsortedFilePath;
	private readonly string _chunkDirectoryPath;
	
	private long _inputFileSize;
	private readonly int _maxRank;
	
	private readonly SortedChunk?[] _sortedChunks;
	private readonly Lock _sortedChunksLock = new();
	
	private int _fileChunkCounter; 
	private readonly Lock _fileCounterLock = new();

	private readonly Channel<UnsortedChunk> _sortChannel;
	private readonly Channel<SortedChunk> _mergeInMemoryChannel;
	private readonly Channel<(SortedChunk, SortedChunk?)> _mergeToFileChannel;
	
	private readonly SortOptions _sortOptions;
	private readonly LiveProgressLogger _logger;

	public FileChunker(
		IOptions<SortOptions> sortOptions, 
		IOptions<GeneralOptions> generalOptions, 
		LiveProgressLogger logger)
	{
		_sortOptions = sortOptions.Value;
		_logger = logger;
		_sortWorkerCount = Environment.ProcessorCount 
		                   - _sortOptions.MergeToFileWorkerCount 
		                   - _sortOptions.MergeWorkerCount 
		                   - 1;
		_baseChunkSize = _sortOptions.BaseChunkSizeMb * 1024 * 1024;
		
		_maxRank = MaxRank((int)(_sortOptions.IntermediateFileSizeMaxMb/2.0 * 1024 * 1024), _baseChunkSize);
		_sortedChunks = new SortedChunk?[_maxRank + 1];
		
		var generalOptionsValue = generalOptions.Value;
		_memoryBudgetGb = generalOptionsValue.MemoryBudgetGb;
		_unsortedFilePath = Path.Combine(generalOptionsValue.FilesLocation, generalOptionsValue.UnsortedFileName);
		_chunkDirectoryPath = Path.Combine(
			generalOptionsValue.FilesLocation, 
			generalOptionsValue.ChunksDirectoryBaseName);
		
		_sortChannel = Channel.CreateBounded<UnsortedChunk>(
			new BoundedChannelOptions(_sortOptions.QueueLength)
			{
				SingleWriter = true,
				FullMode = BoundedChannelFullMode.Wait
			});
		_mergeInMemoryChannel = Channel.CreateBounded<SortedChunk>(
			new BoundedChannelOptions(1)
			{
				FullMode = BoundedChannelFullMode.Wait
			});
		_mergeToFileChannel = Channel.CreateBounded<(SortedChunk, SortedChunk?)>(
			new BoundedChannelOptions(1)
			{
				SingleReader = true,
				FullMode = BoundedChannelFullMode.Wait
			});
	}

	public long SplitFileIntoSortedChunkFiles()
	{
		var sw = Stopwatch.StartNew();
		
		Console.WriteLine();
		Console.WriteLine("SORT STEP 1: splitting input file into sorted chunks");
		Console.WriteLine();

		if (!ShouldContinue())
		{
			return _inputFileSize;
		}

		var tasks = new List<Task>
		{
			Task.Run(ReadInputFileAsync),

			// _sortWorkerCount chunk sorters
			Task.WhenAll(Enumerable.Range(0, _sortWorkerCount).Select(_ => Task.Run(SortChunkAsync)))
				.ContinueWith(_ => _mergeInMemoryChannel.Writer.Complete()),

			// _mergeWorkerCount merge workers, continues with flushing queue
			Task.WhenAll(Enumerable.Range(0, _sortOptions.MergeWorkerCount).Select(_ => Task.Run(MergeInMemoryAsync)))
				.ContinueWith(_ => FlushMergeQueue()),

			// merge to file worker(s)
			Task.WhenAll(Enumerable .Range(0, _sortOptions.MergeToFileWorkerCount) .Select(_ => Task.Run(MergeToFileAsync))),
		};
		
		var loggerCancellationTokenSource = new CancellationTokenSource();
		// ReSharper disable once MethodSupportsCancellation
		_ = Task.Run(() => _logger.LogState(
			DateTime.Now, 
			() => $"   Sort queue:{_sortChannel.Reader.Count}/{_sortOptions.QueueLength}  Merge queue:{_mergeInMemoryChannel.Reader.Count}/{1}  Merge to file queue:{_mergeToFileChannel.Reader.Count}/{1}", 
			loggerCancellationTokenSource.Token));

		// ReSharper disable once MethodSupportsCancellation
		Task.WaitAll(tasks);
		
		loggerCancellationTokenSource.Cancel();
		
		Console.WriteLine();
		Console.WriteLine($"Split to sorted files in: {sw.ElapsedMilliseconds/1000.0:F1} ms");
		Console.WriteLine();
		
		return _inputFileSize;
	}

	private bool ShouldContinue()
	{
		_inputFileSize = File.Exists(_unsortedFilePath) ? new FileInfo(_unsortedFilePath).Length : 0;
		
		if (Directory.Exists(_chunkDirectoryPath) && _sortOptions.ReuseChunks)
		{
			Console.WriteLine($"Reusing chunks from {_chunkDirectoryPath}");
			Console.WriteLine();
			return false;
		}
		
		if (_inputFileSize == 0)
		{
			throw new FileNotFoundException(
				$"Chunker did not find unsorted file {_unsortedFilePath} or it's empty, and it could not reuse existing chunks."
				+ Environment.NewLine
				+ $"Add '--generate true' to generate file or '--reuseChunks true' if chunks exist in {_chunkDirectoryPath}");
		}

		if (Directory.Exists(_chunkDirectoryPath))
		{
			Directory.Delete(_chunkDirectoryPath, true);
		}
		Directory.CreateDirectory(_chunkDirectoryPath);
		return true;
	}

	private async Task ReadInputFileAsync()
	{
		_logger.LogSingleMessage($"Started reading {_unsortedFilePath}");

		using var reader = new StreamReader(
			_unsortedFilePath,
			Encoding.UTF8,
			true,
			new FileStreamOptions
			{
				BufferSize = _sortOptions.BufferSizeMb * 1024 * 1024,
				Options = FileOptions.SequentialScan
			});
		
		var chunk = new UnsortedChunk(_baseChunkSize);
		bool isReadToEnd;

		do
		{
			var charsRead = reader.Read(chunk.Span[chunk.StartOffset..]);
			isReadToEnd = reader.Peek() == -1;
			_logger.BytesRead += charsRead;

			var lineEndIndex = chunk.Span.LastIndexOf('\n');
			if (isReadToEnd)
			{
				lineEndIndex = charsRead + chunk.StartOffset - 1;
			}
				
			chunk.FilledLength = lineEndIndex + 1;
			await _sortChannel.Writer.WriteAsync(chunk);
				
			if (isReadToEnd)
			{
				break;
			}
				
			// init a new chunk with the end of the previous one and set offset
			var newChunk = new UnsortedChunk(_baseChunkSize);
			if (chunk.FilledLength < _baseChunkSize)
			{
				newChunk.StartOffset = _baseChunkSize - chunk.FilledLength;
				chunk.Span[(lineEndIndex+1)..].CopyTo(newChunk.Span[..newChunk.StartOffset]);
			}
			chunk = newChunk;

			var usedMemory = GC.GetTotalMemory(true);
			while ((float)usedMemory / 1024 / 1024 / 1024 > _memoryBudgetGb * 0.85)
			{
				_logger.LogSingleMessage($"approaching memory budget, holding new chunk for 1 sec");
				await Task.Delay(1000);
				usedMemory = GC.GetTotalMemory(true);
			}

		} while (!isReadToEnd);

		_sortChannel.Writer.Complete();
	}

	private async Task SortChunkAsync()
	{
		await foreach (var chunk in _sortChannel.Reader.ReadAllAsync())
		{
			var linesCount = 0;
			for (var i = 0; i < chunk.FilledLength; i++)
			{
				if (chunk.Span[..chunk.FilledLength][i] == '\n')
					linesCount++;
			}
					
			// parse
			var records = new Line[linesCount];
			var count = Line.ParseLines(chunk.Span[..chunk.FilledLength], ref records);
					
			// sort
			var comparer = new LineComparer(chunk.Buffer);
			Array.Sort(records, 0, count, comparer);
			
			var sortedChunk = new SortedChunk(records, chunk, _maxRank, count);
			await _mergeInMemoryChannel.Writer.WriteAsync(sortedChunk);
		}
	}

	private async Task MergeInMemoryAsync()
	{
		await foreach (var minRankChunk in _mergeInMemoryChannel.Reader.ReadAllAsync())
		{
			var chunk = minRankChunk;
			while (true)
			{
				SortedChunk? secondChunk;
				lock (_sortedChunksLock)
				{
					secondChunk = _sortedChunks[chunk.ChunkRank];
					if (secondChunk is null)
					{
						_sortedChunks[chunk.ChunkRank] = chunk;
						break;
					}

					_sortedChunks[chunk.ChunkRank] = null;
				}

				if (chunk.ChunkRank == 0)
				{
					// can't merge this to memory, merge directly to file
					await _mergeToFileChannel.Writer.WriteAsync((chunk, secondChunk));
					break;
				}

				// merge in memory
				chunk = new SortedChunk(chunk, secondChunk);
			}
		}
	}

	private async Task FlushMergeQueue()
	{
		SortedChunk? last;
		SortedChunk? accumulator = null;

		lock (_sortedChunksLock)
		{
			for (var i = _sortedChunks.Length - 1; i > 0; i--)
			{
				last = _sortedChunks[i];

				if (last is null)
					continue;

				_sortedChunks[i] = null;

				accumulator = accumulator is not null ? new SortedChunk(last, accumulator) : last;
			}

			last = _sortedChunks[0];
		}

		switch (last, accumulator)
		{
			case (not null, _):
				await _mergeToFileChannel.Writer.WriteAsync((last, accumulator));
				break;
			case (_, not null):
				await _mergeToFileChannel.Writer.WriteAsync((accumulator, last));
				break;
		}
		
		_mergeToFileChannel.Writer.Complete();
	}

	private async Task MergeToFileAsync()
	{
		await foreach (var (chunkA, chunkB) in _mergeToFileChannel.Reader.ReadAllAsync())
		{
			string filename;
			lock (_fileCounterLock)
			{
				filename = $"large_chunk_{_fileChunkCounter:00000}.txt";
				_fileChunkCounter++;
			}

			var path = Path.Combine(_chunkDirectoryPath, filename);
			await using var writer = new StreamWriter(
				path,
				Encoding.UTF8, 
				new FileStreamOptions
				{
					BufferSize = _sortOptions.BufferSizeMb * 1024 * 1024,
					Mode = FileMode.Create, 
					Access = FileAccess.Write
				});
		
			if (chunkB is not null)
			{
				// merge 2 chunks directly to the file
				chunkA.MergeToStream(chunkB, writer, _logger);
			}
			else
			{
				chunkA.WriteChunk(writer, _logger);
			}
			_logger.LogSingleMessage($"written sorted chunk to file {path}");
		}
	}

	private static int MaxRank(int maxLength, int chunkSize)
	{
		return (int)Math.Floor(Math.Log2((double)maxLength / chunkSize));
	}
}