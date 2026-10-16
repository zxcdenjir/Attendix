using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Attendix.Models;
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

    [ObservableProperty]
    public partial TimeOnly StartOfClasses { get; set; } = new TimeOnly(9, 0);

    [ObservableProperty] public partial TimeOnly EndOfClasses { get; set; } = new TimeOnly(16, 40);

    [ObservableProperty] public partial ThemeVariant Theme { get; set; } = ThemeVariant.Light;

    [JsonConstructor]
    private Settings() { }

    partial void OnStartOfClassesChanged(TimeOnly value) => SaveIfLoaded();
    partial void OnEndOfClassesChanged(TimeOnly value) => SaveIfLoaded();

    partial void OnThemeChanged(ThemeVariant value)
    {
        if (Application.Current is not null)
            Application.Current.RequestedThemeVariant = value;
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

            _loading = true;
            StartOfClasses = loaded.StartOfClasses;
            EndOfClasses = loaded.EndOfClasses;
            Theme = loaded.Theme;
        }
        catch
        {
            // битый файл
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
            File.Move(tmp, FilePath, true);
        }
        catch
        {
            // ошибки диска
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