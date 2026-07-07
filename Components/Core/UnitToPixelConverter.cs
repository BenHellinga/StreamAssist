using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace StreamAssist.Components.Keyboard;

/// <summary>
/// Converts a key's integer "width units" to actual pixel width.
/// 1 unit = 7px, plus 4px to compensate for the 2px margin on each side.
/// </summary>
public class UnitToPixelConverter : IValueConverter
{
    public static readonly UnitToPixelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int units ? units * 7.0 + 4.0 : AvaloniaProperty.UnsetValue;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
