namespace DotShelf.Launcher.Core;

public sealed class LauncherInstallState
{
    public string InstalledVersion { get; init; } = "";
    public string InstallPath { get; init; } = "";
    public string EntryPoint { get; init; } = "";
    public DateTimeOffset InstalledAt { get; init; } = DateTimeOffset.UtcNow;
}
