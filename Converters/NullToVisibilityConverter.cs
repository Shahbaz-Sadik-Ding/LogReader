using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LogReader.Converters;

/// <summary>
/// Visible when value is null. Pass ConverterParameter="invert" to flip
/// (visible when value is NOT null).
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNull = value == null;
        bool invert = string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase);
        bool show = invert ? !isNull : isNull;
        return show ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
