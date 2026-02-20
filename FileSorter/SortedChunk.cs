namespace LargeFileSort.FileSorter;

public class SortedChunk
{
	public readonly int ChunkRank;
	
	private readonly Line[] _lines;
	private readonly int _linesCount;
	private readonly UnsortedChunk[] _subChunks;

	public SortedChunk(Line[] lines, UnsortedChunk chunk, int chunkRank, int linesCount)
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

	public long MergeToStream(SortedChunk secondChunk, StreamWriter stream, int bufferSize)
	{
		var chunkA = this;
		var chunkB = secondChunk;
		
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
					stream.WriteLine(chunkA._subChunks[a.SubChunkIndex].Span.Slice(a.LineOffset, a.LineLength));
				
				/*
				todo: write to buffer and flush it
				*/
			}
			else
			{
				j++;
				if(b.LineLength > 0)
					stream.WriteLine(chunkB._subChunks[b.SubChunkIndex].Span.Slice(b.LineOffset, b.LineLength));
			}
		}

		chunkA.WriteChunk(stream, i);
		chunkB.WriteChunk(stream, j);
		
		return stream.BaseStream.Position;
	}

	public long WriteChunk(StreamWriter writer, int startIndex = 0)
	{
		for (var i = startIndex; i < _linesCount; i++)
		{
			ref readonly var line = ref _lines[i];
			if(line.LineLength > 0)
				writer.WriteLine(_subChunks[line.SubChunkIndex].Span.Slice(line.LineOffset, line.LineLength));
		}
		return writer.BaseStream.Position;
	}

	private static int Compare(Line a, Line b, UnsortedChunk[] chunkArrayA, UnsortedChunk[] chunkArrayB)
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