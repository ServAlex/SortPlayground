using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using LargeFileSort.Configurations;
using Microsoft.Extensions.Options;

namespace LargeFileSort.FileSorter.ChunkInputFile;

public class FileChunker
{
	private readonly int _bufferSize;
	private readonly int _memoryBudgetMb;
	private readonly int _sortWorkerCount;
	private readonly int _mergeWorkerCount;
	private readonly int _queueLength;
	private readonly int _baseChunkSize;
	
	private readonly int _maxRank;
	private readonly SortedChunk?[] _sortedChunks;
	private readonly Lock _sortedChunksLock = new();
	private readonly Lock _fileCounterLock = new();

	private readonly Channel<CharChunk> _sortChannel;
	private readonly Channel<SortedChunk> _mergeInMemoryChannel ;
	private readonly Channel<(SortedChunk, SortedChunk?)> _mergeToFileChannel;


	private int _fileChunkCounter; 
	private long _inputFileSize;
	
	private readonly SortOptions _sortOptions;
	private readonly PathOptions _pathOptions;
	private readonly FileProgressLogger _logger;

	public FileChunker(IOptions<SortOptions> sortOptions, IOptions<PathOptions> pathOptions, FileProgressLogger logger)
	{
		_sortOptions = sortOptions.Value;
		_pathOptions = pathOptions.Value;
		_logger = logger;
		_bufferSize = _sortOptions.BufferSizeMb * 1024 * 1024;
		_mergeWorkerCount = _sortOptions.MergeWorkerCount;
		//_sortWorkerCount = _sortOptions.SortWorkerCount;
		_sortWorkerCount = Environment.ProcessorCount - 2 - _mergeWorkerCount;
		_queueLength = _sortOptions.QueueLength;
		_baseChunkSize = _sortOptions.BaseChunkSizeMb * 1024 * 1024;
		_memoryBudgetMb = _sortOptions.MemoryBudgetGb * 1024;
		
		_maxRank = MaxRank((int)(_sortOptions.IntermediateFileSizeMaxMb/2.0 * 1024 * 1024), _baseChunkSize);
		_sortedChunks = new SortedChunk?[_maxRank + 1];
		
		_sortChannel = Channel.CreateBounded<CharChunk>(
			new BoundedChannelOptions(_queueLength)
			{
				SingleWriter = true,
				FullMode = BoundedChannelFullMode.Wait
			});
		_mergeInMemoryChannel = Channel.CreateBounded<SortedChunk>(
			new BoundedChannelOptions(1)
			{
				SingleReader = true,
				FullMode = BoundedChannelFullMode.Wait
			});
		_mergeToFileChannel = Channel.CreateBounded<(SortedChunk, SortedChunk?)>(
			new BoundedChannelOptions(1)
			{
				SingleWriter = true,
				SingleReader = true,
				FullMode = BoundedChannelFullMode.Wait
			});
	}

	public long ChunkFileAsync()
	{
		var sw = Stopwatch.StartNew();
		Console.WriteLine();

		var chunkDirectoryPath = Path.Combine(_pathOptions.FilesLocation, _pathOptions.ChunksDirectoryBaseName);
		if (Directory.Exists(chunkDirectoryPath))
		{
			Directory.Delete(chunkDirectoryPath, true);
		}
		Directory.CreateDirectory(chunkDirectoryPath);
		
		var unsortedFilePath = Path.Combine(_pathOptions.FilesLocation, _pathOptions.UnsortedFileName);
		
		var tasks = new List<Task>
		{
			// file reader task
			Task.Run(async () => await ReadInputFileAsync(unsortedFilePath, _sortChannel)),
			
			// chunk sorters
			Task.WhenAll(
					Enumerable
						.Range(0, _sortWorkerCount)
						.Select(_ =>
							Task.Run(async () => await SortChunkAsync(_sortChannel, _mergeInMemoryChannel)))
				)
				.ContinueWith(_ => _mergeInMemoryChannel.Writer.Complete()),
			
			// merge worker, continues with flushing queue
			Task.WhenAll(
					Enumerable
						.Range(0, _mergeWorkerCount)
						.Select(_ =>
							Task.Run(async () => await MergeInMemoryAsync(_mergeInMemoryChannel, _mergeToFileChannel)))
				)
				.ContinueWith(_ => FlushMergeQueue(_mergeToFileChannel)),
			
			// merge to file worker
			Task.Run(async () => await MergeToFileAsync(_mergeToFileChannel, chunkDirectoryPath))
		};
		
		var loggerCancellationTokenSource = new CancellationTokenSource();
		// ReSharper disable once MethodSupportsCancellation
		_ = Task.Run(() => _logger.LogState(DateTime.Now, () => 
			$"   SQ:{_sortChannel.Reader.Count}/{_queueLength}  MQ:{_mergeInMemoryChannel.Reader.Count}/{1}  MFQ:{_mergeToFileChannel.Reader.Count}/{1}", loggerCancellationTokenSource.Token));

		
		// ReSharper disable once MethodSupportsCancellation
		Task.WaitAll(tasks);
		
		loggerCancellationTokenSource.Cancel();
		
		Console.WriteLine();
		Console.WriteLine($"Split to sorted files in: {sw.ElapsedMilliseconds/1000.0:F1} ms");
		Console.WriteLine();
		
		return _inputFileSize;
	}
	
