namespace Screenshot.Services;

/// <summary>On-screen text, exactly as in SPEC.md. Keep in step with the macOS app.</summary>
internal static class Strings
{
    public const string AppName = "Screenshotz";

    // status line
    public const string Ready = "Ready to capture";
    public const string Selecting = "Select an area · Esc to cancel";
    public const string SavedAndCopied = "Saved and copied to clipboard";
    public const string Cancelled = "Capture cancelled";
    public const string NeedsAttention = "Screenshotz needs attention";
    public const string CopiedRecent = "Copied screenshot to clipboard";
    public const string PreviewUnavailable = "Screenshot preview unavailable · Use Recent Screenshots to open it";
    public const string CheckingUpdates = "Checking for updates…";
    public static string Downloading(int percent) => $"Downloading update… {percent}%";
    public const string Installing = "Installing update…";
    public static string UpdatedTo(string v) => $"Updated to Screenshotz {v}";

    // preview captions
    public const string CaptionSavedAndCopied = "Saved and copied";
    public const string CaptionCopyRetry = "Saved · Click Copy to retry";
    public const string CaptionRecent = "Recent screenshot";

    // menu
    public const string CaptureArea = "Capture Area";
    public const string OpenFolder = "Open Screenshots Folder";
    public const string RevealLast = "Reveal Last Screenshot";
    public const string Recent = "Recent Screenshots";
    public const string ShowLastPreview = "Show Last Screenshot Preview";
    public const string ShowThumbnail = "Show Thumbnail After Capture";
    public const string EnableAltS = "Enable Alt+S Shortcut";
    public const string EnablePrintScreen = "Enable Print Screen Shortcut";
    public const string LaunchAtLogin = "Launch at Login";
    public const string LaunchAtLoginSettings = "Launch at Login — Turn On in Settings…";
    public const string AutoUpdate = "Check for Updates Automatically";
    public const string CheckForUpdates = "Check for Updates…";
    public const string HowToUse = "How to Use Screenshotz…";
    public const string HelpAbout = "Help & About Screenshotz…";
    public const string Quit = "Quit Screenshotz";
    public static string InstallUpdate(string v) => $"Install Update {v}…";
    public const string CancelUpdate = "Cancel Update";
    public const string Loading = "Loading screenshots…";
    public const string NoScreenshots = "No screenshots yet";
    public const string FolderUnreadable = "Could not read screenshots folder";

    // recent actions
    public const string Open = "Open";
    public const string CopyImage = "Copy Image";
    public const string RevealInExplorer = "Show in File Explorer";
    public const string ShowPreview = "Show Preview";
    public const string MarkUp = "Mark Up in Paint…";

    // alerts
    public const string CaptureFailed = "Capture could not finish.";
    public static string SavedNotCopied(string detail) => $"The screenshot was saved, but it couldn't be copied to the clipboard. {detail}";
    public static string ActionFailed(string action) => $"Couldn't {action} that screenshot. It may have been moved or deleted.";
    public const string PrintScreenTaken =
        "Windows is using Print Screen for its own screen capture. Turn off \"Use the Print screen key to open screen capture\" in Settings, then turn this shortcut on again.";
    public static string ShortcutTaken(string name, int code) =>
        $"The {name} shortcut couldn't be turned on because another app is already using it (Windows error {code}). You can still use Capture Area in the menu.";
    public const string UpToDatePrefix = "You're up to date. Screenshotz ";
    public const string UpToDateSuffix = " is the latest version.";
    public const string UpdateCheckFailed = "Couldn't check for updates.";
    public const string UpdateUnsafe = "The update didn't pass its safety check, so it wasn't installed.";
    public const string UpdatesNeedInstalledApp = "Updates work only in the installed app (from the .msix).";

    // help
    public static string Privacy => AppInfo.IsStoreBuild
        ? "Screenshots stay on this computer. Screenshotz doesn't go online; the Microsoft Store handles updates."
        : "Screenshots stay on this computer. Screenshotz only goes online to check GitHub for updates.";
    public const string Licence = "MIT licence · github.com/ashwin-mathias/Screenshots";
    public const string RepoUrl = "https://github.com/ashwin-mathias/Screenshots";
}
