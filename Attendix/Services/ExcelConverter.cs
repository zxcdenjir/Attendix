using System;
using System.Globalization;
using System.IO;
using System.Text;
using ExcelDataReader;

namespace Attendix.Services;

public static class ExcelConverter
{
    /// <summary>Читает xls/xlsx и пишет csv, отрезая skipRows строк шапки отчёта сверху.</summary>
    public static void ConvertToCsv(string excelPath, string csvPath, int skipRows = 10)
    {
        using var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream); // сам определяет xls или xlsx

        for (var i = 0; i < skipRows && reader.Read(); i++)
        {
            // шапка отчёта («Отчет о посещаемости», даты, время занятий) — отрезаем
        }

        using var writer = new StreamWriter(csvPath, false, Encoding.UTF8);
        while (reader.Read())
        {
            var cells = new string[reader.FieldCount];
            for (var c = 0; c < reader.FieldCount; c++)
                cells[c] = EscapeCsvField(CellToString(reader.GetValue(c)));
            writer.WriteLine(string.Join(",", cells));
        }
    }

    private static string CellToString(object? value) => value switch
    {
        null or DBNull => string.Empty,
        string s => s,
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        TimeSpan ts => ts.ToString(@"hh\:mm\:ss"),
        // время без формата ячейки (доля суток 0..1)
        double d when d >= 0 && d < 1 => TimeSpan.FromDays(d).ToString(@"hh\:mm\:ss"),
        // дата без формата ячейки (серийный номер Excel)
        double d => DateTime.FromOADate(d).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        bool b => b ? "1" : "0",
        _ => value.ToString() ?? string.Empty,
    };

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }
}