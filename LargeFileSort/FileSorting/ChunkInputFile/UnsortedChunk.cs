namespace LargeFileSort.FileSorting.ChunkInputFile;

public sealed class UnsortedChunk
{
	public char[] Buffer { get; }
	
	/// <summary>
	/// represents how long is the first part of the line that was copied from the previous chunk to the beginning of this one
	/// </summary>
	public int StartOffset { get; set; }
	
	/// <summary>
	/// relevant data length in the buffer, defined by the last line break, used to exclude incomplete line in the end
	/// </summary>
	public int FilledLength { get; set; }

	private readonly int _length;

	public UnsortedChunk(int length)
	{
		_length = length;
		Buffer = new char[_length];
		FilledLength = 0;
	}

	public Span<char> Span => Buffer.AsSpan(0, _length);
}