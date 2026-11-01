using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Screenshot.Interop;

namespace Screenshot.Windows;

/// <summary>
/// The floating thumbnail (PRE-001 to PRE-006, DESIGN §5). Never takes keyboard focus.
/// Closes 8 s after it appears; hovering pauses it and leaving starts a fresh 8 s.
/// </summary>
internal sealed class PreviewWindow : Window
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(8);
    private const double CardWidth = 324, CardHeight = 248, ShadowMargin = 10, EdgeGap = 20;

    private readonly DispatcherTimer _timer;
    private string? _path;

    public event Action<string>? CopyRequested;
    public event Action<string>? RevealRequested;
    public event Action<string>? MarkUpRequested;

    private readonly Image _image;
    private readonly TextBlock _caption;

    public PreviewWindow()
    {
        Title = "Screenshot preview";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        Width = CardWidth + 2 * ShadowMargin;
        Height = CardHeight + 2 * ShadowMargin;
        UseLayoutRounding = true;

        _timer = new DispatcherTimer { Interval = Lifetime };
        _timer.Tick += (_, _) => Dismiss();

        _image = new Image { Stretch = Stretch.Uniform, Height = 150 };
        AutomationProperties("Captured screenshot", _image);
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);
        var imageClip = new Border { CornerRadius = new CornerRadius(6), Height = 150, ClipToBounds = true, Child = _image };

        _caption = new TextBlock
        {
            FontSize = 12, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 10, 0, 8),
        };
        _caption.SetResourceReference(TextBlock.ForegroundProperty, "ShotText");

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(Button("Copy", "Copy image", () => Act(CopyRequested)));
        buttons.Children.Add(Button("Reveal", "Show in File Explorer", () => Act(RevealRequested)));
        buttons.Children.Add(Button("Mark Up", "Mark Up in Paint", () => Act(MarkUpRequested)));

        var close = new Button
        {
            Content = "✕", Width = 18, Height = 18, Padding = new Thickness(0), FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -4, -4, 0), Focusable = false, ToolTip = "Dismiss preview",
        };
        AutomationProperties("Dismiss preview", close);
        close.Click += (_, _) => Dismiss();

        var stack = new StackPanel();
        stack.Children.Add(imageClip);
        stack.Children.Add(_caption);
        stack.Children.Add(buttons);

        var grid = new Grid { Margin = new Thickness(14) };
        grid.Children.Add(stack);
        grid.Children.Add(close);

        var card = new Border
        {
            Width = CardWidth, Height = CardHeight, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1),
            Margin = new Thickness(ShadowMargin), Child = grid,
            Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 2, Opacity = 0.25, Direction = 270 },
        };
        card.SetResourceReference(Border.BackgroundProperty, "ShotSurface");
        card.SetResourceReference(Border.BorderBrushProperty, "ShotStroke");
        Content = card;

        MouseEnter += (_, _) => _timer.Stop();
        MouseLeave += (_, _) => Restart(); // a fresh 8 s, not what was left

        SourceInitialized += (_, _) =>
        {
            Screens.MakeNonActivating(this);
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(NoActivateHook);
        };
    }

    public bool IsShowing => IsVisible && _path is not null;

    /// <summary>Shows the thumbnail on the given display's corner (PRE-003). Never activates.</summary>
    public void Present(BitmapSource thumbnail, string path, string caption, Display display)
    {
        _path = path;
        _image.Source = thumbnail;
        _caption.Text = caption;
        _caption.ToolTip = System.IO.Path.GetFileName(path);

        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        if (!IsVisible) Show();
        const uint flags = Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW;
        // First move onto that display so WPF applies its DPI (the size stays 340 × 268 DIPs)…
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, display.WorkArea.Left, display.WorkArea.Top, 0, 0, flags);
        // …then place it in the corner, in physical pixels, inside the usable area (above the taskbar).
        var w = (int)Math.Round(Width * display.Scale);
        var h = (int)Math.Round(Height * display.Scale);
        var gap = (int)Math.Round((EdgeGap - ShadowMargin) * display.Scale);
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, display.WorkArea.Right - w - gap, display.WorkArea.Bottom - h - gap, 0, 0, flags);
        Restart();
    }

    /// <summary>Closes now and lets go of the image and path (PRE-005).</summary>
    public void Dismiss()
    {
        _timer.Stop();
        _path = null;
        _image.Source = null;
        if (IsVisible) Hide();
    }

    private void Restart()
    {
        _timer.Stop();
        if (_path is not null) _timer.Start();
    }

    private void Act(Action<string>? action)
    {
        var path = _path;
        if (path is null) return;
        Dismiss();
        action?.Invoke(path);
    }

    private static Button Button(string text, string tooltip, Action onClick)
    {
        var b = new Button { Content = text, ToolTip = tooltip, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(10, 3, 10, 3), Focusable = false };
        AutomationProperties(text, b);
        b.Click += (_, _) => onClick();
        return b;
    }

    private static void AutomationProperties(string name, DependencyObject element) =>
        System.Windows.Automation.AutomationProperties.SetName(element, name);

    private static nint NoActivateHook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == Native.WM_MOUSEACTIVATE)
        {
            handled = true;
            return Native.MA_NOACTIVATE;
        }
        return 0;
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);
        _timer.Stop(); // the pointer is inside; MouseLeave restarts
    }
}
