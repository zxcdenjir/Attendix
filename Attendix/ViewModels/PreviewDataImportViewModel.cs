using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Attendix.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CsvHelper;
using CsvHelper.Configuration;

namespace Attendix.ViewModels;

/// <summary>Колонка исходного csv для выбора в ComboBox.</summary>
public sealed class SourceColumn
{
    public static readonly SourceColumn None = new(-1, "— не импортировать —");

    public int Index { get; }
    public string DisplayName { get; }

    public SourceColumn(int index, string displayName)
    {
        Index = index;
        DisplayName = displayName;
    }

    public override string ToString() => DisplayName;
}

public partial class PreviewDataImportViewModel : ViewModelBase
{
    private const int PreviewRowCount = 100;

    private readonly string _csvPath;
    private List<string[]> _allRows = [];

    public ObservableCollection<SourceColumn> Columns { get; } = [];
    public ObservableCollection<Student> PreviewStudents { get; } = [];

    [ObservableProperty] private bool _hasHeader = true;
    [ObservableProperty] private SourceColumn? _selectedIdColumn;
    [ObservableProperty] private SourceColumn? _selectedFullNameColumn;
    [ObservableProperty] private SourceColumn? _selectedDateTimeColumn;
    [ObservableProperty] private string _statusText = string.Empty;

    public bool HasFullNameMapping => SelectedFullNameColumn is not null;

    public PreviewDataImportViewModel()
    {
        // для дизайнера XAML
    }

    public PreviewDataImportViewModel(string csvPath)
    {
        _csvPath = csvPath;

        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ",",
                HasHeaderRecord = false, // читаем всё как есть, заголовок обрабатываем сами
                BadDataFound = null,
            };

            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, config);
            while (csv.Read())
            {
                var record = csv.Parser.Record;
                _allRows.Add(record?.Select(f => f ?? string.Empty).ToArray() ?? []);
            }

            Rebuild();
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось прочитать CSV: " + ex.Message;
        }
    }

    partial void OnHasHeaderChanged(bool value) => Rebuild();

    partial void OnSelectedIdColumnChanged(SourceColumn? value) => RefreshPreview();

    partial void OnSelectedFullNameColumnChanged(SourceColumn? value)
    {
        OnPropertyChanged(nameof(HasFullNameMapping));
        RefreshPreview();
    }

    partial void OnSelectedDateTimeColumnChanged(SourceColumn? value) => RefreshPreview();

    private void Rebuild()
    {
        Columns.Clear();
        PreviewStudents.Clear();
        SelectedIdColumn = null;
        SelectedFullNameColumn = null;
        SelectedDateTimeColumn = null;

        if (_allRows.Count == 0)
        {
            StatusText = "Файл не содержит данных";
            return;
        }

        var columnCount = _allRows.Max(r => r.Length);
        Columns.Add(SourceColumn.None);
        for (var c = 0; c < columnCount; c++)
        {
            var header = HasHeader && !string.IsNullOrWhiteSpace(GetCell(_allRows[0], c))
                ? GetCell(_allRows[0], c)!.Trim()
                : $"Колонка {c + 1}";
            Columns.Add(new SourceColumn(c, header));
        }

        AutoGuessMapping();
        RefreshPreview();
    }

    private IEnumerable<string[]> DataRows() => HasHeader ? _allRows.Skip(1) : _allRows;

    private void RefreshPreview()
    {
        PreviewStudents.Clear();

        if (SelectedFullNameColumn is not null)
        {
            foreach (var row in DataRows().Take(PreviewRowCount))
            {
                var student = ParseRow(row);
                if (student is not null)
                    PreviewStudents.Add(student);
            }
        }

        StatusText = SelectedFullNameColumn is null
            ? "Сопоставьте колонку с ФИО — предпросмотр появится в таблице"
            : $"Показаны первые {PreviewStudents.Count} строк — так данные лягут в таблицу";
    }

    private Student? ParseRow(string[] row)
    {
        var fullName = GetCell(row, SelectedFullNameColumn)?.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            return null; // пустые строки не импортируем

        return new Student
        {
            FullName = fullName,
            Id = int.TryParse(GetCell(row, SelectedIdColumn)?.Trim(),
                    NumberStyles.Any, CultureInfo.InvariantCulture, out var id) ? id : 0,
            Date = TryParseDateTime(GetCell(row, SelectedDateTimeColumn), out var dt) ? dt : default,
        };
    }

    /// <summary>Полный импорт по текущему сопоставлению (не только предпросмотр).</summary>
    public List<Student> GetStudents()
    {
        if (SelectedFullNameColumn is null)
            throw new InvalidOperationException("Не сопоставлена колонка с ФИО.");

        return DataRows().Select(ParseRow)
                         .Where(s => s is not null)
                         .Select(s => s!)
                         .ToList();
    }

    private void AutoGuessMapping()
    {
        SelectedIdColumn = Guess("id", "номер");
        SelectedFullNameColumn = Guess("фио", "фамил", "имя", "name", "студент");
        SelectedDateTimeColumn = Guess("дата", "время", "date", "time");
    }

    private SourceColumn Guess(params string[] keywords)
    {
        foreach (var column in Columns)
        {
            if (column.Index < 0)
                continue;

            var header = column.DisplayName.ToLowerInvariant();
            if (keywords.Any(header.Contains))
                return column;
        }

        return SourceColumn.None;
    }

    private static string? GetCell(string[] row, int index)
        => index >= 0 && index < row.Length ? row[index] : null;

    private static string? GetCell(string[] row, SourceColumn? column)
        => column is { Index: >= 0 } ? GetCell(row, column.Index) : null;

    private static bool TryParseDateTime(string? value, out DateTime result)
    {
        value = value?.Trim();

        if (DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out result))
            return true;
        if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out result))
            return true;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return true;

        return false;
    }
}