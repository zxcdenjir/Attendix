using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ExcelDataReader;

namespace Attendix.Services;

public static class ExcelConverter
{
     /// <summary>Читает xls/xlsx и пишет csv, отрезая skipRows строк шапки отчёта сверху.</summary>
    public static void ConvertToCsv(string excelPath, string csvPath, int skipRows = 10, int[]? columnsToUpper = null)
    {
        using var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);

        for (var i = 0; i < skipRows && reader.Read(); i++)
        {
            // отрезает шапку отчёта
        }

        var upper = new HashSet<int>(columnsToUpper ?? []);

        string[]? header = null;
        var dataRows = new List<string[]>();
        var usedColumns = new HashSet<int>();

        while (reader.Read())
        {
            var fieldCount = reader.FieldCount;
            var cells = new string[fieldCount];
            var hasData = false;

            for (var c = 0; c < fieldCount; c++)
            {
                cells[c] = CellToString(reader.GetValue(c));
                if (cells[c].Length > 0)
                {
                    hasData = true;
                    usedColumns.Add(c);
                }
            }

            if (!hasData)
                continue;

            if (header == null)
            {
                header = cells;
            }
            else
            {
                foreach (var c in upper)
                    if (c < fieldCount)
                        cells[c] = cells[c].ToUpperInvariant();
                dataRows.Add(cells);
            }
        }

        if (header == null)
            return;

        var kept = Enumerable.Range(0, header.Length).Where(usedColumns.Contains).ToArray();

        using var writer = new StreamWriter(csvPath, false, Encoding.UTF8);
        WriteRow(writer, kept.Select(c => header[c]));
        foreach (var row in dataRows)
            WriteRow(writer, kept.Select(c => row[c]));
    }
     
    private static void WriteRow(TextWriter writer, IEnumerable<string> cells) =>
        writer.WriteLine(string.Join(",", cells.Select(EscapeCsvField)));

    private static string CellToString(object? value) => value switch
    {
        null or DBNull => string.Empty,
        string s => s,
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        TimeSpan ts => ts.ToString(@"hh\:mm\:ss"),
        double d when d >= 0 && d < 1 => TimeSpan.FromDays(d).ToString(@"hh\:mm\:ss"),
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