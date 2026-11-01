using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Screenshot.Services;

/// <summary>The app's saved choices (SPEC §16). Missing or unreadable file = defaults.</summary>
internal sealed class Settings
{
    public bool HasLaunched { get; set; }
    public bool AltSDisabled { get; set; }
    public bool PrintScreenEnabled { get; set; }
    public bool CapturePreviewDisabled { get; set; }
    public bool AutoUpdateCheckDisabled { get; set; }
    public DateTime? LastUpdateCheck { get; set; }
    public string? SkippedVersion { get; set; }
    /// <summary>Set just before installing an update, to say "Updated to …" once after the restart.</summary>
    public string? PendingUpdateVersion { get; set; }

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Screenshot");
    private static string FilePath => Path.Combine(Folder, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new Settings();
            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json) ?? new Settings();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Log.Write($"Settings unreadable, using defaults: {e.Message}");
            try { File.Move(FilePath, Path.Combine(Folder, "settings.bad.json"), overwrite: true); }
            catch (Exception m) when (m is IOException or UnauthorizedAccessException) { }
            return new Settings();
        }
    }

    /// <summary>Writes a temp file, then replaces the old one, so a crash never leaves half a file.</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, Json));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write($"Couldn't save settings: {e.Message}");
        }
    }
}

/// <summary>A small local log for diagnosing update and capture problems. No screenshot data, no personal data.</summary>
internal static class Log
{
    private static readonly object Gate = new();

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Settings.Folder);
                var path = Path.Combine(Settings.Folder, "screenshot.log");
                var info = new FileInfo(path);
                if (info.Exists && info.Length > 256 * 1024) File.Move(path, path + ".old", overwrite: true);
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
