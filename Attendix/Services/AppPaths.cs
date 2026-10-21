using System;
using System.IO;

namespace Attendix.Services;

/// <summary>
/// Единая точка путей к данным приложения: %APPDATA%\Attendix.
/// Все файлы состояния (settings.json, attendance.csv, student-groups.json и бэкапы)
/// берут путь отсюда, чтобы расположение данных не разъезжалось по классам.
/// </summary>
public static class AppPaths
{
    /// <summary>Папка данных приложения: %APPDATA%\Attendix.</summary>
    public static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Attendix");

    /// <summary>Полный путь к файлу в папке данных. Заодно гарантирует существование папки.</summary>
    public static string GetFilePath(string fileName)
    {
        Directory.CreateDirectory(Folder);
        return Path.Combine(Folder, fileName);
    }
}
