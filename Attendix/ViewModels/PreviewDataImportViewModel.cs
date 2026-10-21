using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Attendix.Models;
using Attendix.Services;
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
    private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

    private const int PreviewRowCount = 100;

    private readonly string _csvPath = null!;
    private readonly List<string[]> _allRows = [];

    public ObservableCollection<SourceColumn> Columns { get; } = [];
    public ObservableCollection<Student> PreviewStudents { get; } = [];

    [ObservableProperty] public partial SourceColumn? SelectedDateColumn { get; set; }
    [ObservableProperty] public partial SourceColumn? SelectedGroupColumn { get; set; }
    [ObservableProperty] public partial SourceColumn? SelectedFullNameColumn { get; set; }
    [ObservableProperty] public partial SourceColumn? SelectedFirstArrivalColumn { get; set; }
    [ObservableProperty] public partial SourceColumn? SelectedLastDepartureColumn { get; set; }

    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;

    // None (— не импортировать —) картой ФИО не считается: у него Index = -1
    public bool HasFullNameMapping => SelectedFullNameColumn is { Index: >= 0 };

    public PreviewDataImportViewModel()
    {
    }

    public PreviewDataImportViewModel(string csvPath)
    {
        _csvPath = csvPath;

        // Справочник нужен уже в предпросмотре: подставляем группу по ФИО при разборе строк
        StudentGroupCatalog.EnsureLoaded();

        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ",",
                HasHeaderRecord = false,
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

    partial void OnSelectedDateColumnChanged(SourceColumn? value) => RefreshPreview();
    partial void OnSelectedGroupColumnChanged(SourceColumn? value) => RefreshPreview();

    partial void OnSelectedFullNameColumnChanged(SourceColumn? value)
    {
        OnPropertyChanged(nameof(HasFullNameMapping));
        RefreshPreview();
    }

    partial void OnSelectedFirstArrivalColumnChanged(SourceColumn? value) => RefreshPreview();
    partial void OnSelectedLastDepartureColumnChanged(SourceColumn? value) => RefreshPreview();

    private void Rebuild()
    {
        Columns.Clear();
        PreviewStudents.Clear();
        SelectedDateColumn = null;
        SelectedGroupColumn = null;
        SelectedFullNameColumn = null;
        SelectedFirstArrivalColumn = null;
        SelectedLastDepartureColumn = null;

        if (_allRows.Count == 0)
        {
            StatusText = "Файл не содержит данных";
            return;
        }

        var columnCount = _allRows.Max(r => r.Length);
        Columns.Add(SourceColumn.None);
        for (var c = 0; c < columnCount; c++)
        {
            var header = !string.IsNullOrWhiteSpace(GetCell(_allRows[0], c))
                ? GetCell(_allRows[0], c)!.Trim()
                : $"Колонка {c + 1}";
            Columns.Add(new SourceColumn(c, header));
        }

        AutoGuessMapping();
        RefreshPreview();
    }

    private IEnumerable<string[]> DataRows() => _allRows.Skip(1);

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
            ? "Сопоставьте колонку с ФИО - предпросмотр появится в таблице"
            : $"Показаны первые {PreviewStudents.Count} строк - так данные импортируются в таблицу";
    }

    private Student? ParseRow(string[] row)
    {
        var fullName = GetCell(row, SelectedFullNameColumn)?.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            return null;

        // Группа — из справочника (главный источник); кого там нет, показываем как в csv
        var group = GetCell(row, SelectedGroupColumn)?.Trim() ?? string.Empty;
        if (StudentGroupCatalog.TryGetGroup(fullName, out var catalogGroup))
            group = catalogGroup;

        return new Student
        {
            Date = TryParseDateOnly(GetCell(row, SelectedDateColumn), out var date) ? date : default,
            Group = group,
            FullName = fullName,
            FirstArrival = TryParseTimeOnly(GetCell(row, SelectedFirstArrivalColumn), out var arrival)
                ? arrival
                : default,
            LastDeparture = TryParseTimeOnly(GetCell(row, SelectedLastDepartureColumn), out var departure)
                ? departure
                : default,
        };
    }

    /// <summary>Полный импорт по текущему сопоставлению (не только предпросмотр).</summary>
    public List<Student> GetStudents()
    {
        if (SelectedFullNameColumn is not { Index: >= 0 })
            throw new InvalidOperationException("Не сопоставлена колонка с ФИО.");

        return DataRows().Select(ParseRow)
            .Where(s => s is not null)
            .Select(s => s!)
            .ToList();
    }

    private void AutoGuessMapping()
    {
        SelectedDateColumn = Guess("дата", "date");
        SelectedGroupColumn = Guess("класс", "групп", "class", "group");
        SelectedFullNameColumn = Guess("фио", "фамил", "имя", "name", "учащ", "студент");
        SelectedFirstArrivalColumn = Guess("первый приход", "приход", "прибы", "arriv");
        SelectedLastDepartureColumn = Guess("последний уход", "уход", "убыл", "depart", "leav");
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

    private static bool TryParseDateOnly(string? value, out DateOnly result)
    {
        result = default;
        value = value?.Trim();
        if (string.IsNullOrEmpty(value))
            return false;

        if (DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt) ||
            DateTime.TryParse(value, RuCulture, DateTimeStyles.None, out dt) ||
            DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out dt) ||
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
        {
            result = DateOnly.FromDateTime(dt);
            return true;
        }

        return false;
    }

    private static bool TryParseTimeOnly(string? value, out TimeOnly result)
    {
        result = default;
        value = value?.Trim().Trim('(', ')', ' ').Trim();
        if (string.IsNullOrEmpty(value) || value.Equals("нет", StringComparison.OrdinalIgnoreCase))
            return false;

        if (TimeOnly.TryParse(value, RuCulture, out result) ||
            TimeOnly.TryParse(value, CultureInfo.CurrentCulture, out result) ||
            TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out result))
            return true;

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            result = TimeOnly.FromDateTime(dt);
            return true;
        }

        return false;
    }
}