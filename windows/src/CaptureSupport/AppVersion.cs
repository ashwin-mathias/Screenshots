using System.Globalization;
using System.Text.RegularExpressions;

namespace Screenshot.Support;

/// <summary>An X.Y.Z version. Release tags are "v" + X.Y.Z (UPD-002).</summary>
public readonly partial record struct AppVersion(int Major, int Minor, int Patch) : IComparable<AppVersion>
{
    public static AppVersion? ParseTag(string? tag)
    {
        if (tag is null) return null;
        var m = TagPattern().Match(tag);
        return m.Success ? FromMatch(m) : null;
    }

    public static AppVersion? Parse(string? text)
    {
        if (text is null) return null;
        var m = PlainPattern().Match(text.Trim());
        return m.Success ? FromMatch(m) : null;
    }

    private static AppVersion? FromMatch(Match m)
    {
        // int.TryParse guards against absurdly long numbers.
        if (!int.TryParse(m.Groups[1].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var a)) return null;
        if (!int.TryParse(m.Groups[2].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var b)) return null;
        if (!int.TryParse(m.Groups[3].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var c)) return null;
        return new AppVersion(a, b, c);
    }

    public int CompareTo(AppVersion other) =>
        Major != other.Major ? Major.CompareTo(other.Major)
        : Minor != other.Minor ? Minor.CompareTo(other.Minor)
        : Patch.CompareTo(other.Patch);

    public static bool operator >(AppVersion a, AppVersion b) => a.CompareTo(b) > 0;
    public static bool operator <(AppVersion a, AppVersion b) => a.CompareTo(b) < 0;
    public static bool operator >=(AppVersion a, AppVersion b) => a.CompareTo(b) >= 0;
    public static bool operator <=(AppVersion a, AppVersion b) => a.CompareTo(b) <= 0;

    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    [GeneratedRegex(@"^v([0-9]+)\.([0-9]+)\.([0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"^([0-9]+)\.([0-9]+)\.([0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex PlainPattern();
}
