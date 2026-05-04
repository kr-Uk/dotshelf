namespace DotShelf.Launcher.Core;

public sealed class LauncherPaths
{
    public LauncherPaths(string rootDirectory)
    {
        RootDirectory = new DirectoryInfo(rootDirectory);
    }

    public DirectoryInfo RootDirectory { get; }
    public DirectoryInfo VersionsDirectory => new(Path.Combine(RootDirectory.FullName, "versions"));
    public DirectoryInfo TempDirectory => new(Path.Combine(RootDirectory.FullName, "_temp"));
    public FileInfo StateFile => new(Path.Combine(RootDirectory.FullName, "install-state.json"));

    public static LauncherPaths CreateDefault(string appName)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new LauncherPaths(Path.Combine(localAppData, appName));
    }

    public DirectoryInfo GetVersionDirectory(string version)
    {
        var safeVersion = string.Concat(version.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_'));
        return new DirectoryInfo(Path.Combine(VersionsDirectory.FullName, safeVersion));
    }

    public void EnsureCreated()
    {
        RootDirectory.Create();
        VersionsDirectory.Create();
        TempDirectory.Create();
    }
}
