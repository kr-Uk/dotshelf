using System.Text.RegularExpressions;

namespace DotShelf.Launcher.Core;

public sealed class LauncherManifest
{
    // 완화된 SemVer: X.Y.Z + optional pre-release (-...) + optional build metadata (+...).
    // ReleaseVersionComparer 가 silent fallback (0.0.0) 하므로 invalid 한 manifest 가
    // 들어와 잘못된 비교로 update 누락 / 무한 reinstall 되는 걸 fetch 시점에 차단.
    private static readonly Regex SemVerPattern = new(
        @"^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?(\+[A-Za-z0-9.-]+)?$",
        RegexOptions.Compiled);

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

        if (!SemVerPattern.IsMatch(LatestVersion))
        {
            throw new InvalidDataException($"Manifest latestVersion is not valid SemVer: {LatestVersion}");
        }

        if (!string.IsNullOrWhiteSpace(MinLauncherVersion) && !SemVerPattern.IsMatch(MinLauncherVersion))
        {
            throw new InvalidDataException($"Manifest minLauncherVersion is not valid SemVer: {MinLauncherVersion}");
        }

        // packageUrl: HTTPS 강제. http:// (MITM 위험) / file:// (임의 로컬 파일을
        // 패키지로 가장) 차단. 배너 linkUrl 은 이미 https-only 인데 packageUrl 이
        // 더 위험한 통로(설치 후 실행되는 바이너리)이므로 동일 강제.
        if (!Uri.TryCreate(PackageUrl, UriKind.Absolute, out var packageUri) ||
            packageUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidDataException("Manifest packageUrl must be an HTTPS URL.");
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
