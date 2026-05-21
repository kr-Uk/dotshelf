namespace DotShelf.Launcher.Core;

public sealed record InstallProgress(double Percent, string Message, string? Detail = null);
