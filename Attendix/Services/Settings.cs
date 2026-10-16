using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Attendix.Services;

public partial class Settings : ObservableObject
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Attendix");

    private static string FilePath => Path.Combine(Folder, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new ThemeVariantJsonConverter() }
    };

    public static Settings Current { get; } = new();

    private bool _loading;

    [ObservableProperty] public partial TimeOnly StartOfClasses { get; set; } = new TimeOnly(9, 0);

    [ObservableProperty] public partial TimeOnly EndOfClasses { get; set; } = new TimeOnly(16, 30);

    [ObservableProperty] public partial ThemeVariant Theme { get; set; } = ThemeVariant.Light;

    private Settings() { }

    partial void OnStartOfClassesChanged(TimeOnly value) => SaveIfLoaded();
    partial void OnEndOfClassesChanged(TimeOnly value) => SaveIfLoaded();

    partial void OnThemeChanged(ThemeVariant value)
    {
        
        SaveIfLoaded();
    }

    private void SaveIfLoaded()
    {
        if (!_loading) Save();
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return;

            var loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), JsonOptions);
            if (loaded is null)
                return;

            _loading = true;          // чтобы Load не вызвал запись тех же данных
            StartOfClasses = loaded.StartOfClasses;
            EndOfClasses = loaded.EndOfClasses;
            Theme = loaded.Theme;
        }
        catch
        {
            // битый файл — остаёмся на значениях по умолчанию
        }
        finally
        {
            _loading = false;
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOptions));
            File.Move(tmp, FilePath, true);   // атомарная замена — файл не побьётся при сбое
        }
        catch
        {
            // ошибки диска не роняют приложение
        }
    }
}

public sealed class ThemeVariantJsonConverter : JsonConverter<ThemeVariant>
{
    public override ThemeVariant Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetString() switch
        {
            nameof(ThemeVariant.Light) => ThemeVariant.Light,
            nameof(ThemeVariant.Dark)  => ThemeVariant.Dark,
            _                          => ThemeVariant.Default
        };

    public override void Write(Utf8JsonWriter writer, ThemeVariant value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Key.ToString());
}