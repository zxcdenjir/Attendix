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
        : Students.Count(s => s.Group.Equals(SelectedGroup, StringComparison.CurrentCultureIgnoreCase));

    /// <summary>Число применённых фильтров: скрытые показы + выбранная группа. Максимум 4, поиск не считается.</summary>
    public int ActiveFilterCount =>
        (!ShowAbsent ? 1 : 0) + (!ShowLate ? 1 : 0) + (!ShowLeft ? 1 : 0) + (SelectedGroup is not null ? 1 : 0);

    /// <summary>Для Header в u:Badge: null скрывает бейдж, иначе — число применённых фильтров.</summary>
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

    /// <summary>Начальные значения фильтров берутся из настроек после Settings.Load() в App.</summary>
    private void LoadPersistedFilters()
    {
        var settings = Settings.Current;
        SearchText = settings.SearchText;
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
        IsLoading = true;
        if (File.Exists(GetFilePath("attendance.csv")))
        {
            var records = ParseCsv<Student>(GetFilePath("attendance.csv"));

            Students.Clear();
            foreach (var record in records)
            {
                Students.Add(record);
            }
            
            Groups.Clear();
            foreach (var student in Students.Select(x => x.Group).Distinct())
            {
                Groups.Add(student);
            }

            ApplyFilters();
        }
        else
        {
            Students.Clear();
            ApplyFilters();
        }

        IsLoading = false;
    }

    [RelayCommand]
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
            var tempCsv = GetFilePath("import_preview.csv");

            IsLoading = true;
            await Task.Run(() => ExcelConverter.ConvertToCsv(filePath, tempCsv, 10, [2], ["Посещаемость"]));
            IsLoading = false;

            var previewVm = new PreviewDataImportViewModel(tempCsv);
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
                try
                {
                    File.Delete(tempCsv);
                }
                catch
                {
                    // ignored
                }

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
                return;
            }

            await Task.Run(() => WriteStudentsToCsv(students, GetFilePath("attendance.csv")));
            try
            {
                File.Delete(tempCsv);
            }
            catch
            {
                // ignored
            }

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


    [RelayCommand]
    private async Task ClearDataAsync()
    {
        var result = await OverlayMessageBox.ShowAsync("Вы уверены, что хотите стереть текущие локальные данные?",
            "Подтверждение", null, MessageBoxIcon.Question, MessageBoxButton.YesNo);

        if (result != MessageBoxResult.Yes) return;

        IsLoading = true;

        Students.Clear();
        ApplyFilters();

        try
        {
            File.Delete(GetFilePath("attendance.csv"));
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
            students = students.Where(s => s.Group.Equals(SelectedGroup, StringComparison.CurrentCultureIgnoreCase)).ToList();
        }

        if (!string.IsNullOrEmpty(SearchText))
        {
            var words = SearchText.Split(" ", StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim())
                .Where(w => w.Length > 0).ToList();
            students = students.Where(s =>
                    words.Any(w =>
                        s.FullName.Contains(w, StringComparison.CurrentCultureIgnoreCase) ||
                        s.Date.ToString("dd.MM.yyyy").Contains(w, StringComparison.CurrentCultureIgnoreCase) ||
                        s.Group.Contains(w, StringComparison.CurrentCultureIgnoreCase))).ToList();
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

    public static string GetAppDataFolder()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(basePath, "Attendix");
        Directory.CreateDirectory(appFolder);
        return appFolder;
    }

    public static string GetFilePath(string fileName) => Path.Combine(GetAppDataFolder(), fileName);

    /// <summary>Подбирает имя файла без расширения, которого ещё нет в directory: base, "base (1)", "base (2)" и т.д.</summary>
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
        Settings.Current.SearchText = value;
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