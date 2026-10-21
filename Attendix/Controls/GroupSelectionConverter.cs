using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Attendix.Controls;

public sealed class GroupSelectionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string { Length: > 0 } group ? group : BindingOperations.DoNothing;
}
