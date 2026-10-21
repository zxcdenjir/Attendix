using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Attendix.Models;
using Attendix.Services;
using Attendix.Views;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CsvHelper;
using CsvHelper.Configuration;
using Ursa.Controls;

namespace Attendix.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    public  ObservableCollection<Student> Students { get; } = [];

    public ObservableCollection<Student> FilteredStudents { get; } = [];
    
    public ObservableCollection<string> Groups { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPanelEnabled))]
    public partial bool DataIsEmpty { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPanelEnabled))]
    public partial bool IsLoading { get; set; } = false;

    public bool IsPanelEnabled => !DataIsEmpty && !IsLoading;

    [ObservableProperty] public partial string? SearchText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveFilterBadge))]
    public partial bool ShowAbsent { get; set; } = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveFilterBadge))]
    public partial bool ShowLate { get; set; } = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveFilterBadge))]
    public partial bool ShowLeft { get; set; } = true;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalInSelectedGroup))]
    [NotifyPropertyChangedFor(nameof(ActiveFilterBadge))]
    public partial string? SelectedGroup { get; set; }

    public int TotalInSelectedGroup => string.IsNullOrEmpty(SelectedGroup)
        ? 0
        : Students.Count(s => string.Equals(s.Group, SelectedGroup, StringComparison.CurrentCultureIgnoreCase));

    public int ActiveFilterCount =>
        (!ShowAbsent ? 1 : 0) + (!ShowLate ? 1 : 0) + (!ShowLeft ? 1 : 0) + (SelectedGroup is not null ? 1 : 0);

    public string? ActiveFilterBadge => ActiveFilterCount == 0 ? null : ActiveFilterCount.ToString();

    public HomeViewModel()
    {
        Students.CollectionChanged += (_, _) =>
        {
            DataIsEmpty = Students.Count == 0;
            OnPropertyChanged(nameof(TotalInSelectedGroup));
        };
        Settings.Current.PropertyChanged += OnSettingsChanged;
        LoadPersistedFilters();
        _ = LoadDataAsync();
    }

    private void LoadPersistedFilters()
    {
        var settings = Settings.Current;
        ShowAbsent = settings.ShowAbsent;
        ShowLate = settings.ShowLate;
        ShowLeft = settings.ShowLeft;
        SelectedGroup = settings.SelectedGroup;
    }
    
    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(Settings.StartOfClasses) or nameof(Settings.EndOfClasses)
            or nameof(Settings.HighlightCells)))
            return;

        foreach (var student in Students)
            student.RefreshAttendance();

        ApplyFilters();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            var csvPath = AppPaths.GetFilePath("attendance.csv");

            if (File.Exists(csvPath))
            {
                var records = ParseCsv<Student>(csvPath);

                Students.Clear();
                foreach (var record in records)
                {
                    Students.Add(record);
                }

                StudentGroupCatalog.ApplyGroups(Students.ToList());
            }
            else
            {
                Students.Clear();
            }

            RebuildGroups();

            if (SelectedGroup is not null
                && !Groups.Contains(SelectedGroup, StringComparer.CurrentCultureIgnoreCase))
                SelectedGroup = null;

            ApplyFilters();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Битый или залоченный attendance.csv не должен вешать панель: грузим пустое
            // состояние и показываем ошибку, вместо навсегда заполпшего индикатора
            Students.Clear();
            RebuildGroups();
            ApplyFilters();
            MainView.Current.ShowToast($"Не удалось загрузить attendance.csv: {ex.Message}", NotificationType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RebuildGroups()
    {
        Groups.Clear();
        foreach (var group in Students.Select(s => s.Group).Where(g => g is { Length: > 0 }).Distinct())
            Groups.Add(group);
    }

    private void ApplyGroupRenames(IReadOnlyList<(string Old, string New)> renames)
    {
        foreach (var (oldName, newName) in renames)
        {
            foreach (var student in Students.Where(s => s.Group == oldName).ToList())
                student.Group = newName;
        }
    }    [RelayCommand]
    private async Task ImportDataAsync()
    {
        try
        {
            var files = await TopLevel.GetTopLevel(MainView.Current)!.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("Таблицы Excel") { Patterns = ["*.xlsx", "*.xls"] }
                    ],
                });

            if (files.Count == 0)
                return;

            var filePath = files[0].Path.LocalPath;
            var tempCsv = AppPaths.GetFilePath("import_preview.csv");

            IsLoading = true;
            await Task.Run(() => ExcelConverter.ConvertToCsv(filePath, tempCsv, 10, [2], ["Посещаемость"]));
            IsLoading = false;

            var previewVm = await Task.Run(() => new PreviewDataImportViewModel(tempCsv));
            var result = await OverlayDialog.ShowStandardAsync<PreviewDataImportView, PreviewDataImportViewModel>(
                previewVm, null, new OverlayDialogOptions()
                {
                    Buttons = DialogButton.OKCancel,
                    CanDragMove = true,
                    CanLightDismiss = false,
                    CanResize = true,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    IsCloseButtonVisible = true,
                    Title = $"Предпросмотр данных - {Path.GetFileName(filePath)}",
                    Mode = DialogMode.None,
                    FullScreen = false,
                    StyleClass = "PreviewImport"
                });

            if (result != DialogResult.OK)
            {
                TryDeleteFile(tempCsv);
                return;
            }

            IsLoading = true;

            List<Student> students;
            try
            {
                students = await Task.Run(previewVm.GetStudents);
            }
            catch (InvalidOperationException ex)
            {
                MainView.Current.ShowToast(ex.Message, NotificationType.Warning);
                // на маппинг-ошибке temp-файл тоже уводим в сторону, чтобы не оставлять мусора
                TryDeleteFile(tempCsv);
                return;
            }

            await Task.Run(() => WriteStudentsToCsv(students, AppPaths.GetFilePath("attendance.csv")));
            TryDeleteFile(tempCsv);

            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            MainView.Current.ShowToast(ex.InnerException?.Message ?? ex.Message, NotificationType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task OpenStudentGroupsAsync()
    {
        try
        {
            StudentGroupCatalog.EnsureLoaded();
            StudentGroupCatalog.CreateBackup();
            var vm = new StudentGroupsViewModel();
            var dialogTask = OverlayDialog.ShowStandardAsync<StudentGroupsView, StudentGroupsViewModel>(
                vm, null, new OverlayDialogOptions
                {
                    Title = "Справочник групп",
                    Buttons = DialogButton.OKCancel,
                    CanDragMove = true,
                    CanLightDismiss = false,
                    CanResize = true,
                    IsCloseButtonVisible = true,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    StyleClass = "Groups"
                });

            Dispatcher.UIThread.Post(HomeView.Current.HideSettingsFlyout, DispatcherPriority.Background);

            var result = await dialogTask;
            if (result != DialogResult.OK)
                return;

            vm.SaveToCatalog();
            StudentGroupCatalog.ApplyGroups(Students.ToList());
            ApplyGroupRenames(vm.GroupRenames);
            if (Students.Count > 0)
                WriteStudentsToCsv(Students.ToList(), AppPaths.GetFilePath("attendance.csv"));
            await LoadDataAsync();

            MainView.Current.ShowToast("Справочник групп сохранен", NotificationType.Success);
        }
        catch (Exception ex)
        {
            MainView.Current.ShowToast(ex.InnerException?.Message ?? ex.Message, NotificationType.Error);
        }
    }

    [RelayCommand]
    private async Task ExportToXlsxAsync()
    {
        var students = FilteredStudents.ToList();
        if (students.Count == 0)
        {
            MainView.Current.ShowToast("Нет данных для экспорта", NotificationType.Warning);
            return;
        }

        try
        {
            var top = TopLevel.GetTopLevel(MainView.Current)!;
            var directory = Settings.Current.LastExportDirectory;
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                directory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            IStorageFolder? startFolder = null;
            try
            {
                startFolder = await top.StorageProvider.TryGetFolderFromPathAsync(new Uri(directory));
            }
            catch
            {
                // путь оказался некорректным - диалог откроется в папке по умолчанию
            }

            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Экспорт отчета о посещаемости",
                SuggestedStartLocation = startFolder,
                SuggestedFileName = GetUniqueFileName(directory, $"Отчет о посещаемости {DateTime.Now:dd.MM.yyyy}"),
                DefaultExtension = "xlsx",
                FileTypeChoices = [new FilePickerFileType("Книга Excel") { Patterns = ["*.xlsx"] }],
                ShowOverwritePrompt = true,
            });
            if (file is null)
                return;

            var path = file.Path.LocalPath;
            Settings.Current.LastExportDirectory = Path.GetDirectoryName(path);
            IsLoading = true;
            await Task.Run(() => AttendanceReportExporter.Export(students, path));
            MainView.Current.ShowToast("Отчет успешно сохранен", NotificationType.Success);
        }
        catch (Exception ex)
        {
            MainView.Current.ShowToast(ex.InnerException?.Message ?? ex.Message, NotificationType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static void WriteStudentsToCsv(List<Student> students, string path)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = "," };
        using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
        using var csv = new CsvWriter(writer, config);
        csv.WriteRecords(students);
    }


    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // «переезд» служебного файла к следующему циклу работы — не ошибка
        }
    }

    [RelayCommand]
    private async Task ClearDataAsync()
    {
        var result = await OverlayMessageBox.ShowAsync("Вы уверены, что хотите стереть текущие локальные данные?",
            "Подтверждение", null, MessageBoxIcon.Question, MessageBoxButton.YesNo);

        if (result != MessageBoxResult.Yes) return;

        IsLoading = true;

        Students.Clear();
        RebuildGroups();
        ApplyFilters();

        try
        {
            File.Delete(AppPaths.GetFilePath("attendance.csv"));
        }
        catch (Exception ex)
        {
            MainView.Current.ShowToast(ex.Message, NotificationType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilters()
    {
        var students = Students.ToList();
        
        if (!string.IsNullOrEmpty(SelectedGroup))
        {
            students = students.Where(s => string.Equals(s.Group, SelectedGroup, StringComparison.CurrentCultureIgnoreCase)).ToList();
        }

        if (!string.IsNullOrEmpty(SearchText))
        {
            var words = SearchText.Split(" ", StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim())
                .Where(w => w.Length > 0).ToList();
            students = students.Where(s =>
                    words.Any(w =>
                        (s.FullName?.Contains(w, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                        s.Date.ToString("dd.MM.yyyy").Contains(w, StringComparison.CurrentCultureIgnoreCase) ||
                        (s.Group?.Contains(w, StringComparison.CurrentCultureIgnoreCase) ?? false))).ToList();
        }
        
        if (!ShowAbsent)
        {
            students = students.Where(s => s.FirstArrival != TimeOnly.MinValue || s.LastDeparture != TimeOnly.MinValue).ToList();
        }
        if (!ShowLate)
        {
            students = students.Where(s => s.FirstArrival == TimeOnly.MinValue || s.FirstArrival < Settings.Current.StartOfClasses).ToList();
        }
        if (!ShowLeft)
        {
            students = students.Where(s => s.LastDeparture == TimeOnly.MinValue || s.LastDeparture > Settings.Current.EndOfClasses).ToList();
        }

        FilteredStudents.Clear();
        foreach (var student in students)
        {
            FilteredStudents.Add(student);
        }
    }

    public static List<T> ParseCsv<T>(string csvPath)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ","
        };

        using var reader = new StreamReader(csvPath);
        using var csv = new CsvReader(reader, config);
        return csv.GetRecords<T>().ToList();
    }

    private static string GetUniqueFileName(string directory, string baseName)
    {
        var name = baseName;
        var index = 1;
        while (File.Exists(Path.Combine(directory, name + ".xlsx")))
            name = $"{baseName} ({index++})";
        return name;
    }

    partial void OnSearchTextChanged(string? value)
    {
        ApplyFilters();
    }

    partial void OnShowAbsentChanged(bool value)
    {
        Settings.Current.ShowAbsent = value;
        ApplyFilters();
    }

    partial void OnShowLateChanged(bool value)
    {
        Settings.Current.ShowLate = value;
        ApplyFilters();
    }

    partial void OnShowLeftChanged(bool value)
    {
        Settings.Current.ShowLeft = value;
        ApplyFilters();
    }

    partial void OnSelectedGroupChanged(string? value)
    {
        Settings.Current.SelectedGroup = value;
        ApplyFilters();
    }

    [RelayCommand]
    private void ResetFilters()
    {
        ShowAbsent = true;
        ShowLate = true;
        ShowLeft = true;
        SelectedGroup = null;
    }
}