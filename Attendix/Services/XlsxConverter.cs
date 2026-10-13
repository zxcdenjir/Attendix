using System.Globalization;
using System.IO;
using System.Text;
using ClosedXML.Excel;

namespace Attendix.Services;

public static class XlsxConverter
{
    public static void ConvertToCsv(string xlsxPath, string csvPath, int sheetIndex = 1)
    {
        using var workbook = new XLWorkbook(xlsxPath);
        var range = workbook.Worksheet(sheetIndex).RangeUsed();

        using var writer = new StreamWriter(csvPath, false, Encoding.UTF8);
        if (range is null)
            return;

        var columnCount = range.ColumnCount();
        foreach (var row in range.RowsUsed())
        {
            var cells = new string[columnCount];
            for (var c = 1; c <= columnCount; c++)
                cells[c - 1] = EscapeCsvField(CellToString(row.Cell(c)));
            writer.WriteLine(string.Join(",", cells));
        }
    }

    private static string CellToString(IXLCell cell)
    {
        try
        {
            return cell.DataType switch
            {
                XLDataType.DateTime => cell.GetDateTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                XLDataType.TimeSpan => cell.GetTimeSpan().ToString(@"hh\:mm\:ss"),
                XLDataType.Boolean => cell.GetBoolean() ? "1" : "0",
                XLDataType.Blank => string.Empty,
                _ => cell.GetString(),
            };
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }
}