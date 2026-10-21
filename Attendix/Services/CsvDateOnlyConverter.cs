using System;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace Attendix.Services;

/// <summary>
/// Формат дат для attendance.csv (экспорт и импорт через CsvHelper должны совпадать):
/// в файл пишется человекочитаемое dd.MM.yyyy; читаются также прежний формат M/d/yyyy
/// и ISO yyyy-MM-dd. Разделители у форматов разные, поэтому старые и новые файлы
/// при разборе не путаются (в M/d/yyyy точек не бывает, в dd.MM.yyyy — слэшей).
/// </summary>
public sealed class CsvDateOnlyConverter : DefaultTypeConverter
{
    private static readonly string[] ReadFormats = ["dd.MM.yyyy", "M/d/yyyy", "yyyy-MM-dd"];

    public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
        // Пустая дата — как раньше: нулевая дата (CsvHelper и до этого подставлял default для value-типов)
        if (string.IsNullOrWhiteSpace(text))
            return DateOnly.MinValue;

        if (DateOnly.TryParseExact(text, ReadFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        throw new FormatException($"Не удалось разобрать дату \"{text}\" в attendance.csv.");
    }

    public override string? ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData) =>
        value is DateOnly date
            ? date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
            : base.ConvertToString(value, row, memberMapData);
}
