using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using LogReader.Models;

namespace LogReader.Converters;

/// <summary>Maps a log level to its accent brush for the level pill / row text.</summary>
public sealed class LevelToBrushConverter : IValueConverter
{
    public static readonly SolidColorBrush Trace = New("#7D8590");
    public static readonly SolidColorBrush Debug = New("#56A8F5");
    public static readonly SolidColorBrush Info  = New("#3FB950");
    public static readonly SolidColorBrush Warn  = New("#D29922");
    public static readonly SolidColorBrush Error = New("#F85149");
    public static readonly SolidColorBrush Fatal = New("#FF6BD6");
    public static readonly SolidColorBrush Unknown = New("#7D8590");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = value switch
        {
            LogLevel lv => lv,
            string s => LogLevels.Parse(s),
            LogEntry e => e.LevelValue,
            _ => LogLevel.Unknown
        };
        return level switch
        {
            LogLevel.Trace => Trace,
            LogLevel.Debug => Debug,
            LogLevel.Info => Info,
            LogLevel.Warn => Warn,
            LogLevel.Error => Error,
            LogLevel.Fatal => Fatal,
            _ => Unknown
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static SolidColorBrush New(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
