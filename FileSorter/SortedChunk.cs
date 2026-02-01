namespace FileGenerator.FileSorter;

public class SortedChunk: IDisposable
{
	private readonly FileSorter.Line[] _lines;
	public readonly int LinesCount;
	public readonly int ChunkGrade;
	private readonly CharChunk[] subChunks;

	public SortedChunk(FileSorter.Line[] lines, CharChunk chunk, int chunkGrade, int linesCount)
	{
		this._lines = lines;
		subChunks = [chunk];
		ChunkGrade = chunkGrade;
		LinesCount = linesCount;
	}

	public SortedChunk(SortedChunk chunkA,  SortedChunk chunkB, int linesCount)
	{
		LinesCount = linesCount;
		ChunkGrade = Math.Min(chunkA.ChunkGrade, chunkB.ChunkGrade) - 1;
		subChunks = [..chunkA.subChunks, ..chunkB.subChunks];
		var chunksOffset = (short)chunkA.subChunks.Length;
		_lines = new FileSorter.Line[chunkA.LinesCount +  chunkB.LinesCount];

		var i = 0;
		var j = 0;
		var k = 0;

		while (i < chunkA.LinesCount && j < chunkB.LinesCount)
		{
			ref readonly var a = ref chunkA._lines[i];
			ref readonly var b = ref chunkA._lines[j];
			
			if (Compare(a, b, subChunks, chunksOffset) < 0)
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

	private static int Compare(FileSorter.Line a, FileSorter.Line b, CharChunk[] chunkArray, int bOffset)
	{
		var prefixComparison = a.Prefix.CompareTo(b.Prefix);
		if (prefixComparison != 0)
			return prefixComparison;

		var spanA = chunkArray[a.ChunkIndex] .Buffer.AsSpan(a.LineOffset + a.StringOffsetFromLine, a.StringLength);
		var spanB = chunkArray[b.ChunkIndex + bOffset] .Buffer.AsSpan(b.LineOffset + b.StringOffsetFromLine, b.StringLength);

		var sequenceCompare = spanA.SequenceCompareTo(spanB);
		if (sequenceCompare != 0)
			return sequenceCompare;

		return a.Number.CompareTo(b.Number);
	}
	
	public void Dispose()
	{
		throw new NotImplementedException();
	}
}