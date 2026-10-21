using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Attendix.Models;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Attendix.Services;

public partial class StudentGroupRecord : ObservableObject
{
    [ObservableProperty] public partial string FullName { get; set; } = string.Empty;

    [ObservableProperty] public partial string Group { get; set; } = string.Empty;

    [JsonIgnore]
    public ObservableCollection<string>? GroupOptions { get; set; }

    public void RefreshGroupSelection() =>
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Group)));
}

public static class StudentGroupCatalog
{
    private static string FilePath => AppPaths.GetFilePath("student-groups.json");

    private static string BackupFilePath => AppPaths.GetFilePath("student-groups.backup.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
    };

    private sealed class CatalogFile
    {
        public string GeneratedAt { get; set; } = string.Empty;
        public List<string> CustomGroups { get; set; } = [];
        public List<StudentGroupRecord> Students { get; set; } = [];
    }

    private static List<StudentGroupRecord> _records = [];

    public static List<string> CustomGroups { get; private set; } = [];

    public static bool IsLoaded { get; private set; }

    public static IReadOnlyList<StudentGroupRecord> Records =>
        IsLoaded ? _records : throw new InvalidOperationException("Справочник групп не загружен; вызовите EnsureLoaded");

    public static void EnsureLoaded()
    {
        if (IsLoaded)
            return;

        CatalogFile? file = null;
        try
        {
            if (File.Exists(FilePath))
                file = JsonSerializer.Deserialize<CatalogFile>(File.ReadAllText(FilePath), JsonOptions);
        }
        catch
        {
            // битый файл — ниже развернем встроенный ресурс
        }

        if (file is null)
            file = LoadEmbeddedOrDefault();

        _records = Sanitize(file.Students ?? []);
        CustomGroups = file.CustomGroups ?? [];
        RebuildIndex();
        IsLoaded = true;
    }

    private static List<StudentGroupRecord> Sanitize(List<StudentGroupRecord> records)
    {
        foreach (var record in records)
        {
            record.FullName ??= string.Empty;
            record.Group ??= string.Empty;
        }

        return records;
    }

    private const int MaxBackupCopies = 5;

    public static void CreateBackup()
    {
        try
        {
            if (!File.Exists(FilePath))
                return;

            var stamped = AppPaths.GetFilePath(
                $"student-groups.backup.{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
            File.Copy(FilePath, stamped, true);

            var oldCopies = Directory.GetFiles(AppPaths.Folder, "student-groups.backup.*.json")
                .Where(f => !string.Equals(f, stamped, StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(f, BackupFilePath, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f, StringComparer.Ordinal)
                .Skip(MaxBackupCopies - 1);
            foreach (var stale in oldCopies)
                File.Delete(stale);

            File.Copy(FilePath, BackupFilePath, true);
        }
        catch
        {
            // резервная копия — не критична для работы
        }
    }

    public static bool TryLoadBackup(out List<StudentGroupRecord> records, out List<string> customGroups)
    {
        records = [];
        customGroups = [];
        try
        {
            if (!File.Exists(BackupFilePath))
                return false;

            var file = JsonSerializer.Deserialize<CatalogFile>(File.ReadAllText(BackupFilePath), JsonOptions);
            if (file?.Students is null)
                return false;

            records = Sanitize(file.Students);
            customGroups = file.CustomGroups ?? [];
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static CatalogFile LoadEmbeddedOrDefault()
    {
        try
        {
            var stream = AssetLoader.Open(new Uri("avares://Attendix.Core/Assets/StudentGroups.json"));
            using var reader = new StreamReader(stream);
            var fromResource = JsonSerializer.Deserialize<CatalogFile>(reader.ReadToEnd(), JsonOptions);
            if (fromResource is { Students.Count: > 0 })
            {
                Save(fromResource.Students, fromResource.CustomGroups ?? []);
                return fromResource;
            }
        }
        catch
        {
            // ресурс недоступен — работаем с пустым справочником
        }

        return new CatalogFile();
    }

    public static string NormalizeName(string? name)
    {
        var normalized = (name ?? string.Empty).Replace('ё', 'е').Replace('Ё', 'Е').ToLowerInvariant();
        return string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static bool TryGetGroup(string? fullName, out string group)
    {
        group = string.Empty;
        if (fullName is null)
            return false;

        var index = _index;
        if (index.TryGetValue(NormalizeName(fullName), out var found))
        {
            group = found ?? string.Empty;
            return true;
        }

        return false;
    }

    private static Dictionary<string, string> _index = [];

    private static void RebuildIndex() =>
        _index = _records
            .Where(r => r.FullName.Length > 0)
            .GroupBy(r => NormalizeName(r.FullName))
            .ToDictionary(g => g.Key, g => g.First().Group, StringComparer.Ordinal);

    public static int ApplyGroups(List<Student> students)
    {
        EnsureLoaded();
        var corrected = 0;
        foreach (var student in students)
        {
            if (TryGetGroup(student.FullName, out var group) && student.Group != group)
            {
                student.Group = group;
                corrected++;
            }
        }

        return corrected;
    }

    public static void Save(IReadOnlyList<StudentGroupRecord> records, IReadOnlyList<string> customGroups)
    {
        _records = records.ToList();
        CustomGroups = customGroups.ToList();
        RebuildIndex();

        try
        {
            Directory.CreateDirectory(AppPaths.Folder);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(
                new CatalogFile
                {
                    GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    CustomGroups = [.. CustomGroups],
                    Students = _records,
                }, JsonOptions));
            File.Move(tmp, FilePath, true);
        }
        catch
        {
            // ошибки диска — не мешаем работе приложения
        }
    }
}
