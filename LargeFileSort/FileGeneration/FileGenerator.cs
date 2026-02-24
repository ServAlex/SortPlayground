using System.Diagnostics;
using System.Text;
using LargeFileSort.Configurations;
using LargeFileSort.Infrastructure;
using LargeFileSort.Logging;
using Microsoft.Extensions.Options;

namespace LargeFileSort.FileGeneration;

public class FileGenerator
{
	private readonly FileGenerationOptions _fileGenerationOptions;
	private readonly GeneralOptions _generalOptions;
	private readonly LiveProgressLogger _logger;
	private readonly IFileSystem _fileSystem;

	public FileGenerator(
		IOptions<FileGenerationOptions> fileGenerationOptions, 
		IOptions<GeneralOptions> pathOptions, 
		LiveProgressLogger logger, 
		IFileSystem fileSystem)
	{
		_logger = logger;
		_fileSystem = fileSystem;
		_fileGenerationOptions = fileGenerationOptions.Value;
		_generalOptions = pathOptions.Value;
	}

	private const int StringPartMaxLength = 100;
	private const int BatchSize = 512;

	private static readonly string[] Words = [
		"berry", 
		"apple", 
		"banana", 
		"cherry", 
		"date", 
		"fig", 
		"grape", 
		"kiwi", 
		"lemon", 
		"mango", 
		"orange", 
		"pear", 
		"strawberry", 
		"watermelon", 
		"yogurt", 
		"zucchini", 
		"pineapple", 
		"peach"
	];
	
	// SingleThreadedBatched
	public void GenerateFile()
	{
		if (!_fileGenerationOptions.Enabled)
		{
			Console.WriteLine("Generation is not enabled in options, skipping");
			return;
		}
		
		if (!Directory.Exists(_generalOptions.FilesLocation))
		{
			Directory.CreateDirectory(_generalOptions.FilesLocation);
		}
		
		var filePath = Path.Combine(_generalOptions.FilesLocation, _generalOptions.UnsortedFileName);
		var desiredFileSize = (long)_fileGenerationOptions.FileSizeGb * 1024 * 1024 * 1024;

		if (_fileGenerationOptions.Reuse 
		    && File.Exists(filePath) 
		    && (double)Math.Abs(new FileInfo(filePath).Length - desiredFileSize) / desiredFileSize < 0.01)
		{
			Console.WriteLine($"File {_generalOptions.UnsortedFileName} already exists, " +
			                  $"it's size is within 1% of desired, reusing it");
			return;
		}
		
		if (!_fileSystem.HasEnoughFreeSpace(_generalOptions.FilesLocation, desiredFileSize))
		{
			throw new IOException($"Not enough free space on disk to create {_generalOptions.UnsortedFileName} file, " +
			                      $"you may reduce --sizeGb in options - generate smaller input file, " +
			                      $"keep in mind sort will require 2 times more free space than this file size");
		}
		
		using var writer = new StreamWriter(
			filePath,
			Encoding.UTF8, 
			new FileStreamOptions
			{
				BufferSize = 1 << 22, 
				Mode = FileMode.Create, 
				Access = FileAccess.Write
			});
		
		var sw = Stopwatch.StartNew();
		Console.WriteLine($"Generating {_generalOptions.UnsortedFileName} file, " +
		                  $"size {_fileGenerationOptions.FileSizeGb} GB");
		
		var loggerCancellationTokenSource = new CancellationTokenSource();
		// ReSharper disable once MethodSupportsCancellation
		Task.Run(() => _logger.LogState(DateTime.Now, null, loggerCancellationTokenSource.Token));

		var random = new Random();
		var stringBuilder = new StringBuilder((StringPartMaxLength + 20) * BatchSize);
		
		while (writer.BaseStream.Length < desiredFileSize)
		{
			stringBuilder.Clear();

			for (var i = 0; i < BatchSize; i++)
			{
				stringBuilder.Append(random.Next()).Append(". ");
				
				var stringPartSoftLength = random.Next(StringPartMaxLength);
				var nextWord = Words[random.Next(Words.Length)];
				var stringLength = nextWord.Length;
				stringBuilder.Append(nextWord);
				nextWord = Words[random.Next(Words.Length)];
				
				while(stringLength + nextWord.Length <= stringPartSoftLength)
				{
					stringBuilder.Append(' ').Append(nextWord);
					stringLength += nextWord.Length + 1;
					nextWord = Words[random.Next(Words.Length)];
				}
				stringBuilder.AppendLine();
			}
				
			writer.Write(stringBuilder);
			_logger.BytesWritten += stringBuilder.Length;
		}
		
		loggerCancellationTokenSource.Cancel();
		Console.WriteLine();
		Console.WriteLine($"File generated with length {writer.BaseStream.Length / 1024 / 1024 } MB " +
		                  $"in {sw.ElapsedMilliseconds/1000.0:F1} s");
		Console.WriteLine();
	}
}