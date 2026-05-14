using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using App.Infrastructure.Api.Dtos;
using UnityEngine;
using UnityEngine.Networking;

namespace App.Infrastructure.Assets
{
    public sealed class RemoteAssetCacheService
    {
        private const string CacheDirectoryName = "DotShelfRemoteAssets";

        public RemoteAssetCacheService(string cacheRoot = null)
        {
            CacheRoot = string.IsNullOrWhiteSpace(cacheRoot)
                ? Path.Combine(UnityEngine.Application.persistentDataPath, CacheDirectoryName)
                : cacheRoot;
        }

        public string CacheRoot { get; }

        public IEnumerator EnsureManifestCached(
            DownloadManifestResponseDto manifest,
            Action<IReadOnlyList<string>> onSuccess,
            Action<string> onError)
        {
            var cachedPaths = new List<string>();
            var files = manifest?.Files;
            if (files == null || files.Count == 0)
            {
                onSuccess?.Invoke(cachedPaths);
                yield break;
            }

            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                if (file == null || string.IsNullOrWhiteSpace(file.Url))
                {
                    continue;
                }

                var failed = false;
                var cachedPath = string.Empty;
                yield return EnsureFileCached(
                    file,
                    path => cachedPath = path,
                    error =>
                    {
                        failed = true;
                        onError?.Invoke(error);
                    });

                if (failed && file.Required)
                {
                    yield break;
                }

                if (!string.IsNullOrWhiteSpace(cachedPath))
                {
                    cachedPaths.Add(cachedPath);
                }
            }

            onSuccess?.Invoke(cachedPaths);
        }

        public IEnumerator EnsureFileCached(DownloadFileResponseDto file, Action<string> onSuccess, Action<string> onError)
        {
            if (file == null)
            {
                onError?.Invoke("Remote asset file is empty.");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(file.Url))
            {
                onError?.Invoke($"Remote asset url is empty: assetId={file.AssetId}");
                yield break;
            }

            var cachePath = BuildCachePath(file);
            if (IsValidCachedFile(cachePath, file))
            {
                onSuccess?.Invoke(cachePath);
                yield break;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(cachePath) ?? CacheRoot);
            var tempPath = cachePath + ".download";
            DeleteIfExists(tempPath);

            using (var request = UnityWebRequest.Get(file.Url))
            {
                request.downloadHandler = new DownloadHandlerFile(tempPath)
                {
                    removeFileOnAbort = true
                };

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    DeleteIfExists(tempPath);
                    onError?.Invoke($"Remote asset download failed: assetId={file.AssetId}, error={request.error}");
                    yield break;
                }
            }

            if (!IsValidCachedFile(tempPath, file))
            {
                DeleteIfExists(tempPath);
                onError?.Invoke($"Remote asset validation failed: assetId={file.AssetId}, code={file.Code}");
                yield break;
            }

            DeleteIfExists(cachePath);
            File.Move(tempPath, cachePath);
            onSuccess?.Invoke(cachePath);
        }

        public string BuildCachePath(DownloadFileResponseDto file)
        {
            var version = file != null && file.Version > 0 ? file.Version : 1;
            var hashSegment = SafeSegment(string.IsNullOrWhiteSpace(file?.FileHash) ? "nohash" : file.FileHash);
            var fileName = SafeSegment(FirstNonEmpty(file?.Code, file?.Name, $"asset-{file?.AssetId ?? 0L}"));
            var extension = ResolveExtension(file);
            return Path.Combine(
                CacheRoot,
                $"asset-{file?.AssetId ?? 0L}",
                $"v{version}",
                hashSegment,
                fileName + extension);
        }

        public bool IsValidCachedFile(string path, DownloadFileResponseDto file)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            var info = new FileInfo(path);
            if (file?.FileSize != null && file.FileSize.Value > 0 && info.Length != file.FileSize.Value)
            {
                return false;
            }

            return HashMatches(path, file?.FileHash);
        }

        private static bool HashMatches(string path, string expectedHash)
        {
            if (string.IsNullOrWhiteSpace(expectedHash))
            {
                return true;
            }

            var normalized = expectedHash.Trim();
            if (normalized.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring("sha256:".Length);
            }

            if (normalized.Length != 64)
            {
                return true;
            }

            using (var stream = File.OpenRead(path))
            using (var sha256 = SHA256.Create())
            {
                var actual = ToLowerHex(sha256.ComputeHash(stream));
                return string.Equals(actual, normalized, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static string ToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }

            return builder.ToString();
        }

        private static string ResolveExtension(DownloadFileResponseDto file)
        {
            var urlExtension = ExtensionFromUrl(file?.Url);
            if (!string.IsNullOrWhiteSpace(urlExtension))
            {
                return urlExtension;
            }

            var contentType = file?.ContentType ?? string.Empty;
            if (contentType.IndexOf("gltf-binary", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ".glb";
            }

            if (contentType.IndexOf("assetbundle", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ".bundle";
            }

            return ".bin";
        }

        private static string ExtensionFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            var path = url;
            var queryIndex = path.IndexOf('?');
            if (queryIndex >= 0)
            {
                path = path.Substring(0, queryIndex);
            }

            var extension = Path.GetExtension(path);
            return string.IsNullOrWhiteSpace(extension) || extension.Length > 16 ? null : extension;
        }

        private static string SafeSegment(string value)
        {
            var source = string.IsNullOrWhiteSpace(value) ? "asset" : value.Trim();
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(source.Length);
            for (var i = 0; i < source.Length; i++)
            {
                var ch = source[i];
                builder.Append(Array.IndexOf(invalid, ch) >= 0 || ch == ':' ? '_' : ch);
            }

            return builder.ToString();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    return values[i];
                }
            }

            return string.Empty;
        }

        private static void DeleteIfExists(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
