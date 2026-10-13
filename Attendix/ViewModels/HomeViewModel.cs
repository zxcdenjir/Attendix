using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using DocumentFormat.OpenXml.Drawing.Charts;
using Ursa.Controls;

namespace Attendix.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private ObservableCollection<Student> Students { get; } = [];

    public ObservableCollection<Student> FilteredStudents { get; } = []; //TODO: сделать фильтры (вёрстка и код)

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPanelEnabled))]
    public partial bool DataIsEmpty { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPanelEnabled))]
    public partial bool IsLoading { get; set; } = false;

    public bool IsPanelEnabled => !DataIsEmpty && !IsLoading;

    public HomeViewModel()
    {
        Students.CollectionChanged += (_, _) => DataIsEmpty = Students.Count == 0;
        _ = LoadDataAsync();
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
            await Task.Run(() => ExcelConverter.ConvertToCsv(filePath, tempCsv));
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
                    VerticalScrollBarVisibility =  ScrollBarVisibility.Disabled,
                    IsCloseButtonVisible = true,
                    Title = $"Предпросмотр данных - {Path.GetFileName(filePath)}",
                    Mode = DialogMode.None,
                    FullScreen = false,
                    StyleClass = "PreviewImport"
                });

            if (result != DialogResult.OK)
            {
                try { File.Delete(tempCsv); }
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
            try { File.Delete(tempCsv); }
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
        FilteredStudents.Clear();
        foreach (var student in Students)
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
}