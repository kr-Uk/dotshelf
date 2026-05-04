namespace DotShelf.Launcher.Core;

public sealed class EventsManifest
{
    public int SchemaVersion { get; init; } = 1;
    public IReadOnlyList<EventBanner> Banners { get; init; } = Array.Empty<EventBanner>();
}

public sealed class EventBanner
{
    public string Id { get; init; } = "";
    public string Type { get; init; } = "";
    public string Title { get; init; } = "";
    public string? LinkUrl { get; init; }
    public DateTimeOffset? DisplayFrom { get; init; }
    public DateTimeOffset? DisplayTo { get; init; }
}
