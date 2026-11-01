using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Screenshot.Interop;

/// <summary>One display, in physical pixels (per-monitor DPI v2).</summary>
internal sealed record Display(nint Handle, Native.RECT Bounds, Native.RECT WorkArea, double Scale);

internal static class Screens
{
    /// <summary>The display that contains the pointer (CAP-001).</summary>
    public static Display AtPointer()
    {
        Native.GetCursorPos(out var p);
        return FromMonitor(Native.MonitorFromPoint(p, Native.MONITOR_DEFAULTTONEAREST));
    }

    public static Display Primary() => FromMonitor(Native.MonitorFromPoint(default, Native.MONITOR_DEFAULTTOPRIMARY));

    public static Display FromMonitor(nint monitor)
    {
        var info = new Native.MONITORINFOEX { cbSize = Marshal.SizeOf<Native.MONITORINFOEX>(), szDevice = "" };
        Native.GetMonitorInfoW(monitor, ref info);
        var scale = Native.GetDpiForMonitor(monitor, 0, out var dpiX, out _) == 0 ? dpiX / 96.0 : 1.0;
        return new Display(monitor, info.rcMonitor, info.rcWork, scale);
    }

    /// <summary>Copies a display's pixels into memory: the frozen frame for the overlay (CAP-002).</summary>
    public static BitmapSource Grab(Display d)
    {
        var w = d.Bounds.Width;
        var h = d.Bounds.Height;
        var screen = Native.GetDC(0);
        if (screen == 0) throw new InvalidOperationException("Windows didn't give access to the screen.");
        var mem = Native.CreateCompatibleDC(screen);
        var bmp = Native.CreateCompatibleBitmap(screen, w, h);
        var old = Native.SelectObject(mem, bmp);
        try
        {
            if (bmp == 0 || !Native.BitBlt(mem, 0, 0, w, h, screen, d.Bounds.Left, d.Bounds.Top, Native.SRCCOPY | Native.CAPTUREBLT))
                throw new InvalidOperationException($"Windows couldn't copy the screen (error {Marshal.GetLastWin32Error()}).");
            Native.SelectObject(mem, old);
            old = 0;
            var source = Imaging.CreateBitmapSourceFromHBitmap(bmp, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            if (old != 0) Native.SelectObject(mem, old);
            if (bmp != 0) Native.DeleteObject(bmp);
            Native.DeleteDC(mem);
            Native.ReleaseDC(0, screen);
        }
    }

    /// <summary>Makes a window float without ever taking focus (PRE-001).</summary>
    public static void MakeNonActivating(Window w)
    {
        var h = new WindowInteropHelper(w).EnsureHandle();
        var ex = (long)Native.GetWindowLongPtr(h, Native.GWL_EXSTYLE);
        Native.SetWindowLongPtr(h, Native.GWL_EXSTYLE, (nint)(ex | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW | Native.WS_EX_TOPMOST));
    }
}
