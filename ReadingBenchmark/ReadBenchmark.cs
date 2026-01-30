using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Channels;

namespace FileGenerator.ReadingBenchmark;

public class ReadBenchmark(
	int bufferSize,
	int chunkSize = 1024 * 1024,
	int workerCount = 2,
	int lineMaxLength = 10 + 2 + 100 + 1)
{
	private Channel<Memory<char>> _channel = Channel.CreateBounded<Memory<char>>(
		new BoundedChannelOptions(1)
		{
			SingleWriter = true
		});

	public async Task ReadToChannelSyncNoBuffer(string fileName = "test.txt")
	{
		var channel = Channel.CreateBounded<CharChunk>(
			new BoundedChannelOptions(workerCount)
			{
				SingleWriter = true
			});

		long sizeCounter = 0;
		long readCounter = 0;
		long sentCounter = 0;
		int chunkCounter = 0;
		
		//File.Delete("compiled.txt");
		if (Directory.Exists("Chunks"))
		{
			Directory.Delete("Chunks", true);
		}
		
		Directory.CreateDirectory("Chunks");
		
		// worker tasks
		var tasks = Enumerable
			.Range(0, workerCount)
			.Select(_ => Task.Run(async () =>
			{
				await foreach (var chunk in channel.Reader.ReadAllAsync())
				{
					using (chunk)
					{
						Stopwatch sw = Stopwatch.StartNew();
						Interlocked.Add(ref sizeCounter, chunk.FilledLength);
						// parse
						var estimatedLines = chunk.FilledLength / 50;
						var records = new Line[estimatedLines];
						
						var count = ParseLines(chunk.Span[..chunk.FilledLength], ref records);
						
						// sort
						var comparer = new LineComparer(chunk.Chunk);
						Array.Sort(records, 0, count, comparer);
						
						// write
						var sb = new StringBuilder($"sorted chunk with {count} lines in {sw.ElapsedMilliseconds} ms");
						sw.Restart();
						
						using var writer = new StreamWriter(
							Path.Combine("Chunks", $"chunk_{chunkCounter:00000}.txt"),
							Encoding.UTF8, 
							new FileStreamOptions
							{
								BufferSize = 1 << 22, 
								Mode = FileMode.Create, 
								Access = FileAccess.Write
							});
						Interlocked.Increment(ref chunkCounter);
						
						foreach (var line in records)
						{
							writer.WriteLine(chunk.Span.Slice(line.LineOffset, line.LineLength));
						}
						Console.WriteLine(sb.Append($", file written in {sw.ElapsedMilliseconds} ms"));
					}
				}
			})).ToList();

		// file reader task
		tasks.Add(Task.Run(async () =>
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
				readCounter += charsRead;
				isReadToEnd = chunk.StartOffset + charsRead < chunkSize;
				
				var lineEndIndex = chunk.Span.LastIndexOf('\n');
				if (isReadToEnd)
				{
					lineEndIndex = charsRead + chunk.StartOffset - 1;
				}
				
				chunk.FilledLength = lineEndIndex + 1;
				await channel.Writer.WriteAsync(chunk);
				sentCounter+=chunk.FilledLength;
				
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
		}));
		
		await Task.WhenAll(tasks);
		Console.WriteLine($"Channel completed async file read {readCounter} b, read in channel {sizeCounter} b");
	}

	public async Task ReadToChannelSync(string fileName = "test.txt")
	{
		var channel = Channel.CreateBounded<CharChunk>(
			new BoundedChannelOptions(workerCount)
			{
				SingleWriter = true
			});
		
		
		long sizeCounter = 0;
		long readCounter = 0;
		
		var tasks = Enumerable
			.Range(0, workerCount)
			.Select(_ => Task.Run(async () =>
			{
				//Console.WriteLine($"worker started, thread number {Environment.CurrentManagedThreadId}");
				await foreach (var chunk in channel.Reader.ReadAllAsync())
				{
					using (chunk)
					{
						Stopwatch sw = new Stopwatch();
						Interlocked.Add(ref sizeCounter, chunk.FilledLength);
						// parse
						var estimatedLines = chunk.FilledLength / 50;
						var records = new Line[estimatedLines];
						
						var count = ParseLines(chunk.Span, ref records);
						
						// sort
						records.Sort();
						// write
						
						Console.WriteLine($"sorted chunk with {count} lines in {sw.ElapsedMilliseconds} ms");
					}
				}
				//Console.WriteLine($"worker completed reading channel {Environment.CurrentManagedThreadId}");
			})).ToList();

		tasks.Add(Task.Run(async () =>
		{
			Console.WriteLine("Started reading");

			var charArrayPool = ArrayPool<char>.Shared;
			var chunk = new CharChunk(chunkSize, charArrayPool);
				//PooledChunkOwner(chunkSize, charArrayPool);
			//var chunk = charArrayPool.Rent(chunkSize);
			//var chunkLen = 0;

			using var reader = new StreamReader(
				fileName,
				Encoding.UTF8,
				true,
				new FileStreamOptions
				{
					BufferSize = bufferSize,
					Options = FileOptions.SequentialScan
				});

			var buffer = new char[bufferSize];
			var charsRead = 0;
			var bufferOffset = 0;

			do
			{
				charsRead = reader.Read(buffer, 0, buffer.Length);
				readCounter += charsRead;

				if (chunk.FilledLength + charsRead > chunkSize - lineMaxLength)
				{
					// complete last line
					if (chunk.Chunk[chunk.FilledLength - 1] != '\n')
					{
						// what if -1?
						var lineEndIndex = buffer.IndexOf('\n');
						bufferOffset = lineEndIndex + 1;
						buffer.AsSpan(0, bufferOffset).CopyTo(chunk.Chunk.AsSpan(chunk.FilledLength));
						chunk.FilledLength += bufferOffset;
						//chunkLen += bufferOffset;
					}

					// flush
					await channel.Writer.WriteAsync(chunk);
					chunk = new CharChunk(chunkSize, charArrayPool);
					//chunk.AsMemory(0, chunkLen));
					//chunk = charArrayPool.Rent(chunkSize);
					//chunkLen = 0;
				}

				buffer.AsSpan(bufferOffset).CopyTo(chunk.Chunk.AsSpan(chunk.FilledLength));
				chunk.FilledLength += charsRead - bufferOffset;
				//buffer.AsSpan(bufferOffset).CopyTo(chunk.Chunk.AsSpan(chunkLen));
				//chunkLen += charsRead - bufferOffset;

			} while (charsRead - bufferOffset > 0);

			if (chunk.FilledLength > 0)
			{
				await channel.Writer.WriteAsync(chunk);
			}

			channel.Writer.Complete();
			Console.WriteLine("Channel completed writing");
		}));
		
		await Task.WhenAll(tasks);
		
		Console.WriteLine($"Channel completed async file read {readCounter}, read in channel {sizeCounter}");
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
	
	public async Task ReadToChannel(string fileName = "test.txt")
	{
		_channel = Channel.CreateBounded<Memory<char>>(
			new BoundedChannelOptions(workerCount)
			{
				SingleWriter = true
			});
		
		var readerTask = Task.Run(async () =>
		{
			await foreach (var item in _channel.Reader.ReadAllAsync())
			{
				//Console.WriteLine(item);
				//Console.WriteLine("channel read");
			}

			Console.WriteLine("Channel completed async");
		});

		
		var charArrayPool = ArrayPool<char>.Shared;
		var chunk = charArrayPool.Rent(chunkSize + lineMaxLength);
		var chunkLen = 0;

		using var reader = new StreamReader(
			fileName,
			Encoding.UTF8,
			true,
			new FileStreamOptions
			{
				BufferSize = bufferSize,
				Options = FileOptions.SequentialScan
			});

		var buffer = new char[bufferSize];
		
		//var charsRead = reader.ReadBlock(buffer, 0, buffer.Length);
		//var charsRead = reader.Read(buffer, 0, buffer.Length);
		var charsRead = 0;
		var bufferOffset = 0;

		do
		{
			charsRead = await reader.ReadAsync(buffer, 0, buffer.Length);
			//var bufferSpan = new ReadOnlySpan<char>(buffer, 0, charsRead);

			if (chunkLen + charsRead > chunkSize - lineMaxLength)
			{
				// complete last line
				if (chunk[chunkLen - 1] != '\n')
				{
					// what if -1?
					var lineEndIndex = buffer.IndexOf('\n');
					bufferOffset = lineEndIndex + 1;
					buffer.AsSpan(0, bufferOffset).CopyTo(chunk.AsSpan(chunkLen));
					//bufferSpan[..bufferOffset].CopyTo(chunk.AsSpan(chunkLen));
					chunkLen += bufferOffset;

					//bufferSpan = bufferSpan[bufferOffset..];
					//charsRead -= lineEndIndex + 1;
				}

				// flush
				await _channel.Writer.WriteAsync(chunk.AsMemory(0, chunkLen));
				chunk = charArrayPool.Rent(chunkSize);
				chunkLen = 0;
			}
/*
			bufferSpan = buffer.AsSpan(bufferOffset);
			bufferSpan.CopyTo(chunk.AsSpan(chunkLen));
			chunkLen += bufferSpan.Length;
			*/
			
			buffer.AsSpan(bufferOffset).CopyTo(chunk.AsSpan(chunkLen));
			chunkLen += charsRead - bufferOffset;
				
		} while (charsRead - bufferOffset > 0);

		if (chunkLen > 0)
		{
			await _channel.Writer.WriteAsync(chunk.AsMemory(0, chunkLen));
		}
		
		_channel.Writer.Complete();
	}
	
	public void ReadFullFile(string fileName = "text.txt")
	{
		using var reader = new StreamReader(
			fileName,
			Encoding.UTF8,
			true,
			new FileStreamOptions
			{
				BufferSize = 1 << 18,
				//Options = FileOptions.SequentialScan
			});
		
		while (!reader.EndOfStream)
		{
			var line = reader.ReadLine();
		}
	}
	
	public void ReadFileToEnd(string fileName = "text.txt")
	{
		using var reader = new StreamReader(
			fileName,
			Encoding.UTF8,
			true,
			new FileStreamOptions
			{
				BufferSize = 1 << 18,
				//Options = FileOptions.SequentialScan
			});

		reader.ReadToEnd();
	}
	
	public void ReadBlocked(string fileName = "text.txt")
	{
		//new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read));
		using var reader = new StreamReader(
			fileName,
			Encoding.UTF8,
			true,
			new FileStreamOptions
			{
				BufferSize = 1 << 18,
				//Options = FileOptions.SequentialScan
			});

		var len = reader.BaseStream.Length;
//		Console.WriteLine($"file length: {len / 1024 / 1024} MB");
		
		var buffer = new char[1024*10];
		var red = 0;
		
		while (!reader.EndOfStream)
		{
			red += reader.ReadBlock(buffer, 0, buffer.Length);
		}
		
		//reader.ReadBlock()
	}
	
	public void ReadBlockedSequentialLargeBuf(string fileName = "text.txt")
	{
		var bufferSize = 1 << 20;
		//new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read));
		using var reader = new StreamReader(
			fileName,
			Encoding.UTF8,
			true,
			new FileStreamOptions
			{
				BufferSize = bufferSize,
				Options = FileOptions.SequentialScan
			});

		var len = reader.BaseStream.Length;
		Console.WriteLine($"file length: {len / 1024 / 1024} MB");
		
		var buffer = new char[bufferSize];
		var red = 0;
		
		while (!reader.EndOfStream)
		{
			red += reader.ReadBlock(buffer, 0, buffer.Length);
		}
		
		//reader.ReadBlock()
	}
}