namespace FileGenerator.FileSorter;

public class SortedChunk
{
	public readonly int ChunkRank;
	
	private readonly Line[] _lines;
	private readonly int _linesCount;
	private readonly CharChunk[] _subChunks;

	public SortedChunk(Line[] lines, CharChunk chunk, int chunkRank, int linesCount)
	{
		_lines = lines;
		_subChunks = [chunk];
		ChunkRank = chunkRank;
		_linesCount = linesCount;
	}

	public SortedChunk(SortedChunk chunkA,  SortedChunk chunkB)
	{
		_linesCount = chunkA._linesCount +  chunkB._linesCount;
		ChunkRank = Math.Min(chunkA.ChunkRank, chunkB.ChunkRank) - 1;
		_subChunks = [..chunkA._subChunks, ..chunkB._subChunks];
		var subChunksCountA = (short)chunkA._subChunks.Length;
		_lines = new Line[_linesCount];

		var i = 0;
		var j = 0;
		var k = 0;

		while (i < chunkA._linesCount && j < chunkB._linesCount)
		{
			ref readonly var a = ref chunkA._lines[i];
			ref readonly var b = ref chunkB._lines[j];
			
			if (Compare(a, b, chunkA._subChunks, chunkB._subChunks) < 0)
			{
				_lines[k++] = a;
				i++;
			}
			else
			{
				_lines[k++] = b with {SubChunkIndex = (short)(b.SubChunkIndex + subChunksCountA)};
				j++;
			}
		}
		
		while (i < chunkA._linesCount)
		{
			_lines[k++] = chunkA._lines[i++];
		}
		
		while (j < chunkB._linesCount)
		{
			ref readonly var b = ref chunkB._lines[j++];
			_lines[k++] = b with {SubChunkIndex = (short)(b.SubChunkIndex + subChunksCountA)};
		}
	}
	
	public void MergeToStream(SortedChunk second, StreamWriter writer)
	{
		var chunkA = this;
		var chunkB = second;
		
		var i = 0;
		var j = 0;

		while (i < chunkA._linesCount && j < chunkB._linesCount)
		{
			ref readonly var a = ref chunkA._lines[i];
			ref readonly var b = ref chunkB._lines[j];
			
			if (Compare(a, b, chunkA._subChunks, chunkB._subChunks) < 0)
			{
				i++;
				if(a.LineLength > 0)
					writer.WriteLine(chunkA._subChunks[a.SubChunkIndex].Span.Slice(a.LineOffset, a.LineLength));
			}
			else
			{
				j++;
				if(b.LineLength > 0)
					writer.WriteLine(chunkB._subChunks[b.SubChunkIndex].Span.Slice(b.LineOffset, b.LineLength));
			}
		}

		chunkA.WriteChunk(writer, i);
		chunkB.WriteChunk(writer, j);
	}
	
	public void WriteChunk(StreamWriter writer, int startIndex = 0)
	{
		for (var i = startIndex; i < _linesCount; i++)
		{
			ref readonly var line = ref _lines[i];
			if(line.LineLength > 0)
				writer.WriteLine(_subChunks[line.SubChunkIndex].Span.Slice(line.LineOffset, line.LineLength));
		}
	}
	
	public void MergeToStream_BufferedReuse(SortedChunk second, StreamWriter writer, int bufferSize)
	{
		var bufferSpan = new char[bufferSize].AsSpan();
		var bufferFilled = 0;
		var chunkA = this;
		var chunkB = second;
		
		var i = 0;
		var j = 0;

		while (i < chunkA._linesCount && j < chunkB._linesCount)
		{
			ref readonly var a = ref chunkA._lines[i];
			ref readonly var b = ref chunkB._lines[j];
			
			if (Compare(a, b, chunkA._subChunks, chunkB._subChunks) < 0)
			{
				i++;
				BufferedSpanWriteLine(
					bufferSpan, 
					writer, 
					chunkA._subChunks[a.SubChunkIndex].Span.Slice(a.LineOffset, a.LineLength), 
					ref bufferFilled);
			}
			else
			{
				j++;
				BufferedSpanWriteLine(
					bufferSpan, 
					writer, 
					chunkB._subChunks[b.SubChunkIndex].Span.Slice(b.LineOffset, b.LineLength), 
					ref bufferFilled);
			}
		}
		BufferFlush(bufferSpan, writer, ref bufferFilled);

		chunkA.WriteChunk_BufferedReuse(writer, bufferSize, i);
		chunkB.WriteChunk_BufferedReuse(writer, bufferSize, j);
	}


	public void WriteChunk_Buffered(StreamWriter writer, int startIndex = 0)
	{
		var bufferSize = _subChunks.Max(c => c.FilledLength);
		var buffer = new char[bufferSize];
		var filled = 0;
		var nl = Environment.NewLine.AsSpan();

		for (var i = startIndex; i < _linesCount; i++)
		{
			ref readonly var line = ref _lines[i];
			if (filled + line.LineLength + nl.Length >= bufferSize)
			{
				writer.Write(buffer.AsSpan(0, filled));
				filled = 0;
			}
				
			_subChunks[line.SubChunkIndex].Span.Slice(line.LineOffset, line.LineLength)
				.CopyTo(buffer.AsSpan(filled));
			filled += line.LineLength;
			nl.CopyTo(buffer.AsSpan(filled));
			filled += nl.Length;
		}

		if (filled > 0)
		{
			writer.Write(buffer.AsSpan(0, filled));
		}
	}
	
	public void WriteChunk_BufferedReuse(StreamWriter writer, int bufferSize, int startIndex = 0)
	{
		var bufferSpan = new char[bufferSize].AsSpan();
		var filled = 0;

		for (var i = startIndex; i < _linesCount; i++)
		{
			ref readonly var line = ref _lines[i];
			BufferedSpanWriteLine(bufferSpan, writer, _subChunks[line.SubChunkIndex].Span.Slice(line.LineOffset, line.LineLength), ref filled);
		}
		BufferFlush(bufferSpan, writer, ref filled);
	}

	private static void BufferedSpanWriteLine(Span<char> bufferSpan, StreamWriter writer, Span<char> contentSpan, ref int filled)
	{
		if(contentSpan.Length == 0)
			return;
		
		var nl = Environment.NewLine.AsSpan();

		if (filled + contentSpan.Length + nl.Length >= bufferSpan.Length)
		{
			writer.Write(bufferSpan[..filled]);
			filled = 0;
		}
			
		contentSpan.CopyTo(bufferSpan[filled..]);
		filled += contentSpan.Length;
		nl.CopyTo(bufferSpan[filled..]);
		filled += nl.Length;
	}

	private static void BufferFlush(Span<char> bufferSpan, StreamWriter writer, ref int filled)
	{
		if(filled > 0)
			writer.Write(bufferSpan[..filled]);
	}


	private static int Compare(Line a, Line b, CharChunk[] chunkArrayA, CharChunk[] chunkArrayB)
	{
		var prefixComparison = a.Prefix.CompareTo(b.Prefix);
		if (prefixComparison != 0)
			return prefixComparison;

		var spanA = chunkArrayA[a.SubChunkIndex] .Buffer.AsSpan(a.LineOffset + a.StringOffsetFromLine, a.StringLength);
		var spanB = chunkArrayB[b.SubChunkIndex] .Buffer.AsSpan(b.LineOffset + b.StringOffsetFromLine, b.StringLength);

		var sequenceCompare = spanA.SequenceCompareTo(spanB);
		if (sequenceCompare != 0)
			return sequenceCompare;

		return a.Number.CompareTo(b.Number);
	}
}