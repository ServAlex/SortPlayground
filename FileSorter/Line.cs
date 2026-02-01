namespace FileGenerator.FileSorter;

/// <summary>
/// represents single line in a char[] buffer
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
	public short ChunkIndex;
	public int Number;

	public static long EncodeAscii8(ReadOnlySpan<char> s)
	{
		long v = 0;
		int len = Math.Min(8, s.Length);

		for (var i = 0; i < len; i++)
			v = (v << 8) | (byte)s[i];

		return v << ((8 - len) * 8);
	}
}

