using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Attendix.Models;
using Avalonia.Data.Converters;

namespace Attendix.Services;

public sealed class AttendanceCellBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Student s || parameter is not string cell)
            return AvaloniaProperty.UnsetValue;

        var key = (cell, s) switch
        {
            ("Arrival",   { IsAbsent: true })      => "SemiColorDangerLightActive",
            ("Arrival",   { IsLateArrival: true }) => "SemiColorWarningLightActive",
            ("Departure", { IsAbsent: true })      => "SemiColorDangerLightActive",
            ("Departure", { IsEarlyLeave: true })  => "SemiColorWarningLightActive",
            _ => null
        };

        if (key is null)
            return AvaloniaProperty.UnsetValue; // обычная ячейка — без подсветки

        return Application.Current!.TryGetResource(key, Application.Current.ActualThemeVariant, out var brush)
               && brush is IBrush b ? b : AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}