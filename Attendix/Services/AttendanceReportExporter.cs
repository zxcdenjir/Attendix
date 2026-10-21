using System;
using System.Collections.Generic;
using System.Linq;
using Attendix.Models;
using ClosedXML.Excel;

namespace Attendix.Services;

public static class AttendanceReportExporter
{
    public static void Export(IEnumerable<Student> students, string filePath)
    {
        var rows = students.ToList();
        if (rows.Count == 0)
            throw new InvalidOperationException("Нет данных для экспорта.");

        var dateFrom = rows.MinBy(s => s.Date)!.Date;
        var dateTo = rows.MaxBy(s => s.Date)!.Date;

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Лист1");

        ws.Style.Font.FontName = "Arial";
        ws.Style.Font.FontSize = 10;

        int[] widths = [7, 4, 7, 12, 8, 3, 12, 34, 12, 13];
        for (var i = 0; i < widths.Length; i++)
            ws.Column(i + 1).Width = widths[i] - 0.710625;
        
        ws.Range("A1:J1").Merge();
        var title = ws.Cell("A1");
        title.Value = "Отчет о посещаемости";
        title.Style.Font.FontSize = 12;
        title.Style.Font.Bold = true;
        title.Style.Font.Underline = XLFontUnderlineValues.Single;
        title.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        title.Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;
        ws.Row(1).Height = 16;

        for (var r = 2; r <= 10; r++)
            ws.Row(r).Height = 13.5;

        WriteHeaderLabel(ws, "A3:D3", "Дата составления:");
        WriteHeaderValue(ws, "E3:G3", DateTime.Now, "dd.MM.yyyy HH:mm:ss");
        WriteHeaderLabel(ws, "A4:D4", "Дата начала отчета:");
        WriteHeaderValue(ws, "E4:G4", dateFrom.ToDateTime(TimeOnly.MinValue), "dd.MM.yyyy");
        WriteHeaderLabel(ws, "A5:D5", "Дата окончания отчета:");
        WriteHeaderValue(ws, "E5:G5", dateTo.ToDateTime(TimeOnly.MinValue), "dd.MM.yyyy");

        WriteHeaderLabel(ws, "A7:D7", "Время занятий:");
        WriteHeaderLabel(ws, "A8:D8", "Начало занятий:");
        WriteHeaderValue(ws, "E8", Settings.Current.StartOfClasses.ToTimeSpan(), "HH:mm");
        WriteHeaderLabel(ws, "A9:D9", "Окончание занятий:");
        WriteHeaderValue(ws, "E9", Settings.Current.EndOfClasses.ToTimeSpan(), "HH:mm");

        ws.Range("A3:G5").Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        ws.Range("A8:E9").Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

        const int headerRow = 11;
        ws.Range($"A{headerRow}:B{headerRow}").Merge();
        ws.Range($"D{headerRow}:G{headerRow}").Merge();

        var header = new Dictionary<int, string>
        {
            [1] = "Дата",
            [3] = "Класс",
            [4] = "Учащийся",
            [8] = "Посещаемость",
            [9] = "Первый приход",
            [10] = "Последний уход"
        };

        foreach (var (col, text) in header)
        {
            var cell = ws.Cell(headerRow, col);
            cell.Value = text;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#969696");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        ws.Cell(headerRow, 9).Style.Alignment.WrapText = true;
        ws.Cell(headerRow, 10).Style.Alignment.WrapText = true;
        ws.Row(headerRow).Height = 39;

        var row = headerRow + 1;
        foreach (var s in rows)
        {
            ws.Range(row, 1, row, 2).Merge();
            ws.Range(row, 4, row, 7).Merge();

            var date = ws.Cell(row, 1);
            date.Value = s.Date.ToDateTime(TimeOnly.MinValue);
            date.Style.DateFormat.Format = "dd.MM.yyyy";
            date.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            date.Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;

            ws.Cell(row, 3).Value = s.Group;

            var name = ws.Cell(row, 4);
            name.Value = s.FullName;
            name.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            name.Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;

            var attendance = ws.Cell(row, 8);
            attendance.Value = s.Attendance;
            attendance.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            attendance.Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;

            WriteTimeCell(ws.Cell(row, 9), s.FirstArrival);
            WriteTimeCell(ws.Cell(row, 10), s.LastDeparture);

            ws.Row(row).Height = 12.8;
            row++;
        }
        var lastRow = row - 1;

        var table = ws.Range(headerRow, 1, lastRow, 10);
        table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

        wb.SaveAs(filePath);
    }

    private static void WriteHeaderLabel(IXLWorksheet ws, string range, string text)
    {
        ws.Range(range).Merge();
        var cell = ws.Cell(range.Split(':')[0]);
        cell.Value = text;
        cell.Style.Font.Bold = true;
        cell.Style.Font.Italic = true;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;
    }

    private static void WriteHeaderValue(IXLWorksheet ws, string range, object value, string format)
    {
        if (range.Contains(':'))
            ws.Range(range).Merge();

        var cell = ws.Cell(range.Split(':')[0]);
        cell.Value = value switch
        {
            DateTime dt => dt,
            DateOnly d => d.ToDateTime(TimeOnly.MinValue),
            TimeOnly t => t.ToTimeSpan(),
            TimeSpan ts => ts,
            _ => value.ToString() ?? string.Empty
        };
        cell.Style.DateFormat.Format = format;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;
    }

    private static void WriteTimeCell(IXLCell cell, TimeOnly time)
    {
        if (time == TimeOnly.MinValue)
        {
            cell.Value = "(нет)";
        }
        else
        {
            cell.Value = time.ToTimeSpan();
            cell.Style.DateFormat.Format = "HH:mm:ss";
        }

        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;
    }
}
