using System.Runtime.InteropServices;

namespace Screenshot.Interop;

/// <summary>System hotkeys (KEY-001 to KEY-003). Registers keys only; never watches ordinary typing.</summary>
internal sealed class HotkeyManager(nint messageWindow) : IDisposable
{
    public const int AltS = 1;
    public const int PrintScreen = 2;

    private readonly HashSet<int> _registered = [];

    public event Action<int>? Pressed;

    public bool IsRegistered(int id) => _registered.Contains(id);

    /// <summary>Registers one key. Returns 0 on success, otherwise the Windows error code.</summary>
    public int Register(int id)
    {
        if (_registered.Contains(id)) return 0; // already on: harmless
        var (mods, vk) = id switch
        {
            AltS => (Native.MOD_ALT | Native.MOD_NOREPEAT, Native.VK_S),
            PrintScreen => (Native.MOD_NOREPEAT, Native.VK_SNAPSHOT),
            _ => throw new ArgumentOutOfRangeException(nameof(id)),
        };
        if (Native.RegisterHotKey(messageWindow, id, mods, vk))
        {
            _registered.Add(id);
            return 0;
        }
        var err = Marshal.GetLastWin32Error();
        return err == 0 ? -1 : err;
    }

    public void Unregister(int id)
    {
        if (_registered.Remove(id)) Native.UnregisterHotKey(messageWindow, id);
    }

    /// <summary>Called for WM_HOTKEY. Ignores ids that aren't registered, including queued ones after turning a key off.</summary>
    public void OnHotkey(int id)
    {
        if (_registered.Contains(id)) Pressed?.Invoke(id);
    }

    public void Dispose()
    {
        foreach (var id in _registered.ToList()) Unregister(id);
    }

    public static string Name(int id) => id == AltS ? "Alt+S" : "Print Screen";
}
