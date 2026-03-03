using FluentAssertions;
using LargeFileSort.Domain;

namespace LargeFileSort.Tests.Unit;

public class DataSizeTests
{
	[Theory]
	[InlineData("1KB", 1024)]
	[InlineData("1MB", 1024 * 1024)]
	[InlineData("2GB", 2L * 1024 * 1024 * 1024)]
	public void Parse_ShouldConvertToBytes(string input, long expectedBytes)
	{
		var result = DataSize.Parse(input);

		result.Bytes.Should().Be(expectedBytes);
	}

	[Theory]
	[InlineData("")]
	[InlineData("abc")]
	[InlineData("-10mb")]
	[InlineData("10XB")]
	public void Parse_InvalidFormat_ShouldThrow(string input)
	{
		Action act = () => DataSize.Parse(input);

		act.Should().Throw<FormatException>();
	}
	
	[Theory]
	[InlineData("1024kb", "1mb")]
	[InlineData("1024", "1kb")]
	public void Compare_ShouldWorkAcrossUnits(string textA, string textB)
	{
		var sizeA = DataSize.Parse(textA);
		var sizeB = DataSize.Parse(textB);

		sizeA.Should().Be(sizeB);
	}
}