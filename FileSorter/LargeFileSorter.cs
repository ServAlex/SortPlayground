using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using FileGenerator.FileSorter.MergeFiles;

namespace FileGenerator.FileSorter;

public class LargeFileSorter(
	int bufferSize,
	int sortWorkerCount,
	int mergeWorkerCount,
	int queueLength,
	int chunkSize = 1024 * 1024,
	int empiricalConservativeLineLength = 50,
	int fileMaxLength = int.MaxValue / 2,
	int memoryBudgetMb = 16 * 1024)
{
	private int _chunkCounter; 
	private int _fileChunkCounter; 
	
	private readonly SortedChunk?[] _sortedChunks = new SortedChunk?[6];
	private readonly Lock _sortedChunksLock = new();
	private readonly Lock _fileCounterLock = new();

	private readonly Channel<CharChunk> _sortChannel = Channel.CreateBounded<CharChunk>(
		new BoundedChannelOptions(queueLength)
		{
			SingleWriter = true,
			FullMode = BoundedChannelFullMode.Wait
		});

	private readonly Channel<SortedChunk> _mergeInMemoryChannel = Channel.CreateBounded<SortedChunk>(
		new BoundedChannelOptions(1)
		{
			SingleReader = true,
			FullMode = BoundedChannelFullMode.Wait
		});

	private readonly Channel<(SortedChunk, SortedChunk?)> _mergeToFileChannel = Channel.CreateBounded<(SortedChunk, SortedChunk?)>(
		new BoundedChannelOptions(1)
		{
			SingleWriter = true,
			SingleReader = true,
			FullMode = BoundedChannelFullMode.Wait
		});

	public async Task SortFile(string fileName = "test.txt")
	{
		var sw = Stopwatch.StartNew();
		
		
		if (Directory.Exists("Chunks"))
		{
			Directory.Delete("Chunks", true);
		}
		Directory.CreateDirectory("Chunks");
		
		// file reader task
		var tasks = new List<Task> { Task.Run(async () => await ReadInputFileAsync(fileName, _sortChannel)) };

		// chunk sorters
		tasks.Add(
			Task.WhenAll(
					Enumerable
						.Range(0, sortWorkerCount)
						.Select(_ =>
							Task.Run(async () => await SortChunkAsync(_sortChannel, queueLength, _mergeInMemoryChannel)))
				)
				.ContinueWith(_ => _mergeInMemoryChannel.Writer.Complete())
		);
		
		// merge worker
		// continue with flushing queue
		tasks.Add(
			Task.WhenAll(
					Enumerable
						.Range(0, mergeWorkerCount)
						.Select(_ =>
							Task.Run(async () => await MergeInMemoryAsync(_mergeInMemoryChannel, _mergeToFileChannel)))
				)
				.ContinueWith(_ => FlushMergeQueue(_mergeToFileChannel))
		);
		
		// merge to file worker
		tasks.Add(Task.Run(async () => await MergeToFileAsync(_mergeToFileChannel, "Chunks")));
		
		await Task.WhenAll(tasks);
		
		Log($"Split to sorted files in: {sw.ElapsedMilliseconds} ms");
		
		SortedFilesMerger.MergeSortedFiles("Chunks", "sorted.txt", 8 * bufferSize, 8 * bufferSize);
	}

	private async Task ReadInputFileAsync(string fileName, Channel<CharChunk> sortChannel)
	{
		Log("Started reading");

		using var reader = new StreamReader(
			fileName,
			Encoding.UTF8,
			true,
			new FileStreamOptions
			{
				BufferSize = bufferSize,
				Options = FileOptions.SequentialScan
			});

		var chunk = new CharChunk(chunkSize);
		var isReadToEnd = false;

		do
		{
			var charsRead = reader.Read(chunk.Span[chunk.StartOffset..]);
			//isReadToEnd = chunk.StartOffset + charsRead < chunkSize;
			isReadToEnd = reader.Peek() == -1;

			//var isRead = reader.EndOfStream;
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
			var newChunk = new CharChunk(chunkSize);
			if (chunk.FilledLength < chunkSize)
			{
				newChunk.StartOffset = chunkSize - chunk.FilledLength;
				chunk.Span[(lineEndIndex+1)..].CopyTo(newChunk.Span[..newChunk.StartOffset]);
			}
			chunk = newChunk;

			var usedMemoryKb = GC.GetTotalMemory(true) / 1024;
			while ((float)usedMemoryKb/1024 > memoryBudgetMb*0.85)
			{
				Log($"--------------------- used memory: {(float)usedMemoryKb / 1024 / 1024:F1} GB");
				await Task.Delay(1000);
				usedMemoryKb = GC.GetTotalMemory(true) / 1024;
			}

		} while (!isReadToEnd);

		sortChannel.Writer.Complete();
		Console.WriteLine("Channel completed writing");
	}

	private async Task SortChunkAsync(
		Channel<CharChunk> sortChannel, 
		int unsortedChannelCapacity, 
		Channel<SortedChunk> mergeInMemoryChannel)
	{
		await foreach (var chunk in sortChannel.Reader.ReadAllAsync())
		{
			Stopwatch sw = Stopwatch.StartNew();
			// parse
			var estimatedLines = chunk.FilledLength / empiricalConservativeLineLength;
			var records = new Line[estimatedLines];
					
			var count = ParseLines(chunk.Span[..chunk.FilledLength], ref records);
					
			// sort
			var comparer = new LineComparer(chunk.Buffer);
			Array.Sort(records, 0, count, comparer);
			
			Log($"sorted chunk {_chunkCounter:0000} with {count} lines in {sw.ElapsedMilliseconds} ms");
			Interlocked.Increment(ref _chunkCounter);
			
			var rank = DataLengthToRank(chunk.FilledLength, fileMaxLength);
			SortedChunk sortedChunk = new SortedChunk(records, chunk, rank, count);

			// merge
			//MergeChunks(sortedChunk);
			await mergeInMemoryChannel.Writer.WriteAsync(sortedChunk);
			
			// todo: wait if too much memory is occupied
		}
		
		// merge all remaining sorted chunks
	}
	
	private void Log(string message)
	{
		var maxTextLength = 120;
		StringBuilder sb = new(message);
		if (sb.Length < maxTextLength)
			sb.Append(' ', maxTextLength - sb.Length);

		sb.Append(
			$"SQ:{_sortChannel.Reader.Count}/{queueLength} MQ:{_mergeInMemoryChannel.Reader.Count}/{1} MFQ:{_mergeToFileChannel.Reader.Count}/{1}");
		
		var usedMemoryKb = GC.GetTotalMemory(true) / 1024;
		sb.Append($" mem:{(float)usedMemoryKb / 1024 / 1024:F1}/{memoryBudgetMb / 1024} GB");
		
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
						Log($"stored chunk of rank {chunk.ChunkRank}      {string.Join(", ", _sortedChunks.Select(c => c is null ? "0" : "1"))}");
						break;
					}

					_sortedChunks[chunk.ChunkRank] = null;
				}

				Log($"merging chanks of rank {chunk.ChunkRank}");
				if (chunk.ChunkRank == 0)
				{
					// cant merge this to memory, write result directly to file

					//WriteChunks(chunk, second);
					await mergeToFileChannel.Writer.WriteAsync((chunk, second));
					break;
				}

				// merge
				var newSorted = new SortedChunk(chunk, second);

				chunk = newSorted;
			}
		}
		
		//FlushRemainingChunks();
		
		// todo: add flushing routine
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
		await foreach (var (chankA, chunkB) in mergeToFileChannel.Reader.ReadAllAsync())
		{
			WriteChunks(chankA, chunkB);
		}
	}


	private void FlushRemainingChunks()
	{
		// excessive lock, this is the only thread running
		lock (_sortedChunksLock)
		{
			Console.WriteLine($"flushing ramaining accumulated chunks {string.Join(", ", _sortedChunks.Select(c => c is null?"0":"1" ))}");
			SortedChunk? element;
			for(var i = 5; i>0;i--)
			{
				element = _sortedChunks[i];

				if (element is null) continue;
				
				_sortedChunks[i] = null;
				element.ChunkRank--;
				Console.WriteLine($"reposted chunk with new rank {element.ChunkRank}, i={i}");
				MergeChunks(element);
			}

			element = _sortedChunks[0];
			if (element is not null)
			{
				WriteChunks(element);
			}
		}
	}

	private void MergeChunks(SortedChunk sortedChunk)
	{
		while (true)
		{
			SortedChunk? second;
			lock (_sortedChunksLock)
			{
				second = _sortedChunks[sortedChunk.ChunkRank];
				if (second is null)
				{
					_sortedChunks[sortedChunk.ChunkRank] = sortedChunk;
					Console.WriteLine($"stored chunk of rank {sortedChunk.ChunkRank}      {string.Join(", ", _sortedChunks.Select(c => c is null ? "0" : "1"))}");
					return;
				}

				_sortedChunks[sortedChunk.ChunkRank] = null;
			}

			Console.WriteLine($"merging chanks of rank {sortedChunk.ChunkRank}");
			if (sortedChunk.ChunkRank == 0)
			{
				// cant merge this to memory, write result directly to file

				WriteChunks(sortedChunk, second);
				return;
			}

			// merge
			var newSorted = new SortedChunk(sortedChunk, second);

			sortedChunk = newSorted;
		}
	}

	private void WriteChunks(SortedChunk chankA, SortedChunk? chankB = null)
	{
		var sw = Stopwatch.StartNew();
		string filename;
		lock (_fileCounterLock)
		{
			filename = $"large_chunk_{_fileChunkCounter:00000}.txt";
			_fileChunkCounter++;
		}

		using var writer = new StreamWriter(
			Path.Combine("Chunks", filename),
			Encoding.UTF8, 
			new FileStreamOptions
			{
				BufferSize = 1 << 22, 
				Mode = FileMode.Create, 
				Access = FileAccess.Write
			});
		
		if (chankB is not null)
		{
			// merge sorted and second directly to file
			chankA.WriteOnMerge(chankB, writer, 8 * 1024 * 1024);
		}
		else
		{
			chankA.WriteChunk(writer);
		}
		
		Log($"Chunk written to file {filename} in {sw.ElapsedMilliseconds} ms");
	}

	// todo: optimize
	public int DataLengthToRank(int length, int maxLength)
	{
		var rank = 0;
		while (length < maxLength/2 && rank < 5)
		{
			rank++;
			maxLength /= 2;
		}

		return rank;
	}

	static int ParseLines(ReadOnlySpan<char> data, ref Line[] records)
	{
		int count = 0;
		int i = 0;

		while (i < data.Length)
		{
			if (count == records.Length)
				Array.Resize(ref records, records.Length * 2);

			int lineStart = i;

			int number = 0;
			while (data[i] != '.')
				number = number * 10 + (data[i++] - '0');

			i++; // '.'
			if (data[i] == ' ') i++;

			int textStart = i;

			while (i < data.Length && data[i] != '\n')
				i++;

			int lineEnd = i;
			int lineLength = lineEnd - lineStart;
			int textLength = lineEnd - textStart;

			ref var r = ref records[count++];
			r.Number = number;
			r.LineOffset = lineStart;
			r.StringOffsetFromLine = (short)(textStart - lineStart);
			r.LineLength = (short)lineLength;
			r.StringLength = (short)textLength;
			r.ChunkIndex = 0;
			r.Prefix = Line.EncodeAscii8(data.Slice(textStart, textLength));

			i++; // '\n'
		}

		return count;
	}	
}