using System.Buffers;

namespace FileGenerator.FileSorter;

public sealed class CharChunk
{
	public char[] Buffer { get; }
	public int StartOffset { get; set; }
	public int FilledLength { get; set; }

	private readonly int _length;

	public CharChunk(int length)
	{
		_length = length;
		Buffer = new char[_length];
		FilledLength = 0;
	}

	public Span<char> Span => Buffer.AsSpan(0, _length);
}