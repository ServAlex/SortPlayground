using FluentAssertions;
using LargeFileSort.FileSorting.ChunkInputFile;

namespace LargeFileSort.Tests.Unit.FileSorting.Chunking;

public class SortedChunkTests
{
	[Theory]
	[InlineData("1. apple\n3. banana\n1. banana banana\n", "2. apple\n2. apple apple\n2. banana\n")]
	[InlineData("1. apple apple banana\n", "1. apple apple\n")]
	[InlineData("1. apple apple\n", "1. apple apple banana\n")]
	public void MergeConstructor_ShouldProduceSortedChunk(string bufferA, string bufferB)
	{
		// arrange
		var chunkA = new UnsortedChunk(bufferA.Length);
		bufferA.CopyTo(chunkA.Buffer);
		var linesCountA = chunkA.Buffer.Count(c => c == '\n');
		var metadataA = new LineMetadata[linesCountA];
		LineMetadata.ParseLines(chunkA.Buffer, ref metadataA);
		var comparerA = new LineComparer(chunkA.Buffer);
		Array.Sort(metadataA, 0, linesCountA, comparerA);
		var orderedChunkA = new SortedChunk(metadataA, chunkA, 0, linesCountA);
		
		var chunkB = new UnsortedChunk(bufferB.Length);
		bufferB.CopyTo(chunkB.Buffer);
		var linesCountB = chunkB.Buffer.Count(c => c == '\n');
		var metadataB = new LineMetadata[linesCountB];
		LineMetadata.ParseLines(chunkB.Buffer, ref metadataB);
		var comparerB = new LineComparer(chunkB.Buffer);
		Array.Sort(metadataB, 0, linesCountB, comparerB);
		var orderedChunkB = new SortedChunk(metadataB, chunkB, 0, linesCountB);
		
		// act
		var mergedChunk = new SortedChunk(orderedChunkA, orderedChunkB);
		
		// assert
		orderedChunkA.GetLines().Should().BeInAscendingOrder(q => q, new ComparerForShould());
		orderedChunkB.GetLines().Should().BeInAscendingOrder(q => q, new ComparerForShould());
		
		var mergedLines = mergedChunk.GetLines();
		mergedLines.Should().BeInAscendingOrder(q => q, new ComparerForShould());
	}
	
	private class ComparerForShould : IComparer<(int number, string text)>
	{
		public int Compare((int number, string text) a, (int number, string text) b)
		{
			var comparison = string.CompareOrdinal(a.text, b.text);
			if (comparison != 0) 
				return comparison;
		
			return a.number.CompareTo(b.number);
		}
	}
}