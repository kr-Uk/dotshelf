using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace DotShelf.Launcher.Core;

public sealed class PackageInstaller
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private const double DownloadStartPercent = 0;
    private const double DownloadEndPercent = 35;
    private const double VerifyEndPercent = 40;
    private const double ExtractEndPercent = 80;
    private const double CopyEndPercent = 98;

    private readonly LauncherPaths paths;
    private readonly HttpClient httpClient;

    public PackageInstaller(LauncherPaths paths, HttpClient? httpClient = null)
    {
        this.paths = paths;
        this.httpClient = httpClient ?? new HttpClient();
    }

    public async Task<LauncherInstallState?> GetInstallStateAsync(CancellationToken cancellationToken = default)
    {
        if (!paths.StateFile.Exists)
        {
            return null;
        }

        await using var stream = paths.StateFile.OpenRead();
        return await JsonSerializer.DeserializeAsync<LauncherInstallState>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> NeedsInstallOrUpdateAsync(LauncherManifest manifest, CancellationToken cancellationToken = default)
    {
        var state = await GetInstallStateAsync(cancellationToken).ConfigureAwait(false);
        if (state == null || !HasInstalledExecutable(state))
        {
            return true;
        }

        return ReleaseVersionComparer.Compare(state.InstalledVersion, manifest.LatestVersion) < 0;
    }

    public bool HasInstalledExecutable(LauncherInstallState? state)
    {
        if (state == null ||
            string.IsNullOrWhiteSpace(state.InstallPath) ||
            string.IsNullOrWhiteSpace(state.EntryPoint))
        {
            return false;
        }

        return File.Exists(Path.Combine(state.InstallPath, state.EntryPoint));
    }

    public async Task<LauncherInstallState> InstallOrUpdateAsync(
        LauncherManifest manifest,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        manifest.Validate();
        paths.EnsureCreated();

        var workDirectory = new DirectoryInfo(Path.Combine(paths.TempDirectory.FullName, Guid.NewGuid().ToString("N")));
        workDirectory.Create();
        Report(progress, 0, "설치 준비 중...");

        try
        {
            var packagePath = Path.Combine(workDirectory.FullName, "package.zip");
            var extractDirectory = Path.Combine(workDirectory.FullName, "extract");

            await DownloadAsync(new Uri(manifest.PackageUrl), packagePath, progress, cancellationToken)
                .ConfigureAwait(false);

            Report(progress, DownloadEndPercent, "다운로드 완료");
            Report(progress, 36, "무결성 검사 중...");
            var actualHash = await ComputeSha256Async(packagePath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(actualHash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Package hash mismatch. Expected {manifest.Sha256}, got {actualHash}.");
            }

            Report(progress, VerifyEndPercent, "압축 해제 준비 중...");
            ExtractPackage(packagePath, extractDirectory, progress, cancellationToken);

            var entryPointPath = Directory.GetFiles(extractDirectory, manifest.EntryPoint, SearchOption.AllDirectories)
                .FirstOrDefault();
            if (entryPointPath == null)
            {
                throw new FileNotFoundException($"Entrypoint '{manifest.EntryPoint}' was not found in the package.");
            }

            var appRoot = Path.GetDirectoryName(entryPointPath)
                ?? throw new InvalidDataException("Unable to resolve package root.");
            var versionDirectory = paths.GetVersionDirectory(manifest.LatestVersion);
            if (versionDirectory.Exists)
            {
                Report(progress, ExtractEndPercent, "기존 설치 파일 정리 중...");
                versionDirectory.Delete(recursive: true);
            }

            CopyDirectory(appRoot, versionDirectory.FullName, progress, cancellationToken);
            Report(progress, CopyEndPercent, "설치 정보 저장 중...");

            var state = new LauncherInstallState
            {
                InstalledVersion = manifest.LatestVersion,
                InstallPath = versionDirectory.FullName,
                EntryPoint = manifest.EntryPoint,
                InstalledAt = DateTimeOffset.UtcNow
            };

            await WriteStateAsync(state, cancellationToken).ConfigureAwait(false);
            Report(progress, 100, "설치 완료");
            return state;
        }
        finally
        {
            if (workDirectory.Exists)
            {
                workDirectory.Delete(recursive: true);
            }
        }
    }

    public async Task<Process> LaunchAsync(CancellationToken cancellationToken = default)
    {
        var state = await GetInstallStateAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Dotshelf is not installed.");

        var entryPointPath = Path.Combine(state.InstallPath, state.EntryPoint);
        if (!File.Exists(entryPointPath))
        {
            throw new FileNotFoundException("Installed entrypoint was not found.", entryPointPath);
        }

        var startInfo = new ProcessStartInfo(entryPointPath)
        {
            WorkingDirectory = state.InstallPath,
            UseShellExecute = true
        };

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start Dotshelf.");
    }

    public async Task DeleteInstalledAppAsync(CancellationToken cancellationToken = default)
    {
        var state = await GetInstallStateAsync(cancellationToken).ConfigureAwait(false);
        if (state == null)
        {
            return;
        }

        var installPath = Path.GetFullPath(state.InstallPath);
        var versionsPath = Path.GetFullPath(paths.VersionsDirectory.FullName);
        var isInsideVersionsDirectory =
            installPath.StartsWith(versionsPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(installPath, versionsPath, StringComparison.OrdinalIgnoreCase);

        if (!isInsideVersionsDirectory)
        {
            throw new InvalidOperationException("Installed path is outside the launcher versions directory.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (Directory.Exists(installPath))
        {
            Directory.Delete(installPath, recursive: true);
        }

        if (paths.StateFile.Exists)
        {
            paths.StateFile.Delete();
        }
    }

    private async Task DownloadAsync(
        Uri packageUri,
        string destinationPath,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(packageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = File.Create(destinationPath);

        var buffer = new byte[1024 * 128];
        long totalRead = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            totalRead += read;

            if (totalBytes > 0)
            {
                var downloadPercent = DownloadStartPercent +
                    Math.Min(DownloadEndPercent - DownloadStartPercent, totalRead * (DownloadEndPercent - DownloadStartPercent) / totalBytes.Value);
                Report(progress, downloadPercent, "다운로드 중...", $"{FormatBytes(totalRead)} / {FormatBytes(totalBytes.Value)}");
            }
        }
    }

    private async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task WriteStateAsync(LauncherInstallState state, CancellationToken cancellationToken)
    {
        paths.EnsureCreated();
        await using var stream = File.Create(paths.StateFile.FullName);
        await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static void ExtractPackage(
        string packagePath,
        string extractDirectory,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(extractDirectory);
        using var archive = ZipFile.OpenRead(packagePath);
        var entries = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)).ToList();
        var totalBytes = entries.Sum(entry => entry.Length);
        long extractedBytes = 0;

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destinationPath = Path.GetFullPath(Path.Combine(extractDirectory, entry.FullName));
            var extractRoot = Path.GetFullPath(extractDirectory);
            if (!destinationPath.StartsWith(extractRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Package contains an unsafe path: {entry.FullName}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            using var input = entry.Open();
            using var output = File.Create(destinationPath);

            var buffer = new byte[1024 * 128];
            while (true)
            {
                var read = input.Read(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    break;
                }

                output.Write(buffer, 0, read);
                extractedBytes += read;

                if (totalBytes > 0)
                {
                    var percent = VerifyEndPercent +
                        Math.Min(ExtractEndPercent - VerifyEndPercent, extractedBytes * (ExtractEndPercent - VerifyEndPercent) / totalBytes);
                    Report(progress, percent, "압축 해제 중...", entry.FullName);
                }
            }

            if (entry.Length == 0)
            {
                var percent = totalBytes > 0
                    ? VerifyEndPercent + Math.Min(ExtractEndPercent - VerifyEndPercent, extractedBytes * (ExtractEndPercent - VerifyEndPercent) / totalBytes)
                    : VerifyEndPercent;
                Report(progress, percent, "압축 해제 중...", entry.FullName);
            }
        }
    }

    private static void CopyDirectory(
        string sourceDirectory,
        string destinationDirectory,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
        var totalBytes = files.Sum(file => new FileInfo(file).Length);
        long copiedBytes = 0;
        Directory.CreateDirectory(destinationDirectory);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var targetPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            using var input = File.OpenRead(file);
            using var output = File.Create(targetPath);
            var buffer = new byte[1024 * 128];
            while (true)
            {
                var read = input.Read(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    break;
                }

                output.Write(buffer, 0, read);
                copiedBytes += read;

                if (totalBytes > 0)
                {
                    var percent = ExtractEndPercent +
                        Math.Min(CopyEndPercent - ExtractEndPercent, copiedBytes * (CopyEndPercent - ExtractEndPercent) / totalBytes);
                    Report(progress, percent, "파일 복사 중...", relativePath);
                }
            }
        }
    }

    private static void Report(IProgress<InstallProgress>? progress, double percent, string message, string? detail = null)
    {
        progress?.Report(new InstallProgress(Math.Clamp(percent, 0, 100), message, detail));
    }

    private static string FormatBytes(long bytes)
    {
        var mib = bytes / 1024d / 1024d;
        return $"{mib:0} MB";
    }
}
