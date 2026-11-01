using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Screenshot.Interop;
using Screenshot.Services;
using Screenshot.Support;
using Screenshot.Support.Windows;
using Screenshot.Windows;

namespace Screenshot;

/// <summary>Wires the tray, menu, shortcuts, capture, preview, recents and updates together.</summary>
internal sealed class AppController : IDisposable
{
    private static readonly TimeSpan CheckmarkTime = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SelectionDelay = TimeSpan.FromMilliseconds(250);

    private readonly App _app;
    private readonly Settings _settings = Settings.Load();
    private readonly MessageHost _host = new();
    private readonly TrayIcon _tray;
    private readonly HotkeyManager _hotkeys;
    private readonly CaptureStore _store;
    private readonly ImageCopier _copier;
    private readonly CapturePipeline _pipeline;
    private readonly PreviewWindow _preview = new();
    private readonly Updater _updater;
    private readonly DispatcherTimer _checkmark;

    private WelcomeWindow? _welcome;
    private HelpAboutWindow? _help;
    private SelectionOverlay? _overlay;
    private ContextMenu? _menu;
    private Window? _menuHost;
    private MenuItem? _recentMenu;
    private DateTime _menuClosedAt;
    private bool _menuOpening;

    private string _status = Strings.Ready;
    private bool _busy;
    private bool _quitting;
    private bool _updatePromptOpen;
    private string? _lastCapture;          // this session only (REC-004)
    private Display? _captureDisplay;      // where the last capture started (PRE-003)

    private IReadOnlyList<RecentCapture> _recent = [];
    private bool _recentLoaded;
    private bool _recentFailed;
    private Task? _recentScan;

