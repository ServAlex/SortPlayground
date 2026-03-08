using FluentAssertions;
using LargeFileSort.FileSorting.ChunkInputFile;

namespace LargeFileSort.Tests.Unit.FileSorting.Chunking;

public class LineComparerTests
{
	[Theory]
	[InlineData("2. apple\n", "3. apple\n")]
	[InlineData("1. apple\n", "2. banana\n")]
	[InlineData("2. apple\n", "2. banana\n")]
	[InlineData("2. apple\n", "2. apple apple\n")]
	[InlineData("2. apple\n", "2. apple pie\n")]
	[InlineData("0. \n", "1. apple\n")]
	[InlineData("1. apple apple\n", "1. apple apple apple\n")]
	[InlineData("1. apple apple apple\n", "1. apple apple banana\n")]
	public void Compare_FirstIsSmaller(string first, string second)
	{
		// arrange
		var buffer = first + second;
		var bufferArray = buffer.ToCharArray();
		var linesCount =  bufferArray.Count(b => b == '\n');
		
		var metadataRecords = new LineMetadata[linesCount];
		LineMetadata.ParseLines(buffer, ref metadataRecords);
					
		var comparer = new LineComparer(bufferArray);
		
		// act
		var ab = comparer.Compare(metadataRecords[0], metadataRecords[1]);
		var ba = comparer.Compare(metadataRecords[1], metadataRecords[0]);

		// assert
		ab.Should().BeLessThan(0);
		ba.Should().BeGreaterThan(0);
	}

	[Theory]
	[InlineData("2. apple\n", "2. apple\n")]
	[InlineData("0. \n", "0. \n")]
	public void Compare_EqualLines_ReturnsZero(string first, string second)
	{
		// arrange
		var buffer = first + second;
		var bufferArray = buffer.ToCharArray();
		var linesCount = bufferArray.Count(b => b == '\n');

		var metadataRecords = new LineMetadata[linesCount];
		LineMetadata.ParseLines(buffer, ref metadataRecords);

		var comparer = new LineComparer(bufferArray);

		// act
		var ab = comparer.Compare(metadataRecords[0], metadataRecords[1]);
		var ba = comparer.Compare(metadataRecords[1], metadataRecords[0]);

		// assert
		ab.Should().Be(0);
		ba.Should().Be(0);
	}
}