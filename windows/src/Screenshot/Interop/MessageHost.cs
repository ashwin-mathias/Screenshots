using System.Windows.Interop;

namespace Screenshot.Interop;

/// <summary>
/// A hidden, never-shown top-level window. Receives hotkeys, tray clicks, theme changes and the
/// "show Welcome" signal from a second launch (LIFE-001). Not message-only: those windows miss
/// broadcasts such as WM_SETTINGCHANGE and TaskbarCreated.
/// </summary>
internal sealed class MessageHost : IDisposable
{
    public const string WindowName = "AshwinMathias.Screenshot.Host";
    public static readonly uint ShowWelcomeMessage = Native.RegisterWindowMessageW("AshwinMathias.Screenshot.ShowWelcome");
    private static readonly uint TaskbarCreated = Native.RegisterWindowMessageW("TaskbarCreated");

    private readonly HwndSource _source;

    public nint Handle => _source.Handle;

    public event Action<int>? Hotkey;
    public event Action<int>? TrayEvent;
    public event Action? TaskbarRestarted;
    public event Action? ThemeChanged;
    public event Action? ShowWelcomeRequested;

    public MessageHost()
    {
        var p = new HwndSourceParameters(WindowName)
        {
            ParentWindow = 0,
            WindowStyle = unchecked((int)0x80000000),          // WS_POPUP, never made visible
            ExtendedWindowStyle = (int)Native.WS_EX_TOOLWINDOW, // never in Alt-Tab
            Width = 0,
            Height = 0,
        };
        _source = new HwndSource(p);
        _source.AddHook(WndProc);
    }

    /// <summary>Asks a copy that's already running to show Welcome. True if one was found.</summary>
    public static bool SignalRunningCopy()
    {
        var h = Native.FindWindowExW(0, 0, null, WindowName);
        return h != 0 && Native.PostMessageW(h, ShowWelcomeMessage, 0, 0);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        switch (msg)
        {
            case Native.WM_HOTKEY:
                Hotkey?.Invoke((int)wParam);
                handled = true;
                break;
            case TrayIcon.CallbackMessage:
                TrayEvent?.Invoke((int)(lParam & 0xFFFF));
                handled = true;
                break;
            case Native.WM_SETTINGCHANGE:
                ThemeChanged?.Invoke();
                break;
            default:
                if (msg == TaskbarCreated) TaskbarRestarted?.Invoke();
                else if (msg == ShowWelcomeMessage) { ShowWelcomeRequested?.Invoke(); handled = true; }
                break;
        }
        return 0;
    }

    public void Dispose() => _source.Dispose();
}
