using System.Net.Http.Json;
using System.Text.Json;

namespace DotShelf.Launcher.Core;

public sealed class EventsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient httpClient;

    public EventsClient(HttpClient? httpClient = null)
    {
        this.httpClient = httpClient ?? new HttpClient();
    }

    public async Task<EventsManifest> GetAsync(Uri eventsUri, CancellationToken cancellationToken)
    {
        var manifest = await httpClient.GetFromJsonAsync<EventsManifest>(eventsUri, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return manifest ?? new EventsManifest();
    }
}
