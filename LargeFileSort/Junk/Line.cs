namespace LargeFileSort.Junk;

public record Line(long Subkey, string Key)
{
	public override string ToString()
	{
		return $"{Subkey}, {Key}";
	}
}
