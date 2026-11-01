using System.Text.Json;

namespace Screenshot.Support;

public enum UpdateIgnoreReason
{
    UpToDate,
    InvalidTag,
    Draft,
    Prerelease,
    Skipped,
    MissingAsset,
    MissingChecksum,
    BadUrl,
    BadResponse,
}

/// <summary>An update the user can be asked about (UPD-003).</summary>
public sealed record UpdateOffer(AppVersion Version, string Notes, Uri AssetUrl, Uri ChecksumUrl);

/// <summary>Either an offer or the reason there isn't one.</summary>
public sealed record UpdateCheckResult(UpdateOffer? Offer, UpdateIgnoreReason? Reason)
{
    public static UpdateCheckResult Offered(UpdateOffer offer) => new(offer, null);
    public static UpdateCheckResult Ignored(UpdateIgnoreReason reason) => new(null, reason);
}

/// <summary>Reads GitHub's "latest release" JSON and decides whether to offer it (UPD-002).</summary>
public static class UpdateFeed
{
    public const string Repository = "ashwin-mathias/Screenshots";
    public const string LatestReleaseApi = "https://api.github.com/repos/" + Repository + "/releases/latest";
    public const string DownloadPrefix = "https://github.com/" + Repository + "/releases/download/";
    public const string ReleasesPage = "https://github.com/" + Repository + "/releases/latest";
    public const string WindowsAsset = "Screenshotz-win-x64.msix";
    public const string MacAsset = "Screenshotz-mac.zip";

    public static UpdateCheckResult Evaluate(
        string releaseJson,
        AppVersion current,
        string assetName,
        AppVersion? skipped = null,
        bool manual = false,
        string downloadPrefix = DownloadPrefix)
    {
        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(releaseJson);
            root = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return UpdateCheckResult.Ignored(UpdateIgnoreReason.BadResponse);
        }
        if (root.ValueKind != JsonValueKind.Object) return UpdateCheckResult.Ignored(UpdateIgnoreReason.BadResponse);

        if (Bool(root, "draft")) return UpdateCheckResult.Ignored(UpdateIgnoreReason.Draft);
        if (Bool(root, "prerelease")) return UpdateCheckResult.Ignored(UpdateIgnoreReason.Prerelease);

        if (AppVersion.ParseTag(Str(root, "tag_name")) is not { } version)
            return UpdateCheckResult.Ignored(UpdateIgnoreReason.InvalidTag);
        if (version <= current) return UpdateCheckResult.Ignored(UpdateIgnoreReason.UpToDate);
        if (!manual && skipped is { } s && s == version) return UpdateCheckResult.Ignored(UpdateIgnoreReason.Skipped);

        var assets = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.TryGetProperty("assets", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in list.EnumerateArray())
            {
                if (Str(a, "name") is { } name && Str(a, "browser_download_url") is { } url)
                    assets.TryAdd(name, url);
            }
        }
        if (!assets.TryGetValue(assetName, out var assetUrl)) return UpdateCheckResult.Ignored(UpdateIgnoreReason.MissingAsset);
        if (!assets.TryGetValue(assetName + ".sha256", out var checksumUrl)) return UpdateCheckResult.Ignored(UpdateIgnoreReason.MissingChecksum);
        if (!IsTrustedUrl(assetUrl, downloadPrefix) || !IsTrustedUrl(checksumUrl, downloadPrefix))
            return UpdateCheckResult.Ignored(UpdateIgnoreReason.BadUrl);

        var notes = Str(root, "body") ?? "";
        return UpdateCheckResult.Offered(new UpdateOffer(version, notes, new Uri(assetUrl), new Uri(checksumUrl)));
    }

    /// <summary>An https link inside this repo's release downloads.</summary>
    public static bool IsTrustedUrl(string url, string downloadPrefix = DownloadPrefix) =>
        url.StartsWith(downloadPrefix, StringComparison.Ordinal)
        && Uri.TryCreate(url, UriKind.Absolute, out var u)
        && u.Scheme == Uri.UriSchemeHttps
        && !url.Contains("/../", StringComparison.Ordinal);

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

/// <summary>Parses a published ".sha256" file (UPD-004).</summary>
public static class ChecksumFile
{
    /// <summary>The 64-character lower-case hex hash, or null if the text isn't a valid checksum file.</summary>
    public static string? Parse(string text)
    {
        var t = text.AsSpan().TrimStart();
        var end = 0;
        while (end < t.Length && !char.IsWhiteSpace(t[end])) end++;
        var token = t[..end];
        if (token.Length != 64) return null;
        foreach (var c in token)
            if (!char.IsAsciiHexDigit(c)) return null;
        return token.ToString().ToLowerInvariant();
    }

    public static string Sha256Hex(Stream stream) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(stream));
}
