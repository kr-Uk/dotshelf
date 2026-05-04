using System.IO;
using System.Text.Json;
using DotShelf.Launcher.Core;

namespace DotShelf.Launcher;

internal sealed class LauncherSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string InstallRoot { get; set; } = LauncherPaths.CreateDefault("Dotshelf").RootDirectory.FullName;
    public int BestGameScore { get; set; }

    private static FileInfo SettingsFile
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return new FileInfo(Path.Combine(localAppData, "DotShelfLauncher", "settings.json"));
        }
    }

    public static LauncherSettings Load()
    {
        try
        {
            if (!SettingsFile.Exists)
            {
                return CreateDefault();
            }

            using var stream = SettingsFile.OpenRead();
            var settings = JsonSerializer.Deserialize<LauncherSettings>(stream, JsonOptions) ?? CreateDefault();
            if (string.IsNullOrWhiteSpace(settings.InstallRoot))
            {
                settings.InstallRoot = LauncherPaths.CreateDefault("Dotshelf").RootDirectory.FullName;
            }

            settings.InstallRoot = Path.GetFullPath(settings.InstallRoot);
            return settings;
        }
        catch
        {
            return CreateDefault();
        }
    }

    public void Save()
    {
        InstallRoot = Path.GetFullPath(InstallRoot);
        SettingsFile.Directory?.Create();
        using var stream = SettingsFile.Create();
        JsonSerializer.Serialize(stream, this, JsonOptions);
    }

    public LauncherPaths CreatePaths()
    {
        return new LauncherPaths(Path.GetFullPath(InstallRoot));
    }

    private static LauncherSettings CreateDefault()
    {
        return new LauncherSettings
        {
            InstallRoot = LauncherPaths.CreateDefault("Dotshelf").RootDirectory.FullName
        };
    }
}
