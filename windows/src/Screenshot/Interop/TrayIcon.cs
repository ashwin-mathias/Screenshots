using System.IO;
using System.Runtime.InteropServices;

namespace Screenshot.Interop;

/// <summary>The notification-area icon (Shell_NotifyIcon). Survives Explorer restarts.</summary>
internal sealed class TrayIcon : IDisposable
{
    public const int CallbackMessage = Native.WM_APP + 1;
    private const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIM_SETVERSION = 4;
    private const uint NIF_MESSAGE = 0x1, NIF_ICON = 0x2, NIF_TIP = 0x4, NIF_SHOWTIP = 0x80;
    private const uint NOTIFYICON_VERSION_4 = 4;
    private const uint IconId = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public int cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(uint message, ref NOTIFYICONDATAW data);

    private readonly nint _hwnd;
    private readonly Dictionary<string, nint> _icons = [];
    private string _iconName = "viewfinder";
    private string _tip = "Screenshot";
    private bool _added;

    public TrayIcon(nint messageWindow) => _hwnd = messageWindow;

    public void Show()
    {
        var data = Data(NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP);
        _added = Shell_NotifyIconW(NIM_ADD, ref data);
        data.uVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIconW(NIM_SETVERSION, ref data);
    }

    /// <summary>Explorer restarted: the icon must be added again.</summary>
    public void Readd()
    {
        _added = false;
        Show();
    }

    /// <summary>"viewfinder" or "check"; the light/dark variant follows the taskbar theme.</summary>
    public void SetIcon(string name)
    {
        _iconName = name;
        Update();
    }

    public void SetTooltip(string text)
    {
        _tip = text.Length > 127 ? text[..126] + "…" : text;
        Update();
    }

    /// <summary>The taskbar theme changed.</summary>
    public void Refresh()
    {
        foreach (var h in _icons.Values) Native.DestroyIcon(h);
        _icons.Clear();
        Update();
    }

    private void Update()
    {
        if (!_added) return;
        var data = Data(NIF_ICON | NIF_TIP | NIF_SHOWTIP);
        Shell_NotifyIconW(NIM_MODIFY, ref data);
    }

    private NOTIFYICONDATAW Data(uint flags) => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATAW>(),
        hWnd = _hwnd,
        uID = IconId,
        uFlags = flags,
        uCallbackMessage = CallbackMessage,
        hIcon = Icon(_iconName),
        szTip = _tip,
        szInfo = "",
        szInfoTitle = "",
    };

    private nint Icon(string name)
    {
        var variant = Theme.TaskbarIsLight ? "onLight" : "onDark";
        var key = $"tray-{name}-{variant}";
        if (_icons.TryGetValue(key, out var h)) return h;
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "tray", key + ".ico");
        var size = Native.GetSystemMetrics(Native.SM_CXSMICON);
        h = Native.LoadImageW(0, path, Native.IMAGE_ICON, size, size, Native.LR_LOADFROMFILE);
        _icons[key] = h;
        return h;
    }

    public void Dispose()
    {
        if (_added)
        {
            var data = Data(0);
            Shell_NotifyIconW(NIM_DELETE, ref data);
            _added = false;
        }
        foreach (var h in _icons.Values) Native.DestroyIcon(h);
        _icons.Clear();
    }
}
