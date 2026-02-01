using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;

namespace FileGenerator.ReadingBenchmark;

public class ReadBenchmark(
	int bufferSize,
	int chunkSize = 1024 * 1024,
	int workerCount = 2,
	int lineMaxLength = 10 + 2 + 100 + 1,
	int empiricalConservativeLineLength = 50)
{
	private int _chunkCounter = 0; 
	
	private Channel<Memory<char>> _channel = Channel.CreateBounded<Memory<char>>(
		new BoundedChannelOptions(1)
		{
			SingleWriter = true
		});

	public async Task ReadToChannelSyncNoBuffer(string fileName = "test.txt")
	{
		var channelCapacity = workerCount;
		var channel = Channel.CreateBounded<CharChunk>(
			new BoundedChannelOptions(channelCapacity)
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
				.Select(_ => Task.Run(async () => await SorterAsync(channel, channelCapacity)))
			);
		
		await Task.WhenAll(tasks);
	}

	private async Task ReadAsync(string fileName, Channel<CharChunk> channel)
	{
		Console.WriteLine("Started reading");

		var charArrayPool = ArrayPool<char>.Shared;

		using var reader = new StreamReader(
			fileName,
			Encoding.UTF8,
			true,
			new FileStreamOptions
			{
				BufferSize = bufferSize,
				Options = FileOptions.SequentialScan
			});

		var chunk = new CharChunk(chunkSize, charArrayPool);
		var isReadToEnd = false;

		do
		{
			var charsRead = reader.Read(chunk.Span[chunk.StartOffset..]);
			isReadToEnd = chunk.StartOffset + charsRead < chunkSize;
				
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
			var newChunk = new CharChunk(chunkSize, charArrayPool);
			if (chunk.FilledLength < chunkSize)
			{
				newChunk.StartOffset = chunkSize - chunk.FilledLength;
				chunk.Span[(lineEndIndex+1)..].CopyTo(newChunk.Span[..newChunk.StartOffset]);
			}
			chunk = newChunk;

		} while (!isReadToEnd);

		channel.Writer.Complete();
		Console.WriteLine("Channel completed writing");
	}

	private async Task SorterAsync(Channel<CharChunk> channel, int channelCapacity)
	{
		await foreach (var chunk in channel.Reader.ReadAllAsync())
		{
			using (chunk)
			{
				Stopwatch sw = Stopwatch.StartNew();
				// parse
				var estimatedLines = chunk.FilledLength / empiricalConservativeLineLength;
				var records = new Line[estimatedLines];
						
				var count = ParseLines(chunk.Span[..chunk.FilledLength], ref records);
						
				// sort
				var comparer = new LineComparer(chunk.Chunk);
				Array.Sort(records, 0, count, comparer);
						
				// write
				var sb = new StringBuilder($"sorted chunk with {count} lines in {sw.ElapsedMilliseconds} ms, queue {channel.Reader.Count}/{channelCapacity}");
				sw.Restart();
						
				using var writer = new StreamWriter(
					Path.Combine("Chunks", $"chunk_{_chunkCounter:00000}.txt"),
					Encoding.UTF8, 
					new FileStreamOptions
					{
						BufferSize = 1 << 22, 
						Mode = FileMode.Create, 
						Access = FileAccess.Write
					});
				Interlocked.Increment(ref _chunkCounter);
						
				foreach (var line in records)
				{
					writer.WriteLine(chunk.Span.Slice(line.LineOffset, line.LineLength));
				}
				Console.WriteLine(sb.Append($", file written in {sw.ElapsedMilliseconds} ms"));
			}
		}
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
			r.StringOffset = textStart;
			r.LineLength = (short)lineLength;
			r.StringLength = (short)textLength;
			r.Prefix = Line.EncodeAscii8(data.Slice(textStart, textLength));

			i++; // '\n'
		}

		return count;
	}	
}