using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using FileGenerator.FileSorter.MergeFiles;

namespace FileGenerator.FileSorter;

public class LargeFileSorter(
	int bufferSize,
	int workerCount,
	int queueLength,
	int chunkSize = 1024 * 1024,
	int lineMaxLength = 10 + 2 + 100 + 1,
	int empiricalConservativeLineLength = 50,
	int fileMaxLength = int.MaxValue/2,
	int memoryBudgetMb = 16 * 1024)
{
	private int _chunkCounter = 0; 
	private int _fileChunkCounter = 0; 
	
	private readonly SortedChunk?[] _sortedChunks = new SortedChunk?[6];
	private readonly Lock _lock = new();
	private readonly Lock _fileCounterLock = new();

	public async Task SortFile(string fileName = "test.txt")
	{
		var channel = Channel.CreateBounded<CharChunk>(
			new BoundedChannelOptions(queueLength)
			{
				SingleWriter = true
			});
		
		if (Directory.Exists("Chunks"))
		{
			Directory.Delete("Chunks", true);
		}
		Directory.CreateDirectory("Chunks");
		
		// file reader task
		var tasks =  new List<Task> {Task.Run(async () => await ReadAsync(fileName, channel))};

		// worker tasks
		tasks.AddRange( Enumerable
				.Range(0, workerCount)
				.Select(_ => Task.Run(async () => await WorkerAsync(channel, queueLength)))
			);
		
		await Task.WhenAll(tasks);
		FlushRemainingChunks();
		
		SortedFilesMerger.MergeSortedFiles("Chunks", "sorted.txt", bufferSize, bufferSize);
	}

	private async Task ReadAsync(string fileName, Channel<CharChunk> channel)
	{
		Console.WriteLine("Started reading");

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
			await channel.Writer.WriteAsync(chunk);
				
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
				Console.WriteLine($"--------------------- used memory: {(float)usedMemoryKb / 1024 / 1024:F1} GB");
				await Task.Delay(1000);
				usedMemoryKb = GC.GetTotalMemory(true) / 1024;
			}

		} while (!isReadToEnd);

		channel.Writer.Complete();
		Console.WriteLine("Channel completed writing");
	}

	private async Task WorkerAsync(Channel<CharChunk> channel, int channelCapacity)
	{
		await foreach (var chunk in channel.Reader.ReadAllAsync())
		{
			Stopwatch sw = Stopwatch.StartNew();
			// parse
			var estimatedLines = chunk.FilledLength / empiricalConservativeLineLength;
			var records = new Line[estimatedLines];
					
			var count = ParseLines(chunk.Span[..chunk.FilledLength], ref records);
					
			// sort
			var comparer = new LineComparer(chunk.Buffer);
			Array.Sort(records, 0, count, comparer);
			
			var sb = new StringBuilder($"sorted chunk {_chunkCounter:0000} with {count} lines in {sw.ElapsedMilliseconds} ms, Q {channel.Reader.Count}/{channelCapacity}");
			Console.WriteLine(sb);
			sw.Restart();
			Interlocked.Increment(ref _chunkCounter);
			
			var rank = DataLengthToRank(chunk.FilledLength);
			SortedChunk sortedChunk = new SortedChunk(records, chunk, rank, count);

			// merge
			MergeChunks(sortedChunk);
			
			// todo: wait if too much memory is occupied
		}
		
		// merge all remaining sorted chunks
	}

	private void FlushRemainingChunks()
	{
		// excessive lock, this is the only thread running
		lock (_lock)
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
		SortedChunk? second;
		lock (_lock)
		{
			second = _sortedChunks[sortedChunk.ChunkRank];
			if (second is null)
			{
				_sortedChunks[sortedChunk.ChunkRank] = sortedChunk;
				Console.WriteLine($"stored chunk of rank {sortedChunk.ChunkRank}      {string.Join(", ", _sortedChunks.Select(c => c is null?"0":"1" ))}");
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

		var newSorted = new SortedChunk(sortedChunk, second);
		MergeChunks(newSorted);
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
		
		Console.WriteLine($"Chunk written to file {filename} in {sw.ElapsedMilliseconds} ms");
	}

	// todo: optimize
	private int DataLengthToRank(int length)
	{
		var rank = 0;
		var maxLength = fileMaxLength;
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