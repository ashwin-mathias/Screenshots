using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Screenshot.Interop;
using Screenshot.Support;

namespace Screenshot.Services;

/// <summary>Open, Reveal and Mark Up on one exact file (ACT-001, ACT-002).</summary>
internal static class FileActions
{
    public sealed class ActionException(string message) : Exception(message);

    public static void Open(string path)
    {
        Require(path, "open");
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            throw new ActionException($"Windows couldn't open that screenshot. {e.Message}");
        }
    }

    public static void Reveal(string path)
    {
        Require(path, "show");
        var pidl = Native.ILCreateFromPathW(path);
        if (pidl == 0) throw new ActionException(Strings.ActionFailed("show"));
        try
        {
            var hr = Native.SHOpenFolderAndSelectItems(pidl, 0, null, 0);
            if (hr != 0) throw new ActionException($"File Explorer couldn't show that screenshot (error 0x{hr:X8}).");
        }
        finally { Native.ILFree(pidl); }
    }

    /// <summary>Opens the PNG "edit" app: Paint, unless the user picked another.</summary>
    public static void MarkUp(string path)
    {
        Require(path, "mark up");
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "edit" }); }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            throw new ActionException("Paint isn't available to mark up this screenshot. Install Paint from the Microsoft Store, or open the file in another editor.");
        }
    }

    public static void OpenFolder(string folder)
    {
        try { Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true }); }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            throw new ActionException($"File Explorer couldn't open {folder}. {e.Message}");
        }
    }

    public static byte[] Read(string path)
    {
        Require(path, "copy");
        try { return File.ReadAllBytes(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { throw new ActionException(Strings.ActionFailed("copy")); }
    }

    private static void Require(string path, string verb)
    {
        if (!RecentCaptures.IsRegularFile(path)) throw new ActionException(Strings.ActionFailed(verb));
    }
}
