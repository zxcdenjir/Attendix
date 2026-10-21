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

/// <summary>Запись справочника «студент — группа».</summary>
public partial class StudentGroupRecord : ObservableObject
{
    [ObservableProperty] public partial string FullName { get; set; } = string.Empty;

    [ObservableProperty] public partial string Group { get; set; } = string.Empty;

    /// <summary>Список групп для редактора справочника (не сериализуется); назначается VM.</summary>
    [JsonIgnore]
    public ObservableCollection<string>? GroupOptions { get; set; }

    /// <summary>Переподталкивает значение группы в привязку комбобокса строки — так строка
    /// заново выбирает группу после перестройки списка групп.</summary>
    public void RefreshGroupSelection() =>
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Group)));
}

/// <summary>
/// Справочник «студент — группа», собранный из архивных списков обучающихся.
/// Используется при импорте, чтобы перезаписывать группу студента: в отчетах
/// посещаемости она не всегда определена верно. Файл — %APPDATA%\Attendix\student-groups.json;
/// при первом запуске разворачивается из встроенного ресурса Assets/StudentGroups.json
/// (состав: отчисленные исключены, имя группы в верхнем регистре, ФИО как в ведомостях).
/// </summary>
public static class StudentGroupCatalog
{
    private static string FilePath => AppPaths.GetFilePath("student-groups.json");

    // Последний снапшот — именно его читает «Восстановить» в редакторе
    private static string BackupFilePath => AppPaths.GetFilePath("student-groups.backup.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // Кириллица в файле должна читаться человеком, а не выглядеть как \u0418\u04121...
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

    // Группы, добавленные пользователем вручную (в данных их может не быть)
    public static List<string> CustomGroups { get; private set; } = [];

    public static bool IsLoaded { get; private set; }

    public static IReadOnlyList<StudentGroupRecord> Records =>
        IsLoaded ? _records : throw new InvalidOperationException("Справочник групп не загружен; вызовите EnsureLoaded");

    /// <summary>Гарантирует, что справочник прочитан с диска (при отсутствии файла — из ресурса).</summary>
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

    // Ручной правкой файла туда попадают null'ы — они не должны ронять редактор и индекс
    private static List<StudentGroupRecord> Sanitize(List<StudentGroupRecord> records)
    {
        foreach (var record in records)
        {
            record.FullName ??= string.Empty;
            record.Group ??= string.Empty;
        }

        return records;
    }

    // Сколько штампованных снапшотов держать на диске (история отката по сеансам правок)
    private const int MaxBackupCopies = 5;

    /// <summary>
    /// Снимает резервную копию текущего файла (если он есть): текущий файл копируется
    /// в обычный снапшот (его использует «Восстановить》) и в штампованную копию
    /// с датой-временем — на диске накапливается история состояний, а не только последний шаг.
    /// </summary>
    public static void CreateBackup()
    {
        try
        {
            if (!File.Exists(FilePath))
                return;

            var stamped = AppPaths.GetFilePath(
                $"student-groups.backup.{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
            File.Copy(FilePath, stamped, true);

            // Прореживаем штампованные копии: остаются MaxBackupCopies новейших (включая свежую);
            // только-что созданную и обычный снапшот не трогаем
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

    /// <summary>Читает резервную копию в память (без изменения файлов).</summary>
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
                // Кладём файл в %APPDATA%, чтобы справочник можно было видеть и править
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

    /// <summary>Нормализованное ФИО — ключ сопоставления: без регистра, ё→е, лишних пробелов.</summary>
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

        // Локальная копия ссылки на индекс: он пересоздаётся целиком без блокировок,
        // а разбор может идти в фоновом потоке (импорт). Атомарное чтение ссылки всегда
        // даёт целостную версию — старую или новую, но не урванную пополам.
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

    /// <summary>Перезаписывает группу тем студентам, кто найден в справочнике; возвращает число исправлений.</summary>
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

    /// <summary>Сохраняет справочник на диск и переегистрирует индекс сопоставления.</summary>
    public static void Save(IReadOnlyList<StudentGroupRecord> records, IReadOnlyList<string> customGroups)
    {
        _records = records.ToList();
        CustomGroups = customGroups.ToList();
        RebuildIndex();

        try
        {
            Directory.CreateDirectory(AppPaths.Folder);
            // Schema: {"generatedAt","customGroups","students"}; запись сначала во временный файл
            // (снапшот делается только при открытии диалога редактора — не здесь)
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
