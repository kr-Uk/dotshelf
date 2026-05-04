namespace DotShelf.Launcher.Core;

public sealed class LauncherManifest
{
    public string LatestVersion { get; init; } = "";
    public bool Mandatory { get; init; }
    public string PackageUrl { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public string EntryPoint { get; init; } = "Dotshelf.exe";
    public string? MinLauncherVersion { get; init; }
    public IReadOnlyList<string> ReleaseNotes { get; init; } = Array.Empty<string>();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LatestVersion))
        {
            throw new InvalidDataException("Manifest latestVersion is required.");
        }

        if (!Uri.TryCreate(PackageUrl, UriKind.Absolute, out _))
        {
            throw new InvalidDataException("Manifest packageUrl must be an absolute URL.");
        }

        if (string.IsNullOrWhiteSpace(Sha256))
        {
            throw new InvalidDataException("Manifest sha256 is required.");
        }

        if (string.IsNullOrWhiteSpace(EntryPoint))
        {
            throw new InvalidDataException("Manifest entrypoint is required.");
        }
    }
}
