using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Screenshot.Interop;

/// <summary>Light/dark mode from the Windows settings, and the few custom brushes the app draws with.</summary>
internal static class Theme
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool TaskbarIsLight => ReadFlag("SystemUsesLightTheme", fallback: false);
    public static bool AppsAreLight => ReadFlag("AppsUseLightTheme", fallback: true);

    private static bool ReadFlag(string name, bool fallback)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(Key);
            return k?.GetValue(name) is int v ? v != 0 : fallback;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            return fallback;
        }
    }

    /// <summary>Sets the app's own brushes (DESIGN §3, §5). Controls use the Fluent theme.</summary>
    public static void ApplyBrushes(ResourceDictionary resources)
    {
        var light = AppsAreLight;
        Set(resources, "ShotSurface", light ? Color.FromRgb(0xF9, 0xF9, 0xF9) : Color.FromRgb(0x2B, 0x2B, 0x2B));
        Set(resources, "ShotCard", light ? Color.FromRgb(0xFF, 0xFF, 0xFF) : Color.FromRgb(0x32, 0x32, 0x32));
        Set(resources, "ShotStroke", light ? Color.FromArgb(0x24, 0, 0, 0) : Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
        Set(resources, "ShotText", light ? Color.FromRgb(0x1A, 0x1A, 0x1A) : Color.FromRgb(0xFF, 0xFF, 0xFF));
        Set(resources, "ShotTextSecondary", light ? Color.FromRgb(0x5D, 0x5D, 0x5D) : Color.FromRgb(0xC5, 0xC5, 0xC5));
        Set(resources, "ShotKeyCap", light ? Color.FromRgb(0xF0, 0xF0, 0xF0) : Color.FromRgb(0x3A, 0x3A, 0x3A));
        Set(resources, "ShotAccent", SystemParameters.WindowGlassColor.A == 0 ? Color.FromRgb(0x2F, 0x6F, 0xE4) : AccentColor());
    }

    private static Color AccentColor()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (k?.GetValue("AccentColor") is int abgr)
                return Color.FromRgb((byte)(abgr & 0xFF), (byte)((abgr >> 8) & 0xFF), (byte)((abgr >> 16) & 0xFF));
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException) { }
        return Color.FromRgb(0x2F, 0x6F, 0xE4);
    }

    private static void Set(ResourceDictionary r, string key, Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        r[key] = b;
    }
}
