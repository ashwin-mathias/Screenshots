using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Screenshot.Services;

namespace Screenshot.Windows;

/// <summary>First-run window (UI-002, DESIGN §7).</summary>
internal sealed class WelcomeWindow : Window
{
    private readonly TextBlock _status;
    private readonly StackPanel _keys;

    public event Action? CaptureRequested;
    public event Action? RecentRequested;
    public event Action? HelpRequested;

    public WelcomeWindow()
    {
        Title = "Welcome to Screenshotz";
        Width = 540;
        Height = 400;
        ResizeMode = ResizeMode.CanMinimize; // minimise, no maximise; fixed size (DESIGN §7)
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = AppAssets.Icon;

        var root = new StackPanel { Margin = new Thickness(28, 26, 28, 20) };
        root.Children.Add(new TextBlock { Text = "A screenshot. In two places.", FontSize = 26, FontWeight = FontWeights.SemiBold });
        root.Children.Add(Secondary("Select an area. It's saved as a PNG and copied to your clipboard, ready to paste.", top: 6));

        _keys = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 14) };
        root.Children.Add(_keys);

        foreach (var line in new[]
        {
            "Drag to select · Esc cancels",
            "Saved to Documents\\screenshots",
            "The image is on your clipboard",
            "Turn shortcuts on or off in the menu",
        })
            root.Children.Add(new TextBlock { Text = line, Margin = new Thickness(0, 2, 0, 2) });
        root.Children.Add(Secondary("Can't see the icon? Click ^ in the taskbar and drag Screenshotz next to the clock.", top: 10));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        buttons.Children.Add(MakeButton("Help & About", () => HelpRequested?.Invoke(), accent: false));
        buttons.Children.Add(MakeButton("Recent Screenshots", () => RecentRequested?.Invoke(), accent: false));
        buttons.Children.Add(MakeButton("Capture Area", () => CaptureRequested?.Invoke(), accent: true));
        root.Children.Add(buttons);

        _status = Secondary(Strings.Ready, top: 10);
        _status.HorizontalAlignment = HorizontalAlignment.Right;
        root.Children.Add(_status);

        Content = root;
    }

    public void SetStatus(string text) => _status.Text = text;

    /// <summary>Shows the active shortcuts as key caps, or a hint when both are off.</summary>
    public void SetShortcuts(bool altS, bool printScreen)
    {
        _keys.Children.Clear();
        var groups = new List<string[]>();
        if (altS) groups.Add(["Alt", "S"]);
        if (printScreen) groups.Add(["Print Screen"]);
        if (groups.Count == 0)
        {
            _keys.Children.Add(new TextBlock { Text = "Use Capture Area in the menu.", FontSize = 15 });
            return;
        }
        for (var g = 0; g < groups.Count; g++)
        {
            if (g > 0) _keys.Children.Add(new TextBlock { Text = "or", Margin = new Thickness(14, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center });
            for (var k = 0; k < groups[g].Length; k++)
            {
                if (k > 0) _keys.Children.Add(new TextBlock { Text = "+", FontSize = 20, Margin = new Thickness(6, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
                _keys.Children.Add(KeyCap(groups[g][k]));
            }
        }
    }

    private static Border KeyCap(string text)
    {
        var b = new Border
        {
            CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 4, 12, 4), BorderThickness = new Thickness(1),
            Child = new TextBlock { Text = text, FontSize = 20, FontWeight = FontWeights.SemiBold },
        };
        b.SetResourceReference(Border.BackgroundProperty, "ShotKeyCap");
        b.SetResourceReference(Border.BorderBrushProperty, "ShotStroke");
        return b;
    }

    private static TextBlock Secondary(string text, double top)
    {
        var t = new TextBlock { Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, top, 0, 0) };
        t.SetResourceReference(TextBlock.ForegroundProperty, "ShotTextSecondary");
        return t;
    }

    private static Button MakeButton(string text, Action onClick, bool accent)
    {
        var b = new Button { Content = text, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(14, 6, 14, 6), IsDefault = accent };
        if (accent) b.SetResourceReference(StyleProperty, "AccentButtonStyle");
        b.Click += (_, _) => onClick();
        return b;
    }
}
