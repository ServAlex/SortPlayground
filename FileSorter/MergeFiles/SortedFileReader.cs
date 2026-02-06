namespace FileGenerator.FileSorter.MergeFiles;

public class SortedFileReader
{
	public string? LineString;
	public Line Line;
	public bool EndOfFile => LineString is null;
	
	private readonly StreamReader _streamReader;

	public SortedFileReader(StreamReader streamReader)
	{
		_streamReader = streamReader;
		ReadLine();
	}
	
	public bool ReadLine()
	{
		LineString = _streamReader.ReadLine();
		if (LineString == null) 
			return false;
		
		var data = LineString.AsSpan();
		int i = 0;
		Line = Line.ParseLineData(data, ref i);

		return true;
	}
}