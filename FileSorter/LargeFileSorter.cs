using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using FileGenerator.FileSorter.MergeFiles;

namespace FileGenerator.FileSorter;

public class LargeFileSorter
{
	private int _fileChunkCounter; 
	
	private readonly int _bufferSize;
	private readonly int _sortWorkerCount;
	private readonly int _mergeWorkerCount;
	private readonly int _queueLength;
	private readonly int _baseChunkSize;
	private readonly int _memoryBudgetMb;
	private readonly int _empiricalConservativeLineLength;
	
	private readonly int _maxRank;
	private readonly SortedChunk?[] _sortedChunks;
	private readonly Lock _sortedChunksLock = new();
	private readonly Lock _fileCounterLock = new();

	private readonly Channel<CharChunk> _sortChannel;
	private readonly Channel<SortedChunk> _mergeInMemoryChannel ;
	private readonly Channel<(SortedChunk, SortedChunk?)> _mergeToFileChannel;

	private long _inputFileSize;

	private readonly FileProgressLogger logger;
	
	public LargeFileSorter(LargeFileSorterOptions options, FileProgressLogger logger)
	{
		this.logger = logger;
		_bufferSize = options.BufferSizeMb * 1024 * 1024;
		_sortWorkerCount = options.SortWorkerCount;
		_mergeWorkerCount = options.MergeWorkerCount;
		_queueLength = options.QueueLength;
		_baseChunkSize = options.BaseChunkSizeMb * 1024 * 1024;
		_memoryBudgetMb = options.MemoryBudgetGb * 1024;
		_empiricalConservativeLineLength = options.EmpiricalConservativeLineLength;
		
		_maxRank = MaxRank(options.MaxInMemoryChunkSizeMb * 1024 * 1024, _baseChunkSize);
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

	public async Task SortFile(string fileName = "test.txt")
	{
		var sw = Stopwatch.StartNew();
	/*	
		if (Directory.Exists("Chunks"))
		{
			Directory.Delete("Chunks", true);
		}
		Directory.CreateDirectory("Chunks");
		
		var tasks = new List<Task>
		{
			// file reader task
			Task.Run(async () => await ReadInputFileAsync(fileName, _sortChannel)),
			
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
			Task.Run(async () => await MergeToFileAsync(_mergeToFileChannel, "Chunks"))
		};
		
		var loggerCancellationTokenSource = new CancellationTokenSource();
		Task.Run(() => logger.LogState(DateTime.Now, null, loggerCancellationTokenSource.Token));

		await Task.WhenAll(tasks);
		
		await loggerCancellationTokenSource.CancelAsync();
*/
		
//#		Log($"Split to sorted files in: {sw.ElapsedMilliseconds} ms");

		//var outputFileSize = SortedFilesMerger.MergeSortedFiles("Chunks", "sorted.txt", 512 * 1024, 512 * 1024);
		//var outputFileSize = SortedFilesMerger.MergeSortedFiles_Threaded("Chunks", "sorted.txt", 4 * 1024 * 1024, 4 * 1024 * 1024);
		//var outputFileSizeSimple = new SortedFilesMergerSimple().MergeSortedFiles("Chunks", "sorted_simple.txt", 512 * 1024, 512 * 1024);
		//var outputFileSize2Stage = new SortedFilesMergerIntermediateFiles().MergeSortedFiles("Chunks", "sorted_2stage.txt", 40 * 1024 * 1024, 40 * 1024 * 1024);
		var outputFileSizeChanneling = new SortedFilesMergerChanneling(logger.Reset()).
			MergeSortedFiles("Chunks", "sorted_channelling.txt", 1 * 1024 * 1024, 1 * 1024 * 1024);
		
		Console.WriteLine($"Input file size: {_inputFileSize} B");
		//Console.WriteLine($"Output file size simple: {outputFileSizeSimple} B");
		//Console.WriteLine($"Output file size 2 stage: {outputFileSize2Stage} B");
		Console.WriteLine($"Output file size channeling: {outputFileSizeChanneling} B");
	}

	private async Task ReadInputFileAsync(string fileName, Channel<CharChunk> sortChannel)
	{
//#		Log("Started reading");
		logger.LogSingleMessage($"Started reading {fileName}");

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
			logger.BytesRead += charsRead;

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
				
			// init new chunk with end of previous one and set offset
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
				logger.LogSingleMessage($"approaching memory budget, holding new chunk for 1 sec");
//#				Log($"--------------------- used memory: {(float)usedMemoryKb / 1024 / 1024:F1} GB");
				await Task.Delay(1000);
				usedMemoryKb = GC.GetTotalMemory(true) / 1024;
			}

		} while (!isReadToEnd);

