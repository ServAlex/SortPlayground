namespace LargeFileSort.FileSorter;

/// <summary>
/// represents a single line in a char[] buffer
/// </summary>
public struct Line
{
	/// <summary>
	/// encodes first 8 chars of the string portion for fast track in comparator
	/// </summary>
	public long Prefix;
	public int LineOffset;
	public short StringOffsetFromLine;
	public short LineLength;
	public short StringLength;
	public short SubChunkIndex;
	public int Number;

	public static long EncodeAscii8(ReadOnlySpan<char> s)
	{
		long v = 0;
		int len = Math.Min(8, s.Length);

		for (var i = 0; i < len; i++)
			v = (v << 8) | (byte)s[i];

		return v << ((8 - len) * 8);
	}

	internal static int ParseLines(ReadOnlySpan<char> data, ref Line[] lines)
	{
		int count = 0;
		int i = 0;

		while (i < data.Length)
		{
			if (count == lines.Length)
				Array.Resize(ref lines, lines.Length * 2);

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
			if (i > textStart && data[i - 1] == '\r')
			{
				lineEnd--;
			}
			
			int lineLength = lineEnd - lineStart;
			int textLength = lineEnd - textStart;

			ref var r = ref lines[count++];
			r.Number = number;
			r.LineOffset = lineStart;
			r.StringOffsetFromLine = (short)(textStart - lineStart);
			r.LineLength = (short)lineLength;
			r.StringLength = (short)textLength;
			r.SubChunkIndex = 0;
			r.Prefix = EncodeAscii8(data.Slice(textStart, textLength));

			i++; // '\n'
		}
		// todo: skip empty lines

		return count;
	}	
	
	internal static int ParseLines2(ReadOnlySpan<char> data, ref Line[] lines)
	{
		int count = 0;
		int i = 0;

		while (i < data.Length)
		{
			if (count == lines.Length)
				Array.Resize(ref lines, lines.Length * 2);

			lines[count++] = ParseLineData(data, ref i);
		}

		return count;
	}	
	
	internal static Line ParseLineData(ReadOnlySpan<char> data, ref int i)
	{
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
		if (i > textStart && data[i - 1] == '\r')
		{
			lineEnd--;
		}
		
		int lineLength = lineEnd - lineStart;
		int textLength = lineEnd - textStart;

		Line line;
		line.Number = number;
		line.LineOffset = lineStart;
		line.StringOffsetFromLine = (short)(textStart - lineStart);
		line.LineLength = (short)lineLength;
		line.StringLength = (short)textLength;
		line.SubChunkIndex = 0;
		line.Prefix = Line.EncodeAscii8(data.Slice(textStart, textLength));

		i++; // '\n'
		return line;
	}
}
