using LargeFileSort.Logging;

namespace LargeFileSort.FileSorting.ChunkInputFile;

public class SortedChunk
{
	public readonly int ChunkRank;
	
	private readonly LineMetadata[] _metadataRecords;
	private readonly int _linesCount;
	private readonly UnsortedChunk[] _subChunks;

	public SortedChunk(LineMetadata[] metadataRecords, UnsortedChunk chunk, int chunkRank, int linesCount)
	{
		_metadataRecords = metadataRecords;
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
		_metadataRecords = new LineMetadata[_linesCount];

		var i = 0;
		var j = 0;
		var k = 0;

		while (i < chunkA._linesCount && j < chunkB._linesCount)
		{
			ref readonly var a = ref chunkA._metadataRecords[i];
			ref readonly var b = ref chunkB._metadataRecords[j];
			
			if (Compare(a, b, chunkA._subChunks, chunkB._subChunks) < 0)
			{
				_metadataRecords[k++] = a;
				i++;
			}
			else
			{
				_metadataRecords[k++] = b with {SubChunkIndex = (short)(b.SubChunkIndex + subChunksCountA)};
				j++;
			}
		}
		
		while (i < chunkA._linesCount)
		{
			_metadataRecords[k++] = chunkA._metadataRecords[i++];
		}
		
		while (j < chunkB._linesCount)
		{
			ref readonly var b = ref chunkB._metadataRecords[j++];
			_metadataRecords[k++] = b with {SubChunkIndex = (short)(b.SubChunkIndex + subChunksCountA)};
		}
	}

	public void MergeToStream(SortedChunk secondChunk, StreamWriter writer, LiveProgressLogger logger)
	{
		var chunkA = this;
		var chunkB = secondChunk;
		
		var i = 0;
		var j = 0;

		var initialBytesWritten = logger.BytesWritten;
		
		while (i < chunkA._linesCount && j < chunkB._linesCount)
		{
			ref readonly var a = ref chunkA._metadataRecords[i];
			ref readonly var b = ref chunkB._metadataRecords[j];
			
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

			if ((i + j) % 500_000 == 0)
			{
				logger.BytesWritten = initialBytesWritten + writer.BaseStream.Position;
			}
		}

		chunkA.WriteChunk(writer, logger, i);
		chunkB.WriteChunk(writer, logger, j);
	}

	public void WriteChunk(StreamWriter writer, LiveProgressLogger logger, int startIndex = 0)
	{
		var initialBytesWritten = logger.BytesWritten;
		for (var i = startIndex; i < _linesCount; i++)
		{
			ref readonly var line = ref _metadataRecords[i];
			if(line.LineLength > 0)
				writer.WriteLine(_subChunks[line.SubChunkIndex].Span.Slice(line.LineOffset, line.LineLength));

			if (i % 500_000 == 0)
			{
				logger.BytesWritten = initialBytesWritten + writer.BaseStream.Position;
			}
		}
	}

	private static int Compare(LineMetadata a, LineMetadata b, UnsortedChunk[] chunkArrayA, UnsortedChunk[] chunkArrayB)
	{
		var prefixComparison = a.Prefix.CompareTo(b.Prefix);
		if (prefixComparison != 0)
			return prefixComparison;

		var spanA = chunkArrayA[a.SubChunkIndex].Buffer.AsSpan(a.LineOffset + a.StringOffsetInLine, a.StringLength);
		var spanB = chunkArrayB[b.SubChunkIndex].Buffer.AsSpan(b.LineOffset + b.StringOffsetInLine, b.StringLength);

		var sequenceCompare = spanA.SequenceCompareTo(spanB);
		if (sequenceCompare != 0)
			return sequenceCompare;

		return a.Number.CompareTo(b.Number);
	}
}