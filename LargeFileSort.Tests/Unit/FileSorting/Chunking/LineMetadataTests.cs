using FluentAssertions;
using LargeFileSort.FileSorting.ChunkInputFile;

namespace LargeFileSort.Tests.Unit.FileSorting.Chunking;

public class LineMetadataTests
{
	[Theory]
	[InlineData("2. apple apple\n", 1, new []{2}, new []{"apple apple"})]
	[InlineData("2. apple apple\r\n", 1, new []{2}, new []{"apple apple"})]
	[InlineData("2. apple apple", 1, new []{2}, new []{"apple apple"})]
	[InlineData("2. apple apple\r\n53. pear pear\r", 2, 
		new []{2, 53}, new []{"apple apple", "pear pear"})]
	[InlineData("  2   .      apple apple\n", 1, new []{2}, new []{"apple apple"})]
	[InlineData("2apple apple\n", 1, new []{2}, new []{"apple apple"})]
	[InlineData("apple apple\n", 1, new []{0}, new []{"apple apple"})]
	[InlineData("2.\n", 1, new []{2}, new []{""})]
	[InlineData("\n", 1, new []{0}, new []{""})]
	public void ParseLines_WithValidInput_ShouldExtractValidMetadata(
		string input, int linesCount, int[] expectedNumbers, string[] expectedTexts)
	{
		// arrange
		var metadataRecords = new LineMetadata[linesCount];
		
		// act
		LineMetadata.ParseLines(input.AsSpan(), ref metadataRecords);
		
		//assert
		metadataRecords.Should().HaveCount(linesCount);
		
		metadataRecords
			.Select(r => r.Number)
			.ToList()
			.Should()
			.Equal(expectedNumbers);
		
		metadataRecords
			.Select(r => input.Substring(r.LineOffset + r.StringOffsetInLine, r.StringLength))
			.ToList()
			.Should()
			.Equal(expectedTexts);
	}
	
	[Theory]
	[InlineData("")]
	[InlineData(".")]
	[InlineData("....")]
	[InlineData("9999999999999999999999999999999999999.")]
	[InlineData("..\n..\r\n..\r")]
	[InlineData("\0\0\0")]
	public void ParseLines_ShouldNeverThrow(string input)
	{
		// arrange
		var metadataRecords = new LineMetadata[10];
		var action = () => LineMetadata.ParseLines(input.AsSpan(), ref metadataRecords);

		// act and assert
		action.Should().NotThrow();
	}
	
	[Theory]
	[InlineData("2. apple\n53. pear")]
	[InlineData("random text")]
	[InlineData("\n\n\n")]
	public void ParseLines_ShouldNotProduceInvalidOffsets(string input)
	{
		// arrange 
		var metadataRecords = new LineMetadata[10];
		
		// act
		LineMetadata.ParseLines(input.AsSpan(), ref metadataRecords);

		// assert
		foreach (var r in metadataRecords)
		{
			(r.LineOffset + r.StringOffsetInLine + r.StringLength)
				.Should()
				.BeLessThanOrEqualTo(input.Length);

			r.LineOffset.Should().BeGreaterThanOrEqualTo(0);
			r.StringOffsetInLine.Should().BeGreaterThanOrEqualTo(0);
			r.StringLength.Should().BeGreaterThanOrEqualTo(0);
		}
	}
}