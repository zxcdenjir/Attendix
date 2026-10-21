using System;
using System.IO;

namespace Attendix.Services;

public static class AppPaths
{
    public static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Attendix");

    public static string GetFilePath(string fileName)
    {
        Directory.CreateDirectory(Folder);
        return Path.Combine(Folder, fileName);
    }
}
