using System.Net.Http.Json;
using System.Text.Json;

namespace DotShelf.Launcher.Core;

public sealed class ManifestClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly HttpClient httpClient;

    public ManifestClient(HttpClient? httpClient = null)
    {
        this.httpClient = httpClient ?? new HttpClient();
    }

    public async Task<LauncherManifest> GetLatestAsync(Uri manifestUri, CancellationToken cancellationToken)
    {
        var manifest = await httpClient.GetFromJsonAsync<LauncherManifest>(manifestUri, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (manifest == null)
        {
            throw new InvalidDataException("Manifest response was empty.");
        }

        manifest.Validate();
        return manifest;
    }

    public async Task<long?> GetPackageSizeAsync(Uri packageUri, CancellationToken cancellationToken)
    {
        try
        {
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, packageUri);
            using var headResponse = await httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (headResponse.IsSuccessStatusCode && headResponse.Content.Headers.ContentLength.HasValue)
            {
                return headResponse.Content.Headers.ContentLength.Value;
            }
        }
        catch (HttpRequestException)
        {
            // Some static servers do not support HEAD. Fall back to a header-only GET below.
        }

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, packageUri);
        using var getResponse = await httpClient.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        getResponse.EnsureSuccessStatusCode();
        return getResponse.Content.Headers.ContentLength;
    }
}
