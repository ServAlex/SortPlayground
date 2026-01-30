namespace FileGenerator.ReadingBenchmark;

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
	public int StringOffset;
	public short LineLength;
	public short StringLength;
	public int Number;

	public Line(int lineOffset, ReadOnlySpan<char> s)
	{
		LineOffset = lineOffset;
		LineLength = (short)s.Length;
		
		var dotIndex = s.IndexOf('.');
		Number = int.Parse(s[..dotIndex]);

		StringOffset = lineOffset + dotIndex + 2;
		StringLength = (short)(LineLength - dotIndex - 2);
		
		Prefix = EncodeAscii8(s[(dotIndex + 2)..]);
	}
	
	public static int Compare(Line a, Line b, char[] buffer)
	{
		var prefixComparison = a.Prefix.CompareTo(b.Prefix);
		if (prefixComparison != 0)
			return prefixComparison;

		var spanA = buffer.AsSpan(a.StringOffset, a.StringLength);
		var spanB = buffer.AsSpan(b.StringOffset, b.StringLength);

		var sequenceCompare = spanA.SequenceCompareTo(spanB);
		if (sequenceCompare != 0)
			return sequenceCompare;

		return a.Number.CompareTo(b.Number);
	}

	public static long EncodeAscii8(ReadOnlySpan<char> s)
	{
		long v = 0;
		int len = Math.Min(8, s.Length);

		for (var i = 0; i < len; i++)
			v = (v << 8) | (byte)s[i];

		return v << ((8 - len) * 8);
	}
}