		sortChannel.Writer.Complete();
//#		Console.WriteLine("Channel completed writing");
	}

	private async Task SortChunkAsync(Channel<CharChunk> sortChannel, Channel<SortedChunk> mergeInMemoryChannel)
	{
		await foreach (var chunk in sortChannel.Reader.ReadAllAsync())
		{
			Stopwatch sw = Stopwatch.StartNew();
			// parse
			var estimatedLines = chunk.FilledLength / _empiricalConservativeLineLength;
			var records = new Line[estimatedLines];
					
			var count = Line.ParseLines(chunk.Span[..chunk.FilledLength], ref records);
					
			// sort
			var comparer = new LineComparer(chunk.Buffer);
			Array.Sort(records, 0, count, comparer);
			
//			logger.LogSingleMessage($"sorted chunk with {count} lines {chunk.FilledLength} chars in {sw.ElapsedMilliseconds} ms");
//#			Log($"sorted chunk with {count} lines {chunk.FilledLength} chars in {sw.ElapsedMilliseconds} ms");
			
			SortedChunk sortedChunk = new SortedChunk(records, chunk, _maxRank, count);
			await mergeInMemoryChannel.Writer.WriteAsync(sortedChunk);
			
			// todo: wait if too much memory is occupied
		}
	}
	
	private void Log(string message)
	{
		var maxTextLength = 120;
		StringBuilder sb = new(message);
		if (sb.Length < maxTextLength)
			sb.Append(' ', maxTextLength - sb.Length);

		sb.Append(
			$"SQ:{_sortChannel.Reader.Count}/{_queueLength} MQ:{_mergeInMemoryChannel.Reader.Count}/{1} MFQ:{_mergeToFileChannel.Reader.Count}/{1}");
		
		var usedMemoryKb = GC.GetTotalMemory(true) / 1024;
		sb.Append($" mem:{(float)usedMemoryKb / 1024 / 1024:F1}/{_memoryBudgetMb / 1024} GB");
		
		Console.WriteLine(sb);
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
//#						Log($"stored chunk of rank {chunk.ChunkRank}      {string.Join(", ", _sortedChunks.Select(c => c is null ? "0" : "1"))}");
						break;
					}

					_sortedChunks[chunk.ChunkRank] = null;
				}

//#				Log($"merging chunks of rank {chunk.ChunkRank}");
				if (chunk.ChunkRank == 0)
				{
					// can't merge this to memory, merge directly to file
					await mergeToFileChannel.Writer.WriteAsync((chunk, second));
					break;
				}

				// merge
				var newSorted = new SortedChunk(chunk, second);

				chunk = newSorted;
			}
		}
	}

	private async Task FlushMergeQueue(Channel<(SortedChunk, SortedChunk?)> mergeToFileChannel)
	{
		SortedChunk? last;
		SortedChunk? accumulator = null;
		for(var i = _sortedChunks.Length - 1; i > 0; i--)
		{
			last = _sortedChunks[i];

			if (last is null) 
				continue;
				
			_sortedChunks[i] = null;

			accumulator = accumulator is not null ? new SortedChunk(last, accumulator) : last;
		}

		last = _sortedChunks[0];
		
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
				//logger.BytesWritten += 
				chunkA.MergeToStream(chunkB, writer, 1024 * 1024);
				logger.LogSingleMessage($"written merged chunk to file {path}");
			}
			else
			{
				//logger.BytesWritten +=
				chunkA.WriteChunk(writer);
				logger.LogSingleMessage($"written part chunk to file {path}");
			}
		
//#			Log($"Chunk written to file {filename} in {sw.ElapsedMilliseconds} ms");
		}
	}

	private static int MaxRank(int maxLength, int chunkSize)
	{
		return (int)Math.Floor(Math.Log2((double)maxLength / chunkSize));
	}
}
