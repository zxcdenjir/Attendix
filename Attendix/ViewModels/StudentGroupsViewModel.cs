using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Attendix.Services;
using Attendix.Views;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Attendix.ViewModels;

public partial class StudentGroupsViewModel : ViewModelBase
{

    private readonly List<string> _customGroups = [];

    private readonly List<(string Old, string New)> _groupRenames = [];

    public IReadOnlyList<(string Old, string New)> GroupRenames => _groupRenames;

    private bool _suppressRefill;

    public ObservableCollection<StudentGroupRecord> Records { get; private set; } = [];

    public ObservableCollection<string> Groups { get; } = [];

    public ObservableCollection<StudentGroupRecord> GroupStudents { get; } = [];

    [ObservableProperty] public partial string? SelectedGroup { get; set; }

    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty] public partial string NewStudentName { get; set; } = string.Empty;

    [ObservableProperty] public partial string NewGroupName { get; set; } = string.Empty;

    public StudentGroupsViewModel()
    {
        ReplaceRecords(
            StudentGroupCatalog.Records.Select(r => new StudentGroupRecord
            {
                FullName = r.FullName,
                Group = r.Group,
            }).ToList(),
            [.. StudentGroupCatalog.CustomGroups]);
    }

    private void OnRecordPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StudentGroupRecord.Group) && !_suppressRefill)
            RefillGroupStudents();
    }

    partial void OnSelectedGroupChanged(string? value) => RefillGroupStudents();

    partial void OnSearchTextChanged(string value) => RefillGroupStudents();

    private void ReplaceRecords(List<StudentGroupRecord> records, List<string> customGroups)
    {
        foreach (var record in Records)
            record.PropertyChanged -= OnRecordPropertyChanged;

        Records = new ObservableCollection<StudentGroupRecord>(records);
        _customGroups.Clear();
        _customGroups.AddRange(customGroups);

        foreach (var record in Records)
        {
            record.PropertyChanged += OnRecordPropertyChanged;
            record.GroupOptions = Groups;
        }

        RebuildGroups();
        RefillGroupStudents();
    }

    private void RebuildGroups()
    {
        var previous = SelectedGroup;
        Groups.Clear();
        foreach (var name in Records.Select(r => r.Group)
                     .Concat(_customGroups)
                     .Where(g => g is { Length: > 0 })
                     .Distinct()
                     .OrderBy(g => g, StringComparer.Ordinal))
        {
            Groups.Add(name);
        }

        SelectedGroup = previous is not null && Groups.Contains(previous) ? previous : Groups.FirstOrDefault();

        foreach (var record in Records)
            record.RefreshGroupSelection();
    }

    private void RefillGroupStudents()
    {
        GroupStudents.Clear();
        var search = StudentGroupCatalog.NormalizeName(SearchText);
        IEnumerable<StudentGroupRecord> source = Records.Where(r => r.Group == SelectedGroup);
        if (search.Length > 0)
            source = source.Where(r =>
                StudentGroupCatalog.NormalizeName(r.FullName).Contains(search, StringComparison.Ordinal));

        foreach (var record in source.OrderBy(r => r.FullName))
            GroupStudents.Add(record);
    }

    [RelayCommand]
    private void AddStudent()
    {
        var name = NewStudentName.Trim();
        if (SelectedGroup is null)
        {
            MainView.Current.ShowToast("Выберите группу.", NotificationType.Error);
            return;
        }

        if (name.Length == 0)
        {
            MainView.Current.ShowToast("Введите ФИО студента.", NotificationType.Error);
            return;
        }

        var normalized = StudentGroupCatalog.NormalizeName(name);
        if (Records.Any(r => StudentGroupCatalog.NormalizeName(r.FullName) == normalized))
        {
            MainView.Current.ShowToast("Такой студент уже есть в справочнике.", NotificationType.Error);
            return;
        }

        var record = new StudentGroupRecord
        {
            FullName = name,
            Group = SelectedGroup,
            GroupOptions = Groups,
        };
        record.PropertyChanged += OnRecordPropertyChanged;
        Records.Add(record);
        NewStudentName = string.Empty;
        RefillGroupStudents();
    }

    [RelayCommand]
    private void RemoveStudent(StudentGroupRecord? record)
    {
        if (record is null)
            return;

        record.PropertyChanged -= OnRecordPropertyChanged;
        Records.Remove(record);
        RefillGroupStudents();
    }

    [RelayCommand]
    private void RenameGroup()
    {
        if (SelectedGroup is null)
            return;

        var oldName = SelectedGroup;
        var newName = FormatGroupName(NewGroupName);
        if (newName.Length == 0)
        {
            MainView.Current.ShowToast("Введите новое имя группы.", NotificationType.Error);
            return;
        }

        if (newName == oldName)
        {
            NewGroupName = string.Empty;
            return;
        }

        if (Groups.Contains(newName))
        {
            MainView.Current.ShowToast($"Группа {newName} уже есть.", NotificationType.Error);
            return;
        }

        _suppressRefill = true;
        try
        {
            foreach (var record in Records)
            {
                if (record.Group == oldName)
                    record.Group = newName;
            }
        }
        finally
        {
            _suppressRefill = false;
        }

        var customIndex = _customGroups.IndexOf(oldName);
        if (customIndex >= 0)
            _customGroups[customIndex] = newName;

        _groupRenames.Add((oldName, newName));

        NewGroupName = string.Empty;
        SelectedGroup = newName;
        RebuildGroups();
        RefillGroupStudents();
    }

    [RelayCommand]
    private void AddGroup()
    {
        var name = FormatGroupName(NewGroupName);
        if (name.Length == 0)
        {
            MainView.Current.ShowToast("Введите имя группы.", NotificationType.Error);
            return;
        }

        if (Groups.Contains(name))
        {
            MainView.Current.ShowToast($"Группа {name} уже есть.", NotificationType.Error);
            return;
        }

        _customGroups.Add(name);
        NewGroupName = string.Empty;
        RebuildGroups();
        SelectedGroup = name;
        RefillGroupStudents();
    }

    [RelayCommand]
    private void RemoveGroup()
    {
        if (SelectedGroup is null)
            return;

        if (Records.Any(r => r.Group == SelectedGroup))
        {
            MainView.Current.ShowToast("Сначала перенесите или удалите студентов этой группы.", NotificationType.Error);
            return;
        }

        _customGroups.Remove(SelectedGroup);
        RebuildGroups();
        RefillGroupStudents();
    }

    [RelayCommand]
    private void RestoreBackup()
    {
        if (!StudentGroupCatalog.TryLoadBackup(out var records, out var customGroups))
        {
            MainView.Current.ShowToast("Резервная копия не найдена.", NotificationType.Error);
            return;
        }

        ReplaceRecords(records, customGroups);
        _groupRenames.Clear();
        MainView.Current.ShowToast("Восстановлено из резервной копии. Нажмите «ОК», чтобы сохранить.",
            NotificationType.Information);
    }

    public void SaveToCatalog() => StudentGroupCatalog.Save(Records, _customGroups);

    private static string FormatGroupName(string name) => (name ?? string.Empty).Trim();
}