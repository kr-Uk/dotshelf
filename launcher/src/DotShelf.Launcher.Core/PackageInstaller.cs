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

            // 새 버전 설치 직후 옛 버전 폴더들 정리 — 사용자 PC 디스크 누적 차단.
            // 직전 사용자 인지 없이 N × ~570 MB 가 쌓이던 문제 fix. R2 서버측 retention 과 같은 결.
            CleanupOldVersions(versionDirectory, cancellationToken);

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

        if (string.IsNullOrWhiteSpace(state.InstallPath) || string.IsNullOrWhiteSpace(state.EntryPoint))
        {
            throw new InvalidOperationException("Install state is incomplete.");
        }

        var installPath = Path.GetFullPath(state.InstallPath);
        var versionsPath = Path.GetFullPath(paths.VersionsDirectory.FullName);
        if (!IsContainedIn(installPath, versionsPath))
        {
            throw new InvalidOperationException("Installed path is outside the launcher versions directory.");
        }

        var entryPointPath = Path.GetFullPath(Path.Combine(installPath, state.EntryPoint));
        if (!IsContainedIn(entryPointPath, installPath))
        {
            throw new InvalidOperationException("Entry point is outside the install directory.");
        }

        if (!entryPointPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Entry point must be an .exe.");
        }

        if (!File.Exists(entryPointPath))
        {
            throw new FileNotFoundException("Installed entrypoint was not found.", entryPointPath);
        }

        // UseShellExecute=false: Windows ShellExecute (확장자 verb) 우회. 정확히 .exe 만
        // CreateProcess 로 직접 실행 — .bat/.lnk/.html 같은 임의 verb 실행 차단.
        var startInfo = new ProcessStartInfo(entryPointPath)
        {
            WorkingDirectory = installPath,
            UseShellExecute = false
        };

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start Dotshelf.");
    }

    public async Task DeleteInstalledAppAsync(CancellationToken cancellationToken = default)
    {
        // 안전 가드 — versions/ 가 root 안에 있는지 확인. 사용자가 install path 를
        // 옮긴 뒤 root 가 망가졌거나 외부 경로가 들어오면 즉시 throw.
        var versionsPath = Path.GetFullPath(paths.VersionsDirectory.FullName);
        var rootPath = Path.GetFullPath(paths.RootDirectory.FullName);
        if (!IsContainedIn(versionsPath, rootPath))
        {
            throw new InvalidOperationException("versions 디렉터리가 RootDirectory 밖에 있습니다.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // 옛 버전 누적 (CleanupOldVersions 도입 전 N × ~570 MB 쌓였던 것) 까지 함께 정리.
        // 사용자 입장 "DotShelf 앱 삭제" = '디스크에서 통째 비우기' 직관과 일치.
        if (paths.VersionsDirectory.Exists)
        {
            paths.VersionsDirectory.Delete(recursive: true);
        }

        if (paths.StateFile.Exists)
        {
            paths.StateFile.Delete();
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    // R2 서버측 retention 정책 (최근 3개 유지) 과 동일. 현재 버전 + 직전 2개까지 보존해
    // 다운그레이드 안전망 1단계 (옛 빌드 깨졌을 때 사용자 PC 에 직전 버전이 살아있어
    // state.json 만 되돌리면 즉시 복귀 가능).
    private const int VersionsRetentionKeep = 3;

    private void CleanupOldVersions(DirectoryInfo currentVersion, CancellationToken cancellationToken)
    {
        if (!paths.VersionsDirectory.Exists)
        {
            return;
        }

        var versionsRoot = Path.GetFullPath(paths.VersionsDirectory.FullName);
        var currentPath = Path.GetFullPath(currentVersion.FullName);

        // 안전 가드를 통과한 디렉터리만 수집 — VersionsDirectory 밖으로 절대 못 나감
        // (symlink 경유 등).
        var safeDirs = paths.VersionsDirectory.EnumerateDirectories()
            .Where(d => IsContainedIn(Path.GetFullPath(d.FullName), versionsRoot))
            .ToList();

        // 보존 set 구성: 현재 버전 + 옛 버전들 중 SemVer 내림차순 상위 (Keep-1) 개.
        // 폴더 이름 자체가 SemVer (paths.GetVersionDirectory 가 생성한 형식) 라 그대로 비교.
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { currentPath };
        var olderPaths = safeDirs
            .Select(d => new { Dir = d, FullPath = Path.GetFullPath(d.FullName) })
            .Where(x => !string.Equals(x.FullPath, currentPath, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Dir.Name, Comparer<string>.Create(ReleaseVersionComparer.Compare))
            .Take(Math.Max(0, VersionsRetentionKeep - 1))
            .Select(x => x.FullPath);
        foreach (var p in olderPaths)
        {
            keep.Add(p);
        }

        foreach (var dir in safeDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fullPath = Path.GetFullPath(dir.FullName);
            if (keep.Contains(fullPath))
            {
                continue;
            }

            try
            {
                dir.Delete(recursive: true);
            }
            catch (IOException ex)
            {
                // 옛 버전 .exe 가 다른 프로세스에서 실행 중인 경우 등 IO 충돌은 무시 —
                // 다음 업데이트 사이클에서 다시 시도. cleanup 실패가 설치 자체를 깨뜨리지 않도록.
                // 다만 "왜 옛 폴더가 안 지워졌지?" 추적용 Trace 로깅.
                Trace.WriteLine($"[CleanupOldVersions] IO skip: '{dir.FullName}' - {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Trace.WriteLine($"[CleanupOldVersions] access skip: '{dir.FullName}' - {ex.Message}");
            }
        }
    }

    // root containment 검사 헬퍼 — `Path.GetFullPath` 가 trailing separator 를 유지하는
    // 환경 (예: 사용자가 InstallRoot = "C:\\Temp\\DotShelf\\" 로 설정 저장) 에서
    // `parent + sep` 가 `C:\\Temp\\DotShelf\\\\` 두 sep 가 되어 정상 child 도 StartsWith
    // 가 false 가 되던 trailing separator 오탐 차단 (MR #165 리뷰 P1).
    // LaunchAsync / DeleteInstalledAppAsync / CleanupOldVersions / ExtractPackage 의 5개
    // containment 체크가 모두 이 헬퍼로 단일화 — 향후 회귀 줄임.
    private static bool IsContainedIn(string childFullPath, string parentFullPath)
    {
        var normalizedParent = Path.TrimEndingDirectorySeparator(parentFullPath);
        return childFullPath.StartsWith(normalizedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
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
            if (!IsContainedIn(destinationPath, extractRoot))
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
