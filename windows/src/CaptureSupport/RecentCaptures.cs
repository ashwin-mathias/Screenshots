namespace Screenshot.Support;

/// <summary>One file in the screenshots folder.</summary>
public sealed record RecentCapture(string Path, DateTime ModifiedUtc)
{
    public string FileName => System.IO.Path.GetFileName(Path);
    public string DisplayName => FileNaming.DisplayName(FileName);
}

public sealed class RecentCapturesException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Reads the newest PNG files from the folder (REC-001, REC-002). Stateless; nothing is cached.</summary>
public static class RecentCaptures
{
    public const int DefaultLimit = 20;

    public static IReadOnlyList<RecentCapture> Scan(string folder, int limit = DefaultLimit)
    {
        if (limit <= 0) return [];
        DirectoryInfo dir = new(folder);
        IEnumerable<FileSystemInfo> entries;
        try
        {
            if (!dir.Exists) throw new DirectoryNotFoundException($"{folder} doesn't exist.");
            entries = dir.EnumerateFileSystemInfos("*", new EnumerationOptions
            {
                RecurseSubdirectories = false,
                AttributesToSkip = 0,          // filter ourselves, so the rules are explicit
                IgnoreInaccessible = true,
                MatchType = MatchType.Simple,
            }).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            throw new RecentCapturesException("Could not read screenshots folder", e);
        }

        return entries
            .Where(IsEligible)
            .Select(e => new RecentCapture(e.FullName, e.LastWriteTimeUtc))
            .OrderByDescending(r => r.ModifiedUtc)
            .ThenBy(r => r.FileName, StringComparer.Ordinal)
            .Take(limit)
            .ToList();
    }

    /// <summary>Regular, visible .png files only: no folders, links, hidden or system files.</summary>
    public static bool IsEligible(FileSystemInfo entry)
    {
        if (entry is not FileInfo) return false;
        if (entry.Name.StartsWith('.')) return false;
        if (!entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return false;
        var a = entry.Attributes;
        if ((a & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            return false;
        if (entry.LinkTarget is not null) return false;
        return true;
    }

    /// <summary>Is the path still a regular file right now (ACT-001)?</summary>
    public static bool IsRegularFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists
                && (info.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0
                && info.LinkTarget is null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }
}
