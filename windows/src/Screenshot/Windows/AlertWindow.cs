using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Screenshot.Windows;

/// <summary>
/// The app's message box: title, message, optional scrolling notes and named buttons (ERR-001, UPD-003).
/// Not modal, so shortcuts keep working; the result arrives as a task.
/// </summary>
internal sealed class AlertWindow : Window
{
    private readonly TaskCompletionSource<int> _choice = new();

    /// <param name="buttons">Labels; the first one is the default.</param>
    /// <param name="cancelIndex">What Esc or closing the window returns (default: the last button).</param>
    public AlertWindow(string title, string message, IReadOnlyList<string> buttons, string? notes = null, bool warning = false, int? cancelIndex = null)
    {
        var cancel = cancelIndex ?? buttons.Count - 1;
        Title = "Screenshot";
        Width = notes is null ? 460 : 480;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        ShowInTaskbar = true;
        Icon = AppAssets.Icon;

        var icon = new Image { Source = AppAssets.Icon, Width = 40, Height = 40, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 16, 0) };

        var text = new StackPanel();
        var heading = new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        if (warning) heading.Text = "⚠ " + title;
        text.Children.Add(heading);
        text.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
        if (!string.IsNullOrWhiteSpace(notes))
        {
            text.Children.Add(new TextBox
            {
                Text = notes, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 160, Margin = new Thickness(0, 12, 0, 0),
            });
        }

        var row = new DockPanel { Margin = new Thickness(24, 20, 24, 12) };
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        row.Children.Add(text);

        var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(24, 8, 24, 20) };
        for (var i = 0; i < buttons.Count; i++)
        {
            var index = i;
            var b = new Button
            {
                Content = buttons[i], MinWidth = 96, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(12, 5, 12, 5),
                IsDefault = i == 0, IsCancel = i == cancel,
            };
            if (i == 0) b.SetResourceReference(StyleProperty, "AccentButtonStyle");
            b.Click += (_, _) => { _choice.TrySetResult(index); Close(); };
            bar.Children.Add(b);
        }

        var root = new StackPanel();
        root.Children.Add(row);
        root.Children.Add(bar);
        Content = root;
        Closed += (_, _) => _choice.TrySetResult(cancel);
    }

    public Task<int> ShowAsync()
    {
        Show();
        Activate();
        return _choice.Task;
    }

    /// <summary>An error with one OK button (ERR-001).</summary>
    public static Task Error(string message) =>
        new AlertWindow(Services.Strings.NeedsAttention, message, ["OK"], warning: true).ShowAsync();

    public static Task Info(string title, string message) =>
        new AlertWindow(title, message, ["OK"]).ShowAsync();
}

/// <summary>Images bundled with the app.</summary>
internal static class AppAssets
{
    public static readonly ImageSource Icon = Load("Assets/AppIcon.png");
    public static readonly ImageSource Avatar = Load("Assets/avatar.png");
    public static readonly ImageSource Sparky = Load("Assets/sparky.jpg");

    private static ImageSource Load(string path)
    {
        var b = new BitmapImage();
        b.BeginInit();
        b.UriSource = new Uri($"pack://application:,,,/Screenshot;component/{path}", UriKind.Absolute);
        b.CacheOption = BitmapCacheOption.OnLoad;
        b.EndInit();
        b.Freeze();
        return b;
    }
}
