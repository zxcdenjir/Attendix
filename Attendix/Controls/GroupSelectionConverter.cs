using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Attendix.Controls;

/// <summary>
/// Не даёт комбобоксу переноса группы записать null в группу студента. Когда список групп
/// перестраивается (Groups.Clear() в RebuildGroups), выделение в комбобоксе сбрасывается,
/// и двусторонняя привязка отправила бы null в record.Group — обнуляя группы всех строк
/// (отсюда был NRE в RebuildGroups при переименовании).
/// </summary>
public sealed class GroupSelectionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string { Length: > 0 } group ? group : BindingOperations.DoNothing;
}
