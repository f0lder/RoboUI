using System.Globalization;
using System.Windows.Data;

namespace RoboUI.Converters;

/// <summary>Binds int? view-model properties to WPF UI's NumberBox, whose Value is double?. Empty stays empty (null).</summary>
public sealed class IntToDoubleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i ? (double?)i : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double d && !double.IsNaN(d) ? (int?)(int)Math.Round(d) : null;
}