    public static string ScreenshotsFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "screenshots");

    public AppController(App app)
    {
        _app = app;
        _tray = new TrayIcon(_host.Handle);
        _hotkeys = new HotkeyManager(_host.Handle);
        _store = new CaptureStore(ScreenshotsFolder, new WicImageDecoder());
        _copier = new ImageCopier(new Win32Clipboard(_host.Handle));
        _pipeline = new CapturePipeline(_store, _copier);
        _updater = new Updater(_settings);
        _checkmark = new DispatcherTimer { Interval = CheckmarkTime };
        _checkmark.Tick += (_, _) => { _checkmark.Stop(); _tray.SetIcon("viewfinder"); };
    }

    // ---- start and stop ----------------------------------------------------------------------

    public void Start(bool updatedRestart)
    {
        Theme.ApplyBrushes(_app.Resources);

        _host.Hotkey += _hotkeys.OnHotkey;
        _host.TrayEvent += OnTrayEvent;
        _host.TaskbarRestarted += _tray.Readd;
        _host.ThemeChanged += () => { Theme.ApplyBrushes(_app.Resources); _tray.Refresh(); };
        _host.ShowWelcomeRequested += ShowWelcome;
        _hotkeys.Pressed += id => _ = CaptureAsync();

        _preview.CopyRequested += CopyFile;
        _preview.RevealRequested += path => Run(() => FileActions.Reveal(path));
        _preview.MarkUpRequested += path => Run(() => FileActions.MarkUp(path));

        _tray.Show();
        SetStatus(Strings.Ready);

        try { _store.EnsureFolder(); }
        catch (CaptureStoreException e) { Log.Write(e.Message); } // reported again at capture time

        var problems = new List<string>();
        if (!_settings.AltSDisabled && _hotkeys.Register(HotkeyManager.AltS) is var a and not 0)
            problems.Add(Strings.ShortcutTaken("Alt+S", a));
        if (_settings.PrintScreenEnabled)
        {
            if (WindowsUsesPrintScreen()) problems.Add(Strings.PrintScreenTaken);
            else if (_hotkeys.Register(HotkeyManager.PrintScreen) is var p and not 0) problems.Add(Strings.ShortcutTaken("Print Screen", p));
        }
        if (problems.Count > 0)
        {
            SetStatus(Strings.NeedsAttention);
            _ = AlertWindow.Error(string.Join(Environment.NewLine + Environment.NewLine, problems));
        }

        _recentScan = RefreshRecentAsync();

        if (!_settings.HasLaunched)
        {
            _settings.HasLaunched = true;
            _settings.Save();
            ShowWelcome();
        }

        if (_settings.PendingUpdateVersion is { } pending)
        {
            if (pending == AppInfo.Version.ToString() || updatedRestart) SetStatus(Strings.UpdatedTo(AppInfo.Version.ToString()));
            _settings.PendingUpdateVersion = null;
            _settings.Save();
        }

        _updater.OfferFound += (offer, manual) => _ = PromptForUpdateAsync(offer, manual);
        if (AppInfo.IsPackaged && !AppInfo.IsStoreBuild) _updater.Start(); // the Store updates the Store build (UPD-007)
    }

    public void Dispose()
    {
        _quitting = true;
        _overlay?.Close();
        _preview.Dismiss();
        _preview.Close();
        _updater.Dispose();
        _hotkeys.Dispose();
        _tray.Dispose();
        _host.Dispose();
    }

    private void Quit()
    {
        _quitting = true;
        _overlay?.Close();                 // stops an active selection (CAP-004)
        _updater.CancelDownload();
        _app.Shutdown();
    }

    // ---- status ------------------------------------------------------------------------------

    private void SetStatus(string text)
    {
        _status = text;
        _tray.SetTooltip($"Screenshotz — {text}");
        _welcome?.SetStatus(text);
    }

    // ---- capture (SPEC §5) ---------------------------------------------------------------------

    public async Task CaptureAsync()
    {
        if (_busy || _quitting) return;   // CAP-003: one at a time
        _busy = true;
        _menu?.SetCurrentValue(ContextMenu.IsOpenProperty, false);
        var reopen = HideAppWindows();
        try
        {
            var display = Screens.AtPointer();
            _captureDisplay = display;
            _preview.Dismiss();
            SetStatus(Strings.Selecting);

            try { _store.EnsureFolder(); }
            catch (CaptureStoreException e) { await FailAsync(e.Message); return; }

            var previous = Native.GetForegroundWindow();
            await Task.Delay(SelectionDelay);   // menus close, shortcut keys are released
            if (_quitting) return;

            BitmapSource frame;
            try { frame = Screens.Grab(display); }
            catch (InvalidOperationException e) { await FailAsync($"{Strings.CaptureFailed} {e.Message}"); return; }

            _overlay = new SelectionOverlay(frame, display);
            Int32Rect? selection;
            try { selection = await _overlay.SelectAsync(); }
            finally { _overlay = null; }
            var menuHost = _menuHost is null ? 0 : new System.Windows.Interop.WindowInteropHelper(_menuHost).Handle;
            if (previous != 0 && previous != menuHost && Native.IsWindow(previous)) Native.SetForegroundWindow(previous);
            if (_quitting) return;
            if (selection is not { } rect)
            {
                SetStatus(Strings.Cancelled);
                return;
            }

            byte[] png;
            try { png = PngEncoding.Encode(new CroppedBitmap(frame, rect), display.Scale); }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or IOException or System.Runtime.InteropServices.COMException)
            {
                await FailAsync($"{Strings.CaptureFailed} {e.Message}");
                return;
            }

            var result = _pipeline.Process(png);
            switch (result.Kind)
            {
                case CaptureResultKind.SavedAndCopied:
                    _lastCapture = result.SavedPath;
                    SetStatus(Strings.SavedAndCopied);
                    FlashCheckmark();
                    if (!_settings.CapturePreviewDisabled) ShowPreview(png, result.SavedPath!, Strings.CaptionSavedAndCopied);
                    break;
                case CaptureResultKind.SavedNotCopied:
                    _lastCapture = result.SavedPath;
                    SetStatus(Strings.NeedsAttention);
                    if (!_settings.CapturePreviewDisabled) ShowPreview(png, result.SavedPath!, Strings.CaptionCopyRetry);
                    _ = AlertWindow.Error(Strings.SavedNotCopied(result.Error?.Message ?? ""));
                    break;
                default:
                    await FailAsync(result.Error?.Message ?? Strings.CaptureFailed);
                    break;
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Write($"Capture failed: {e}");
            await FailAsync($"{Strings.CaptureFailed} {e.Message}");
        }
        finally
        {
            _busy = false;
            foreach (var w in reopen) if (!_quitting) { w.ShowActivated = false; w.Show(); w.ShowActivated = true; }
        }
    }

    private Task FailAsync(string message)
    {
        SetStatus(Strings.NeedsAttention);
        return AlertWindow.Error(message);
    }

    /// <summary>Hides Welcome and Help so they're not in the capture (CAP-001). Returns the ones to show again.</summary>
    private List<Window> HideAppWindows()
    {
        var hidden = new List<Window>();
        foreach (Window w in new Window?[] { _welcome, _help }.OfType<Window>())
        {
            if (!w.IsVisible) continue;
            w.Hide();
            hidden.Add(w);
        }
        return hidden;
    }

    private void FlashCheckmark()
    {
        _tray.SetIcon("check");
        _checkmark.Stop();
        _checkmark.Start();
    }

    private void ShowPreview(byte[] image, string path, string caption)
    {
        var thumb = Thumbnailer.Load(image);
        if (thumb is null)
        {
            SetStatus(Strings.PreviewUnavailable);
            return;
        }
        _preview.Present(thumb, path, caption, _captureDisplay ?? Screens.Primary());
    }

    private void ShowPreviewOfFile(string path, string caption)
    {
        if (!RecentCaptures.IsRegularFile(path)) { _ = AlertWindow.Error(Strings.ActionFailed("show")); return; }
        var thumb = Thumbnailer.LoadFile(path);
        if (thumb is null) { SetStatus(Strings.PreviewUnavailable); return; }
        _preview.Present(thumb, path, caption, _captureDisplay ?? Screens.Primary());
    }

    // ---- file actions (ACT-002) ------------------------------------------------------------------

    private void CopyFile(string path)
    {
        try
        {
            _copier.Copy(FileActions.Read(path));
            SetStatus(Strings.CopiedRecent);
        }
        catch (Exception e) when (e is FileActions.ActionException or InvalidImageException or ClipboardException)
        {
            SetStatus(Strings.NeedsAttention);
            _ = AlertWindow.Error(e is InvalidImageException ? "That file isn't an image Screenshotz can copy." : e.Message);
        }
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (FileActions.ActionException e)
        {
            SetStatus(Strings.NeedsAttention);
            _ = AlertWindow.Error(e.Message);
        }
    }

    // ---- recents (REC-001) ---------------------------------------------------------------------

    private Task RefreshRecentAsync()
    {
        if (_recentScan is { IsCompleted: false }) return _recentScan; // one refresh at a time
        var folder = ScreenshotsFolder;
        _recentScan = Task.Run(() => RecentCaptures.Scan(folder)).ContinueWith(t =>
        {
            _recentLoaded = true;
            _recentFailed = t.IsFaulted;
            if (!t.IsFaulted) _recent = t.Result;
            else Log.Write($"Recent scan failed: {t.Exception?.GetBaseException().Message}");
            if (_recentMenu is not null) FillRecentMenu(_recentMenu);
        }, TaskScheduler.FromCurrentSynchronizationContext());
        return _recentScan;
    }

    private void FillRecentMenu(MenuItem parent)
    {
        parent.Items.Clear();
        if (!_recentLoaded && _recent.Count == 0)
        {
            parent.Items.Add(Disabled(Strings.Loading));
            return;
        }
        if (_recentFailed) parent.Items.Add(Disabled(Strings.FolderUnreadable));
        else if (_recent.Count == 0) parent.Items.Add(Disabled(Strings.NoScreenshots));
        foreach (var r in _recent)
        {
            var item = new MenuItem
            {
                Header = new TextBlock { Text = r.DisplayName },
                ToolTip = $"{r.FileName}{Environment.NewLine}{r.ModifiedUtc.ToLocalTime():f}",
            };
            var path = r.Path;
            item.Items.Add(Item(Strings.Open, () => Run(() => FileActions.Open(path))));
            item.Items.Add(Item(Strings.CopyImage, () => CopyFile(path)));
            item.Items.Add(Item(Strings.RevealInExplorer, () => Run(() => FileActions.Reveal(path))));
            item.Items.Add(Item(Strings.ShowPreview, () => ShowPreviewOfFile(path, Strings.CaptionRecent)));
            item.Items.Add(Item(Strings.MarkUp, () => Run(() => FileActions.MarkUp(path))));
            parent.Items.Add(item);
        }
        parent.Items.Add(new Separator());
        parent.Items.Add(Item(Strings.OpenFolder, OpenFolder));
    }

    private void OpenFolder()
    {
        try
        {
            _store.EnsureFolder();
            FileActions.OpenFolder(ScreenshotsFolder);
        }
        catch (Exception e) when (e is CaptureStoreException or FileActions.ActionException)
        {
            SetStatus(Strings.NeedsAttention);
            _ = AlertWindow.Error(e.Message);
        }
    }

    private async Task ShowLastOnDiskAsync()
    {
        try
        {
            var newest = await Task.Run(() => RecentCaptures.Scan(ScreenshotsFolder, 1));
            if (newest.Count == 0) { await AlertWindow.Info(Strings.NoScreenshots, "Capture an area first, then try again."); return; }
            ShowPreviewOfFile(newest[0].Path, Strings.CaptionRecent);
        }
        catch (RecentCapturesException e)
        {
            SetStatus(Strings.NeedsAttention);
            await AlertWindow.Error($"{Strings.FolderUnreadable}. {e.InnerException?.Message}");
        }
    }

    // ---- tray menu (UI-001) --------------------------------------------------------------------

    private void OnTrayEvent(int ev)
    {
        if (ev is Native.WM_LBUTTONUP or Native.WM_CONTEXTMENU or Native.NIN_KEYSELECT or Native.NIN_SELECT)
            _ = ShowMenuAsync();
    }

    private async Task ShowMenuAsync()
    {
        if (_menuOpening || _menu is { IsOpen: true }) return; // a click can arrive as two tray events
        if (DateTime.UtcNow - _menuClosedAt < TimeSpan.FromMilliseconds(300)) return; // the click that closed it
        _menuOpening = true;
        LoginItem.State login;
        try
        {
            _ = RefreshRecentAsync();
            login = await LoginItem.GetStateAsync();
        }
        finally { _menuOpening = false; }

        _menu = BuildMenu(login);
        _menuHost ??= new Window
        {
            WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = System.Windows.Media.Brushes.Transparent,
            ShowInTaskbar = false, Width = 1, Height = 1, Left = -32000, Top = -32000, Topmost = true, Title = "Screenshotz menu",
        };
        _menuHost.Show();
        _menuHost.Activate();
        Native.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(_menuHost).Handle); // so a click elsewhere closes it
        _menu.PlacementTarget = _menuHost;
        _menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        _menu.Closed += (_, _) =>
        {
            _menuClosedAt = DateTime.UtcNow;
            _recentMenu = null;
            _menuHost?.Hide();
        };
        _menu.IsOpen = true;
    }

    private ContextMenu BuildMenu(LoginItem.State login)
    {
        var m = new ContextMenu();
        m.Items.Add(Disabled(_status));
        if (AppInfo.IsStoreBuild) { }
        else if (_updater.IsDownloading) m.Items.Add(Item(Strings.CancelUpdate, _updater.CancelDownload));
        else if (_updater.Pending is { } offer) m.Items.Add(Item(Strings.InstallUpdate(offer.Version.ToString()), () => _ = PromptForUpdateAsync(offer, manual: true)));
        m.Items.Add(new Separator());

        var capture = Item(CaptureLabel(), () => _ = CaptureAsync());
        capture.IsEnabled = !_busy;
        m.Items.Add(capture);
        m.Items.Add(Item(Strings.OpenFolder, OpenFolder));
        var reveal = Item(Strings.RevealLast, () => { if (_lastCapture is { } p) Run(() => FileActions.Reveal(p)); });
        reveal.IsEnabled = _lastCapture is not null;
        m.Items.Add(reveal);
        _recentMenu = new MenuItem { Header = Strings.Recent };
        FillRecentMenu(_recentMenu);
        m.Items.Add(_recentMenu);
        m.Items.Add(Item(Strings.ShowLastPreview, () => _ = ShowLastOnDiskAsync()));
        m.Items.Add(new Separator());

        m.Items.Add(Check(Strings.ShowThumbnail, !_settings.CapturePreviewDisabled, () =>
        {
            _settings.CapturePreviewDisabled = !_settings.CapturePreviewDisabled;
            _settings.Save();
            if (_settings.CapturePreviewDisabled) _preview.Dismiss();
        }));
        m.Items.Add(Check(Strings.EnableAltS, _hotkeys.IsRegistered(HotkeyManager.AltS), ToggleAltS));
        m.Items.Add(Check(Strings.EnablePrintScreen, _hotkeys.IsRegistered(HotkeyManager.PrintScreen), () => _ = TogglePrintScreenAsync()));
        m.Items.Add(LoginMenuItem(login));
        if (!AppInfo.IsStoreBuild)
        {
            m.Items.Add(Check(Strings.AutoUpdate, !_settings.AutoUpdateCheckDisabled, () =>
            {
                _settings.AutoUpdateCheckDisabled = !_settings.AutoUpdateCheckDisabled;
                _settings.Save();
            }));
        }
        m.Items.Add(new Separator());

        m.Items.Add(Item(Strings.CheckForUpdates, () => _ = ManualUpdateCheckAsync()));
        m.Items.Add(Item(Strings.HowToUse, () => ShowHelp(1)));
        m.Items.Add(Item(Strings.HelpAbout, () => ShowHelp(0)));
        m.Items.Add(new Separator());
        m.Items.Add(Item(Strings.Quit, Quit));
        return m;
    }

    private string CaptureLabel()
    {
        var keys = new List<string>();
        if (_hotkeys.IsRegistered(HotkeyManager.AltS)) keys.Add("Alt+S");
        if (_hotkeys.IsRegistered(HotkeyManager.PrintScreen)) keys.Add("Print Screen");
        return keys.Count == 0 ? Strings.CaptureArea : $"{Strings.CaptureArea} ({string.Join(" or ", keys)})";
    }

    private MenuItem LoginMenuItem(LoginItem.State state)
    {
        switch (state)
        {
            case LoginItem.State.Enabled:
                return Check(Strings.LaunchAtLogin, true, () => _ = ToggleLoginAsync());
            case LoginItem.State.DisabledByUser:
                return Item(Strings.LaunchAtLoginSettings, () => HelpAboutWindow.OpenUrl("ms-settings:startupapps"));
            case LoginItem.State.Disabled:
                return Check(Strings.LaunchAtLogin, false, () => _ = ToggleLoginAsync());
            default:
                var item = Disabled(Strings.LaunchAtLogin);
                item.ToolTip = state == LoginItem.State.Unavailable ? "Available in the installed app." : "Turned off by your organisation.";
                return item;
        }
    }

    private async Task ToggleLoginAsync()
    {
        try { await LoginItem.ToggleAsync(); }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException or ArgumentException)
        {
            SetStatus(Strings.NeedsAttention);
            await AlertWindow.Error($"Launch at Login couldn't be changed. {e.Message}");
        }
    }

    // ---- shortcuts (KEY-002) ---------------------------------------------------------------------

    private void ToggleAltS()
    {
        if (_hotkeys.IsRegistered(HotkeyManager.AltS))
        {
            _hotkeys.Unregister(HotkeyManager.AltS);
            _settings.AltSDisabled = true;
        }
        else
        {
            _settings.AltSDisabled = false;
            if (_hotkeys.Register(HotkeyManager.AltS) is var err and not 0)
            {
                SetStatus(Strings.NeedsAttention);
                _ = AlertWindow.Error(Strings.ShortcutTaken("Alt+S", err));
            }
        }
        _settings.Save();
        _welcome?.SetShortcuts(_hotkeys.IsRegistered(HotkeyManager.AltS), _hotkeys.IsRegistered(HotkeyManager.PrintScreen));
    }

    private async Task TogglePrintScreenAsync()
    {
        if (_hotkeys.IsRegistered(HotkeyManager.PrintScreen))
        {
            _hotkeys.Unregister(HotkeyManager.PrintScreen);
            _settings.PrintScreenEnabled = false;
            _settings.Save();
        }
        else if (WindowsUsesPrintScreen())
        {
            var choice = await new AlertWindow("Print Screen is in use", Strings.PrintScreenTaken, ["Open Settings", "Cancel"]).ShowAsync();
            if (choice == 0) HelpAboutWindow.OpenUrl("ms-settings:easeofaccess-keyboard");
        }
        else
        {
            _settings.PrintScreenEnabled = true;
            _settings.Save();
            if (_hotkeys.Register(HotkeyManager.PrintScreen) is var err and not 0)
            {
                SetStatus(Strings.NeedsAttention);
                await AlertWindow.Error(Strings.ShortcutTaken("Print Screen", err));
            }
        }
        _welcome?.SetShortcuts(_hotkeys.IsRegistered(HotkeyManager.AltS), _hotkeys.IsRegistered(HotkeyManager.PrintScreen));
    }

    /// <summary>Is Windows' own "Print screen opens screen capture" setting on? (KEY-002)</summary>
    private static bool WindowsUsesPrintScreen()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Keyboard");
            return k?.GetValue("PrintScreenKeyForSnippingEnabled") is int v && v == 1;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    // ---- windows ---------------------------------------------------------------------------------

    private void ShowWelcome()
    {
        if (_welcome is null)
        {
            _welcome = new WelcomeWindow();
            _welcome.CaptureRequested += () => _ = CaptureAsync();
            _welcome.RecentRequested += () => _ = ShowMenuAsync();
            _welcome.HelpRequested += () => ShowHelp(0);
            _welcome.Closed += (_, _) => _welcome = null;
        }
        _welcome.SetShortcuts(_hotkeys.IsRegistered(HotkeyManager.AltS), _hotkeys.IsRegistered(HotkeyManager.PrintScreen));
        _welcome.SetStatus(_status);
        _welcome.Show();
        _welcome.Activate();
    }

    private void ShowHelp(int tab)
    {
        if (_help is null)
        {
            _help = new HelpAboutWindow();
            _help.CheckForUpdatesRequested += () => _ = ManualUpdateCheckAsync();
            _help.Closed += (_, _) => _help = null;
        }
        _help.SelectTab(tab);
        _help.Show();
        _help.Activate();
    }

    // ---- updates (SPEC §13) ----------------------------------------------------------------------

    private async Task ManualUpdateCheckAsync()
    {
        if (AppInfo.IsStoreBuild)
        {
            HelpAboutWindow.OpenUrl(AppInfo.StorePage); // the Store shows Update when there is one (UPD-007)
            return;
        }
        var before = _status;
        SetStatus(Strings.CheckingUpdates);
        try
        {
            var result = await _updater.CheckAsync(manual: true);
            SetStatus(before == Strings.CheckingUpdates ? Strings.Ready : before);
            if (result.Offer is not null) return; // the prompt is shown by OfferFound
            if (result.Reason == UpdateIgnoreReason.UpToDate)
                await AlertWindow.Info("You're up to date", $"{Strings.UpToDatePrefix}{AppInfo.Version}{Strings.UpToDateSuffix}");
            else
                await AlertWindow.Info(Strings.UpdateCheckFailed, $"{Strings.UpdateCheckFailed} The latest release isn't available for Windows yet ({result.Reason}).");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
        {
            SetStatus(before == Strings.CheckingUpdates ? Strings.Ready : before);
            await AlertWindow.Info(Strings.UpdateCheckFailed, $"{Strings.UpdateCheckFailed} {e.Message}");
        }
    }

    private async Task PromptForUpdateAsync(UpdateOffer offer, bool manual)
    {
        if (AppInfo.IsStoreBuild) return;
        if (_updatePromptOpen || _updater.IsDownloading) return;
        _updatePromptOpen = true;
        int choice;
        try
        {
            choice = await new AlertWindow(
                $"Screenshotz {offer.Version} is available",
                $"You have {AppInfo.Version}. Would you like to install it now?",
                ["Install and Relaunch", "Later", "Skip This Version"],
                notes: string.IsNullOrWhiteSpace(offer.Notes) ? null : offer.Notes,
                cancelIndex: 1).ShowAsync();
        }
        finally { _updatePromptOpen = false; }

        if (choice == 2) { _updater.Skip(offer); return; }
        if (choice != 0) return; // Later: the menu item stays

        while (_busy) await Task.Delay(500);   // never during a capture (UPD-003)
        var before = _status;
        try
        {
            SetStatus(Strings.Downloading(0));
            await _updater.InstallAsync(offer, new Progress<int>(p => SetStatus(p >= 100 ? Strings.Installing : Strings.Downloading(p))));
        }
        catch (UpdateException e)
        {
            if (e.Cancelled) { SetStatus(before); return; }
            SetStatus(Strings.NeedsAttention);
            Log.Write($"Update failed: {e.Message}");
            var buttons = e.OfferDownloadPage ? new[] { "Open Download Page", "OK" } : new[] { "OK" };
            var c = await new AlertWindow(Strings.NeedsAttention, e.Message, buttons, warning: true).ShowAsync();
            if (e.OfferDownloadPage && c == 0) HelpAboutWindow.OpenUrl(UpdateFeed.ReleasesPage);
        }
    }

    // ---- menu helpers ------------------------------------------------------------------------------

    private static string Escape(string text) => text.Replace("_", "__");

    private static MenuItem Item(string text, Action onClick)
    {
        var item = new MenuItem { Header = Escape(text) };
        item.Click += (_, e) => { e.Handled = true; onClick(); };
        return item;
    }

    private static MenuItem Check(string text, bool on, Action onClick)
    {
        var item = Item(text, onClick);
        item.IsChecked = on;
        return item;
    }

    private static MenuItem Disabled(string text) => new() { Header = Escape(text), IsEnabled = false };
}
