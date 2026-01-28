using System.Buffers;

namespace FileGenerator.ReadingBenchmark;

sealed class CharChunk : IDisposable
{
	public char[] Chunk { get; }
	public int Length { get; }
	public int FilledLength { get; set; }

	private readonly ArrayPool<char> _pool;
	private bool _disposed;

	public CharChunk(/*char[] buffer, */int length, ArrayPool<char> pool)
	{
		//Buffer = buffer;
		Chunk = pool.Rent(length);
		Length = length;
		_pool = pool;
		FilledLength = 0;
	}

	public Memory<char> Memory => Chunk.AsMemory(0, FilledLength);
	
	//public Span<char> Span => Chunk.AsSpan(0, Length);

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		_pool.Return(Chunk);
	}
}