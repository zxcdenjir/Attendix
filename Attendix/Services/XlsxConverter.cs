using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace Attendix.Services;

public static class XlsxConverter
{
    public static void ConvertToCsv(string xlsxPath, string csvPath, int sheetIndex = 1)
    {
        using var workbook = new XLWorkbook(xlsxPath);
        var worksheet = workbook.Worksheet(sheetIndex);
        var range = worksheet.RangeUsed();

        using var writer = new StreamWriter(csvPath, false, System.Text.Encoding.UTF8);

        foreach (var row in range!.RowsUsed())
        {
            var cells = row.CellsUsed().Select(cell => EscapeCsvField(cell.GetString()));
            writer.WriteLine(string.Join(",", cells));
        }
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }
}