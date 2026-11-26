using System.Globalization;
using System.Windows.Data;

namespace RoboUI.Converters;

/// <summary>
/// Binds a group of RadioButtons to one enum property: each button passes its enum member name as
/// ConverterParameter and is checked when the property has that value.
/// </summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is string name && string.Equals(value.ToString(), name, StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Only the button being checked writes back; unchecking the others must not change the value.
        if (value is not true || parameter is not string name) return Binding.DoNothing;
        var enumType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return Enum.Parse(enumType, name);
    }
}
