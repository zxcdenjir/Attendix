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

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsPanelEnabled))]
    private bool _dataIsEmpty = true;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsPanelEnabled))]
    private bool _isLoading = false;

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
            string filePath;
            var files = await TopLevel.GetTopLevel(MainView.Current)!.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("Таблицы")
                        {
                            Patterns = ["*.xlsx"]
                        }
                    ]
                });

            if (files.Count == 0)
            {
                return;
            }
            else
            {
                filePath = files[0].Path.LocalPath;
            }
            
            var result = await OverlayDialog.ShowStandardAsync<PreviewDataImportView, PreviewDataImportViewModel>(
                new PreviewDataImportViewModel(), null, new OverlayDialogOptions()
                {
                    Buttons = DialogButton.OKCancel,
                    CanDragMove = true,
                    CanLightDismiss = false,
                    CanResize = true,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    IsCloseButtonVisible = true,
                    Title = "Предпросмотр данных",
                    Mode = DialogMode.None
                });

            if (result != DialogResult.OK)
                return;
            
            IsLoading = true;
            
            await Task.Run(() =>
                XlsxConverter.ConvertToCsv(filePath, GetFilePath("attendance.csv")));

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