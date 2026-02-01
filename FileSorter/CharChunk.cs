using System.Buffers;

namespace FileGenerator.FileSorter;

public sealed class CharChunk : IDisposable
{
	public char[] Buffer { get; }
	public int StartOffset { get; set; }
	public int FilledLength { get; set; }

	private readonly int _length;
	private readonly ArrayPool<char> _pool;

	public CharChunk(int length, ArrayPool<char> pool)
	{
		_length = length;
		_pool = pool;
		Buffer = _pool.Rent(_length);
		FilledLength = 0;
	}

	public Span<char> Span => Buffer.AsSpan(0, _length);

	public void Dispose()
	{
		_pool.Return(Buffer);
	}
}