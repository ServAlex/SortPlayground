using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LargeFileSort.Domain;

[TypeConverter(typeof(DataSizeTypeConverter))]
public sealed partial record DataSize
{
	public long Bytes { get; }

	private DataSize(long bytes)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytes);
		Bytes = bytes;
	}

	public static DataSize FromBytes(long bytes) => new(bytes);

	public static implicit operator long(DataSize size) => size.Bytes;

	public static bool TryParse(string? input, out DataSize result)
	{
		result = null!;
		
		if (string.IsNullOrWhiteSpace(input))
			return false;
		
		input = input.Trim().ToLowerInvariant();

		var match = StringValueRegex().Match(input);
		if (!match.Success)
		{
			return false;
		}

		if (!long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
		{
			return false;
		}
		var unit = match.Groups[2].Value;

		long multiplier;
		try
		{
			multiplier = Multiplier(unit);
		}
		catch (FormatException)
		{
			return false;
		}

		if (value <= 0)
		{
			return false;
		}

		try
		{
			var bytes = checked(value * multiplier);
			result = new DataSize(bytes);
			return true;
		}
		catch (OverflowException)
		{
			return false;
		}
	}
	
	public static DataSize Parse(string input)
	{
		return TryParse(input, out var result) ? result : throw new FormatException($"Invalid size: '{input}'");
	}
	
	public override string ToString() => $"{Bytes} bytes";

	public string ToDataSizeString(string multiplierUnit)
	{
		var multiplier = Multiplier(multiplierUnit);
		return $"{Bytes / multiplier} {multiplierUnit}";
	}

	private static long Multiplier(string multiplierUnit)
	{
		return multiplierUnit.Trim().ToLowerInvariant() switch
		{
			"kb" => 1024L,
			"mb" => 1024L * 1024,
			"gb" => 1024L * 1024 * 1024,
			""   => 1L,
			_    => throw new FormatException($"Unknown unit '{multiplierUnit}'")
		};
	}

	[GeneratedRegex(@"^(\d+)(kb|mb|gb)?$")]
	private static partial Regex StringValueRegex();
}