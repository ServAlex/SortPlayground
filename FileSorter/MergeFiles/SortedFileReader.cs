namespace FileGenerator.FileSorter.MergeFiles;

public class SortedFileReader
{
	public string? LineString;
	public Line Line;
	public StreamReader StreamReader;
	public bool EndOfFile => LineString is null;

	public SortedFileReader(StreamReader streamReader)
	{
		StreamReader = streamReader;
		ReadLine();
	}
	
	public bool ReadLine()
	{
		LineString = StreamReader.ReadLine();
		if (LineString == null) 
			return false;
		var data = LineString.AsSpan();
		
		int i = 0;
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

		Line.Number = number;
		Line.LineOffset = lineStart;
		Line.StringOffsetFromLine = (short)(textStart - lineStart);
		Line.LineLength = (short)lineLength;
		Line.StringLength = (short)textLength;
		Line.ChunkIndex = 0;
		Line.Prefix = Line.EncodeAscii8(data.Slice(textStart, textLength));

		i++; // '\n'
		
		return true;
	}
}