	private async Task ReadInputFileAsync(string fileName, Channel<CharChunk> sortChannel)
	{
		_logger.LogSingleMessage($"Started reading {fileName}");

		using var reader = new StreamReader(
			fileName,
			Encoding.UTF8,
			true,
			new FileStreamOptions
			{
				BufferSize = _bufferSize,
				Options = FileOptions.SequentialScan
			});
		
		_inputFileSize = reader.BaseStream.Length;

		var chunk = new CharChunk(_baseChunkSize);
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
			await sortChannel.Writer.WriteAsync(chunk);
				
			if (isReadToEnd)
			{
				break;
			}
				
			// init a new chunk with the end of the previous one and set offset
			var newChunk = new CharChunk(_baseChunkSize);
			if (chunk.FilledLength < _baseChunkSize)
			{
				newChunk.StartOffset = _baseChunkSize - chunk.FilledLength;
				chunk.Span[(lineEndIndex+1)..].CopyTo(newChunk.Span[..newChunk.StartOffset]);
			}
			chunk = newChunk;

			var usedMemoryKb = GC.GetTotalMemory(true) / 1024;
			while ((float)usedMemoryKb/1024 > _memoryBudgetMb*0.85)
			{
				_logger.LogSingleMessage($"approaching memory budget, holding new chunk for 1 sec");
				await Task.Delay(1000);
				usedMemoryKb = GC.GetTotalMemory(true) / 1024;
			}

		} while (!isReadToEnd);

		sortChannel.Writer.Complete();
	}

	private async Task SortChunkAsync(Channel<CharChunk> sortChannel, Channel<SortedChunk> mergeInMemoryChannel)
	{
		await foreach (var chunk in sortChannel.Reader.ReadAllAsync())
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
			//_logger.LogSingleMessage($"sorted chunk with {count} lines {chunk.FilledLength} chars in {sw.ElapsedMilliseconds} ms");
			
			var sortedChunk = new SortedChunk(records, chunk, _maxRank, count);
			await mergeInMemoryChannel.Writer.WriteAsync(sortedChunk);
		}
	}

	private async Task MergeInMemoryAsync(
		Channel<SortedChunk> sortedChunkChannel, 
		Channel<(SortedChunk, SortedChunk?)> mergeToFileChannel)
	{
		await foreach (var minRankChunk in sortedChunkChannel.Reader.ReadAllAsync())
		{
			var chunk = minRankChunk;
			while (true)
			{
				SortedChunk? second;
				lock (_sortedChunksLock)
				{
					second = _sortedChunks[chunk.ChunkRank];
					if (second is null)
					{
						_sortedChunks[chunk.ChunkRank] = chunk;
						//_logger.LogSingleMessage($"stored chunk of rank {chunk.ChunkRank}     {string.Join(", ", _sortedChunks.Select(c => c is null ? "0" : "1"))}");
						break;
					}

					_sortedChunks[chunk.ChunkRank] = null;
				}

				if (chunk.ChunkRank == 0)
				{
					// can't merge this to memory, merge directly to file
					await mergeToFileChannel.Writer.WriteAsync((chunk, second));
					break;
				}

				// merge in memory
				var newSorted = new SortedChunk(chunk, second);

				chunk = newSorted;
			}
		}
	}

	private async Task FlushMergeQueue(Channel<(SortedChunk, SortedChunk?)> mergeToFileChannel)
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
				await mergeToFileChannel.Writer.WriteAsync((last, accumulator));
				break;
			case (_, not null):
				await mergeToFileChannel.Writer.WriteAsync((accumulator, last));
				break;
		}
		
		mergeToFileChannel.Writer.Complete();
	}

	private async Task MergeToFileAsync(Channel<(SortedChunk, SortedChunk?)> mergeToFileChannel, string directoryName)
	{
		await foreach (var (chunkA, chunkB) in mergeToFileChannel.Reader.ReadAllAsync())
		{
			string filename;
			lock (_fileCounterLock)
			{
				filename = $"large_chunk_{_fileChunkCounter:00000}.txt";
				_fileChunkCounter++;
			}

			var path = Path.Combine(directoryName, filename);
			await using var writer = new StreamWriter(
				path,
				Encoding.UTF8, 
				new FileStreamOptions
				{
					BufferSize = 1 << 22, 
					Mode = FileMode.Create, 
					Access = FileAccess.Write
				});
		
			if (chunkB is not null)
			{
				// merge 2 chunks directly to the file
				_logger.BytesWritten += chunkA.MergeToStream(chunkB, writer, 1024 * 1024);
				_logger.LogSingleMessage($"written merged chunk to file {path}");
			}
			else
			{
				_logger.BytesWritten += chunkA.WriteChunk(writer);
				_logger.LogSingleMessage($"written part chunk to file {path}");
			}
		}
	}

	private static int MaxRank(int maxLength, int chunkSize)
	{
		return (int)Math.Floor(Math.Log2((double)maxLength / chunkSize));
	}
}