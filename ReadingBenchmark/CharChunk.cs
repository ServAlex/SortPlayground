using System.Buffers;

namespace FileGenerator.ReadingBenchmark;

sealed class CharChunk : IDisposable
{
	public char[] Chunk { get; }
	public int StartOffset { get; set; }
	public int FilledLength { get; set; }

	private readonly int _length;
	private readonly ArrayPool<char> _pool;

	public CharChunk(int length, ArrayPool<char> pool)
	{
		_length = length;
		_pool = pool;
		Chunk = _pool.Rent(_length);
		FilledLength = 0;
	}

	public Span<char> Span => Chunk.AsSpan(0, _length);

	public void Dispose()
	{
		_pool.Return(Chunk);
	}
}