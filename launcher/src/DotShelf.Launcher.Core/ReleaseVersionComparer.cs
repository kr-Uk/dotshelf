namespace DotShelf.Launcher.Core;

public static class ReleaseVersionComparer
{
    public static int Compare(string? left, string? right)
    {
        var leftVersion = Parse(left);
        var rightVersion = Parse(right);
        return leftVersion.CompareTo(rightVersion);
    }

    private static Version Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new Version(0, 0, 0, 0);
        }

        var core = value.Split('-', '+')[0];
        var parts = core.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var normalized = parts.Length switch
        {
            1 => $"{parts[0]}.0.0.0",
            2 => $"{parts[0]}.{parts[1]}.0.0",
            3 => $"{parts[0]}.{parts[1]}.{parts[2]}.0",
            _ => core
        };

        return Version.TryParse(normalized, out var version)
            ? version
            : new Version(0, 0, 0, 0);
    }
}
