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
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // bare catch 는 OOM/StackOverflow 같은 시스템 예외까지 삼키므로 예상 가능한
            // 케이스만 명시. 손상된 settings 는 .broken-{ts}.json 으로 백업해서 다음
            // Save 가 깔끔히 새 파일을 만들도록 한다 (조용히 덮어쓰면 BestGameScore 등
            // 사용자 데이터가 흔적 없이 사라짐).
            TryBackupCorruptedFile(SettingsFile);
            return CreateDefault();
        }
    }

    private static void TryBackupCorruptedFile(FileInfo file)
    {
        if (!file.Exists)
        {
            return;
        }

        try
        {
            var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
            var stem = Path.GetFileNameWithoutExtension(file.Name);
            var directory = file.DirectoryName ?? "";
            var backupPath = Path.Combine(directory, $"{stem}.broken-{timestamp}.json");
            file.MoveTo(backupPath);
        }
        catch
        {
            // 백업 자체가 실패해도 default 로 진행. corrupted 원본은 다음 Load 에서
            // 다시 catch 되어 결과적으로 같은 동작.
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
