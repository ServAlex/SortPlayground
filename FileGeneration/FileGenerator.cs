using System.Diagnostics;
using System.Text;
using LargeFileSort.Configurations;
using LargeFileSort.FileSorter;
using Microsoft.Extensions.Options;

namespace LargeFileSort.FileGeneration;

public class FileGenerator(IOptions<FileGenerationOptions> fileGenerationOptions, IOptions<PathOptions> pathOptions, FileProgressLogger logger)
{
	private readonly FileGenerationOptions _fileGenerationOptions = fileGenerationOptions.Value;
	private readonly PathOptions _pathOptions = pathOptions.Value;

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
	
	public void GenerateFileSingleThreadedBatched()
	{
		using var writer = new StreamWriter(
			Path.Combine(_pathOptions.FilesLocation, _pathOptions.UnsortedFileName),
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
		Task.Run(() => logger.LogState(DateTime.Now, null, loggerCancellationTokenSource.Token));

		var random = new Random();
		var stringBuilder = new StringBuilder((StringPartMaxLength + 20) * BatchSize);
		
		while (writer.BaseStream.Length < (long)_fileGenerationOptions.FileSizeGb * 1024 * 1024 * 1024)
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
			logger.BytesWritten += stringBuilder.Length;
		}
		
		loggerCancellationTokenSource.Cancel();
		Console.WriteLine($"File generated with length {writer.BaseStream.Length / 1024 / 1024 } MB in {sw.ElapsedMilliseconds/1000} s");
	}
}