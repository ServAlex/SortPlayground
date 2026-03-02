using System.ComponentModel;
using System.Globalization;

namespace LargeFileSort.Domain;

public sealed class DataSizeTypeConverter : TypeConverter
{
	public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
		=> sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

	public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
	{
		if (value is string s)
			return DataSize.Parse(s);

		return base.ConvertFrom(context, culture, value);
	}
}