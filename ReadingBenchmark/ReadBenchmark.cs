using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading.Channels;

namespace FileGenerator.ReadingBenchmark;

public class ReadBenchmark(
	int bufferSize,
	int chunkSize = 5 * 1024,
	int workerCount = 2,
	int lineMaxLength = 10 + 2 + 100 + 1)
{
	private Channel<Memory<char>> _channel = Channel.CreateBounded<Memory<char>>(
		new BoundedChannelOptions(8)
		{
			SingleWriter = true
		});

	//private const int ChunkSize = 256 * 1024 * 1024;
	//private const int BufferSize = 1024 * 1024;

	//

	public async Task ReadToChannelSync(string fileName = "test.txt")
	{
		_channel = Channel.CreateBounded<Memory<char>>(
			new BoundedChannelOptions(workerCount)
			{
				SingleWriter = true
			});
		
		var tasks = Enumerable
			.Range(0, workerCount)
			.Select(_ => Task.Run(async () =>
			{
				Console.WriteLine($"worker started, thread number {Environment.CurrentManagedThreadId}");
				await foreach (var chunk in _channel.Reader.ReadAllAsync())
				{
					// sort
					// write
				}
				Console.WriteLine($"worker completed reading channel {Environment.CurrentManagedThreadId}");
			})).ToList();

		tasks.Add(Task.Run(async () =>
		{
			Console.WriteLine("Started reading");

			var charArrayPool = ArrayPool<char>.Shared;
			var chunk = charArrayPool.Rent(chunkSize);
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
			var charsRead = 0;
			var bufferOffset = 0;

			do
			{
				charsRead = reader.Read(buffer, 0, buffer.Length);

				if (chunkLen + charsRead > chunkSize - lineMaxLength)
				{
					// complete last line
					if (chunk[chunkLen - 1] != '\n')
					{
						// what if -1?
						var lineEndIndex = buffer.IndexOf('\n');
						bufferOffset = lineEndIndex + 1;
						buffer.AsSpan(0, bufferOffset).CopyTo(chunk.AsSpan(chunkLen));
						chunkLen += bufferOffset;
					}

					// flush
					await _channel.Writer.WriteAsync(chunk.AsMemory(0, chunkLen));
					chunk = charArrayPool.Rent(chunkSize);
					chunkLen = 0;
				}

				buffer.AsSpan(bufferOffset).CopyTo(chunk.AsSpan(chunkLen));
				chunkLen += charsRead - bufferOffset;

			} while (charsRead - bufferOffset > 0);

			if (chunkLen > 0)
			{
				await _channel.Writer.WriteAsync(chunk.AsMemory(0, chunkLen));
			}

			_channel.Writer.Complete();
			Console.WriteLine("Channel completed writing");
		}));
		
		await Task.WhenAll(tasks);
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