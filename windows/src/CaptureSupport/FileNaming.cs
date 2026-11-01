using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Screenshot.Support;

/// <summary>File names for saved screenshots (STO-003) and their menu labels (REC-003).</summary>
public static partial class FileNaming
{
    /// <summary>"Screenshot yyyy-MM-dd at HH.mm.ss.fff-&lt;id&gt;.png", in the given local time.</summary>
    public static string FileName(DateTime localTime, string? identifier = null)
    {
        var id = identifier is null ? NewIdentifier() : Sanitize(identifier) ?? NewIdentifier();
        var stamp = localTime.ToString("yyyy-MM-dd 'at' HH.mm.ss.fff", CultureInfo.InvariantCulture);
        return $"Screenshot {stamp}-{id}.png";
    }

    /// <summary>A new upper-case UUID, the default identifier.</summary>
    public static string NewIdentifier() => Guid.NewGuid().ToString("D").ToUpperInvariant();

    /// <summary>Keeps ASCII letters, digits and '-', so an identifier can never become a path. Null if nothing is left.</summary>
    public static string? Sanitize(string identifier)
    {
        var sb = new StringBuilder(identifier.Length);
        foreach (var c in identifier)
        {
            if (c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')
                sb.Append(c);
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

    /// <summary>The menu label: no ".png" and no trailing "-&lt;UUID&gt;".</summary>
    public static string DisplayName(string fileName)
    {
        var name = fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? fileName[..^4] : fileName;
        var m = TrailingUuid().Match(name);
        return m.Success ? name[..m.Index] : name;
    }

    [GeneratedRegex("-[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}$")]
    private static partial Regex TrailingUuid();
}
