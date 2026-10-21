using System;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace Attendix.Services;

public sealed class CsvDateOnlyConverter : DefaultTypeConverter
{
    private static readonly string[] ReadFormats = ["dd.MM.yyyy", "M/d/yyyy", "yyyy-MM-dd"];

    public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
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
