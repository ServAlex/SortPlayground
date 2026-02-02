namespace FileGenerator.FileSorter;

public class SortedChunk
{
	private readonly Line[] _lines;
	public readonly int LinesCount;
	public int ChunkRank;
	private readonly CharChunk[] _subChunks;

	public SortedChunk(Line[] lines, CharChunk chunk, int chunkRank, int linesCount)
	{
		_lines = lines;
		_subChunks = [chunk];
		ChunkRank = chunkRank;
		LinesCount = linesCount;
	}

	public SortedChunk(SortedChunk chunkA,  SortedChunk chunkB)
	{
		LinesCount = chunkA.LinesCount +  chunkB.LinesCount;
		ChunkRank = Math.Min(chunkA.ChunkRank, chunkB.ChunkRank) - 1;
		_subChunks = [..chunkA._subChunks, ..chunkB._subChunks];
		var chunksOffset = (short)chunkA._subChunks.Length;
		_lines = new Line[LinesCount];

		var i = 0;
		var j = 0;
		var k = 0;

		while (i < chunkA.LinesCount && j < chunkB.LinesCount)
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
				_lines[k++] = b with {ChunkIndex = (short)(b.ChunkIndex + chunksOffset)};
				j++;
			}
		}
	}

	public void WriteOnMerge(SortedChunk second, StreamWriter stream, int bufferSize)
	{
		var buffer = new char[bufferSize];
		var bufferSpan = buffer.AsSpan();
		var offset = 0;
		var chunkA = this;
		var chunkB = second;
		//var chunksOffset = (short)chunkA.subChunks.Length;
		
		var i = 0;
		var j = 0;
		var k = 0;

		while (i < chunkA.LinesCount && j < chunkB.LinesCount)
		{
			ref readonly var a = ref chunkA._lines[i];
			ref readonly var b = ref chunkB._lines[j];
			//Line currentLine;
			
			if (Compare(a, b, chunkA._subChunks, chunkB._subChunks) < 0)
			{
				i++;
				stream.WriteLine(chunkA._subChunks[a.ChunkIndex].Span.Slice(a.LineOffset, a.LineLength));
				
				/*
				todo: write to bubber and flush it
				 
				currentLine = a;
				
				stream.WriteLine(
				   	chunkA.subChunks[currentLine.ChunkIndex].Span
				   		.Slice(currentLine.LineOffset, currentLine.LineLength));
				 
				if (offset + currentLine.LineLength < bufferSize)
				{
					//stream.WriteLine(chunk.Span.Slice(line.LineOffset, line.LineLength));
				}
				*/
			}
			else
			{
				j++;
				stream.WriteLine(chunkB._subChunks[b.ChunkIndex].Span.Slice(b.LineOffset, b.LineLength));
			}
		}

		chunkA.WriteChunk(stream, i);
		chunkB.WriteChunk(stream, j);
	}

	public void WriteChunk(StreamWriter writer, int startIndex = 0)
	{
		for (var i = startIndex; i < LinesCount; i++)
		{
			ref readonly var line = ref _lines[i];
			writer.WriteLine(_subChunks[line.ChunkIndex].Span.Slice(line.LineOffset, line.LineLength));
		}
	}

	private static int Compare(Line a, Line b, CharChunk[] chunkArrayA, CharChunk[] chunkArrayB)
	{
		var prefixComparison = a.Prefix.CompareTo(b.Prefix);
		if (prefixComparison != 0)
			return prefixComparison;

		var spanA = chunkArrayA[a.ChunkIndex] .Buffer.AsSpan(a.LineOffset + a.StringOffsetFromLine, a.StringLength);
		var spanB = chunkArrayB[b.ChunkIndex] .Buffer.AsSpan(b.LineOffset + b.StringOffsetFromLine, b.StringLength);

		var sequenceCompare = spanA.SequenceCompareTo(spanB);
		if (sequenceCompare != 0)
			return sequenceCompare;

		return a.Number.CompareTo(b.Number);
	}
}