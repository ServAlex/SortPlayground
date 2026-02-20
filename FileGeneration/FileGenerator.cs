using System.Diagnostics;
using System.Text;
using LargeFileSort.Configurations;
using LargeFileSort.FileSorting;
using Microsoft.Extensions.Options;

namespace LargeFileSort.FileGeneration;

public class FileGenerator
{
	private readonly FileGenerationOptions _fileGenerationOptions;
	private readonly PathOptions _pathOptions;
	private readonly FileProgressLogger _logger;

	public FileGenerator(IOptions<FileGenerationOptions> fileGenerationOptions, IOptions<PathOptions> pathOptions, FileProgressLogger logger)
	{
		_logger = logger;
		_fileGenerationOptions = fileGenerationOptions.Value;
		_pathOptions = pathOptions.Value;
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
		
		if (!Directory.Exists(_pathOptions.FilesLocation))
		{
			Directory.CreateDirectory(_pathOptions.FilesLocation);
		}
		
		var filePath = Path.Combine(_pathOptions.FilesLocation, _pathOptions.UnsortedFileName);
		var desiredFileSize = (long)_fileGenerationOptions.FileSizeGb * 1024 * 1024 * 1024;

		if (_fileGenerationOptions.Reuse 
		    && File.Exists(filePath) 
		    && (double)Math.Abs(new FileInfo(filePath).Length - desiredFileSize) / desiredFileSize < 0.01)
		{
			Console.WriteLine($"File {_pathOptions.UnsortedFileName} already exists, it's size is within 1% of desired, reusing it");
			return;
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
		Console.WriteLine($"Generating {_pathOptions.UnsortedFileName} file, size {_fileGenerationOptions.FileSizeGb} GB");
		
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
		Console.WriteLine($"File generated with length {writer.BaseStream.Length / 1024 / 1024 } MB in {sw.ElapsedMilliseconds/1000.0:F1} s");
		Console.WriteLine();
	}
}