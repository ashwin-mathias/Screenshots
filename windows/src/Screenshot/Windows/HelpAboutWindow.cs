using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Screenshot.Services;

namespace Screenshot.Windows;

/// <summary>Help &amp; About (HELP-001, HELP-002, DESIGN §8).</summary>
internal sealed class HelpAboutWindow : Window
{
    private readonly TabControl _tabs;
    public event Action? CheckForUpdatesRequested;

    public HelpAboutWindow()
    {
        Title = "Help & About Screenshot";
        Width = 620;
        Height = 540;
        MinWidth = 520;
        MinHeight = 430;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = AppAssets.Icon;

        _tabs = new TabControl { Margin = new Thickness(12) };
        _tabs.Items.Add(Tab("About", About()));
        _tabs.Items.Add(Tab("How to Use", HowToUse()));
        _tabs.Items.Add(Tab("Release History", ReleaseHistory()));
        Content = _tabs;
    }

    public void SelectTab(int index) => _tabs.SelectedIndex = index;

    private static TabItem Tab(string header, UIElement content)
    {
        var item = new TabItem
        {
            Header = header,
            Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = content },
        };
        System.Windows.Automation.AutomationProperties.SetName(item, header);
        return item;
    }

    // ---- About ---------------------------------------------------------------------------------

    private UIElement About()
    {
        var about = AboutInfo.Load();
        var panel = new StackPanel { Margin = new Thickness(24) };

        var app = Row(Circle(AppAssets.Icon, 64, round: false),
            Text("Screenshot", 20, FontWeights.SemiBold),
            Secondary($"Version {AppInfo.VersionWithBuild}"));
        panel.Children.Add(app);

        var author = Row(Circle(AppAssets.Avatar, 64, round: true),
            Text(about.Name, 17, FontWeights.SemiBold),
            Secondary(about.Role));
        author.Margin = new Thickness(0, 20, 0, 0);
        panel.Children.Add(author);

        var note = Selectable(about.SparkyNote, 13);
        var cardRow = new DockPanel();
        var photo = Circle(AppAssets.Sparky, 72, round: true);
        photo.Margin = new Thickness(0, 0, 16, 0);
        DockPanel.SetDock(photo, Dock.Left);
        cardRow.Children.Add(photo);
        note.VerticalAlignment = VerticalAlignment.Center;
        cardRow.Children.Add(note);
        var card = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Margin = new Thickness(0, 20, 0, 0), BorderThickness = new Thickness(1), Child = cardRow };
        card.SetResourceReference(Border.BackgroundProperty, "ShotCard");
        card.SetResourceReference(Border.BorderBrushProperty, "ShotStroke");
        panel.Children.Add(card);

        var privacy = Secondary(Strings.Privacy);
        privacy.Margin = new Thickness(0, 20, 0, 0);
        panel.Children.Add(privacy);

        var check = new Button { Content = Strings.CheckForUpdates, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 14, 0, 0), Padding = new Thickness(12, 5, 12, 5) };
        check.Click += (_, _) => CheckForUpdatesRequested?.Invoke();
        panel.Children.Add(check);

        var link = new Hyperlink(new Run("github.com/ashwin-mathias/Screenshots")) { NavigateUri = new Uri(Strings.RepoUrl) };
        link.SetResourceReference(TextElement.ForegroundProperty, "ShotAccent");
        link.RequestNavigate += (_, e) => { OpenUrl(e.Uri.AbsoluteUri); e.Handled = true; };
        var licence = new TextBlock { FontSize = 12, Margin = new Thickness(0, 14, 0, 0) };
        licence.SetResourceReference(TextBlock.ForegroundProperty, "ShotTextSecondary");
        licence.Inlines.Add(new Run("MIT licence · "));
        licence.Inlines.Add(link);
        panel.Children.Add(licence);
        return panel;
    }

    // ---- How to Use (HELP-002) ----------------------------------------------------------------

    private static readonly (string Heading, string Body)[] Help =
    [
        ("Capture and paste",
            "Press Alt+S (or Print Screen, if you turned it on), let go of the keys, then drag across the area you want. " +
            "Screenshot saves it as a PNG and copies the image, so you can paste it straight away with Ctrl+V. Press Esc or right-click to cancel."),
        ("Where files go",
            "Screenshots are saved in Documents\\screenshots, named like \"Screenshot 2026-11-01 at 14.03.27.125-….png\". Nothing is ever overwritten or deleted."),
        ("One display at a time",
            "The selection stays on the display where the pointer was when you started. Windows' own Win+Shift+S still works and isn't affected."),
        ("The preview",
            "After a capture, a small preview appears in the corner for 8 seconds. It never takes your typing focus. Hover over it to keep it open; " +
            "it closes 8 seconds after you move away. Copy, Reveal and Mark Up act on that file. Turn it off with Show Thumbnail After Capture."),
        ("Last in this session, or newest on disk",
            "Reveal Last Screenshot shows the last capture since Screenshot started. Show Last Screenshot Preview shows the newest PNG in the folder, " +
            "even after a restart. Editing an older file can make it the newest."),
        ("Recent Screenshots",
            "The newest 20 PNG files in the folder, read fresh each time. Each has Open, Copy Image, Show in File Explorer, Show Preview and Mark Up in Paint."),
        ("Shortcuts and typing",
            "Each shortcut can be turned off in the menu. While Alt+S is on, apps that use Alt+S (for example Send in Outlook) don't receive it; " +
            "Ctrl+Enter still sends in Outlook. Pressing Alt, letting go, then S (Office ribbon shortcuts) still works."),
        ("Print Screen",
            "Windows normally uses Print Screen for its own tool. To use it here, turn off \"Use the Print screen key to open screen capture\" " +
            "in Settings → Accessibility → Keyboard, then turn on Enable Print Screen Shortcut."),
        ("Finding the icon",
            "Windows may hide new icons. Click ^ next to the clock and drag Screenshot onto the taskbar to keep it visible."),
        ("Launch at login and updates",
            "Launch at Login starts Screenshot when you sign in. Screenshot checks GitHub for updates once a day and always asks before installing; " +
            "turn this off with Check for Updates Automatically."),
        ("Closing and quitting",
            "Closing a window leaves Screenshot running in the tray. Choose Quit Screenshot to stop it."),
    ];

    private static UIElement HowToUse()
    {
        var panel = new StackPanel { Margin = new Thickness(24) };
        foreach (var (heading, body) in Help)
        {
            panel.Children.Add(Text(heading, 13, FontWeights.SemiBold, top: panel.Children.Count == 0 ? 0 : 14));
            panel.Children.Add(Selectable(body, 13));
        }
        return panel;
    }

    // ---- Release History ------------------------------------------------------------------------

    private static UIElement ReleaseHistory()
    {
        var panel = new StackPanel { Margin = new Thickness(24) };
        var entries = ChangelogEntry.Load();
        if (entries.Count == 0)
        {
            panel.Children.Add(Selectable("Release history isn't available in this build.", 13));
            return panel;
        }
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(Text($"Version {e.Version}", 15, FontWeights.SemiBold));
            if (i == 0)
            {
                var tag = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(5, 0, 5, 1), Margin = new Thickness(10, 3, 0, 0), VerticalAlignment = VerticalAlignment.Top };
                tag.SetResourceReference(Border.BorderBrushProperty, "ShotAccent");
                var t = new TextBlock { Text = "LATEST", FontSize = 10, FontWeight = FontWeights.SemiBold };
                t.SetResourceReference(TextBlock.ForegroundProperty, "ShotAccent");
                tag.Child = t;
                head.Children.Add(tag);
            }
            var card = new StackPanel { Margin = new Thickness(0, i == 0 ? 0 : 20, 0, 0) };
            card.Children.Add(head);
            card.Children.Add(Secondary(e.Date));
            card.Children.Add(Text(e.Title, 13, FontWeights.SemiBold, top: 6));
            foreach (var n in e.Notes) card.Children.Add(Selectable("•  " + n, 13));
            panel.Children.Add(card);
        }
        return panel;
    }

    // ---- helpers ----------------------------------------------------------------------------------

    private static StackPanel Row(FrameworkElement image, params TextBlock[] lines)
    {
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
        foreach (var l in lines) text.Children.Add(l);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(image);
        row.Children.Add(text);
        return row;
    }

    private static FrameworkElement Circle(ImageSource source, double size, bool round)
    {
        var brush = new ImageBrush(source) { Stretch = Stretch.UniformToFill };
        var e = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(round ? size / 2 : 0), Background = brush };
        System.Windows.Automation.AutomationProperties.SetName(e, round ? "Photo" : "Screenshot icon");
        return e;
    }

    private static TextBlock Text(string text, double size, FontWeight weight, double top = 0) =>
        new() { Text = text, FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, top, 0, 0) };

    private static TextBlock Secondary(string text)
    {
        var t = new TextBlock { Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        t.SetResourceReference(TextBlock.ForegroundProperty, "ShotTextSecondary");
        return t;
    }

    /// <summary>Wrapping text the user can select and copy, but not edit.</summary>
    private static TextBox Selectable(string text, double size) => new()
    {
        Text = text, FontSize = size, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0),
        Background = Brushes.Transparent, Padding = new Thickness(0), Margin = new Thickness(-2, 2, 0, 0), Focusable = true,
    };

    public static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { Log.Write($"Couldn't open {url}: {e.Message}"); }
    }
}

/// <summary>shared/about.json.</summary>
internal sealed record AboutInfo(string Name, string Role, string SparkyNote)
{
    public static AboutInfo Load()
    {
        var fallback = new AboutInfo("Ashwin Mathias", "Creator of Screenshot", "Designed with love for Sparky, who always camps out where I work.");
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "about.json")));
            var r = doc.RootElement;
            string Get(string k, string d) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? d : d;
            return new AboutInfo(Get("name", fallback.Name), Get("role", fallback.Role), Get("sparkyNote", fallback.SparkyNote));
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return fallback;
        }
    }
}

/// <summary>One entry of shared/changelog.json.</summary>
internal sealed record ChangelogEntry(string Version, string Date, string Title, IReadOnlyList<string> Notes)
{
    public static IReadOnlyList<ChangelogEntry> Load()
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "changelog.json")));
            var list = new List<ChangelogEntry>();
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                string S(string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
                var notes = e.TryGetProperty("notes", out var n) && n.ValueKind == JsonValueKind.Array
                    ? n.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : [];
                list.Add(new ChangelogEntry(S("version"), S("date"), S("title"), notes));
            }
            return list;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
        {
            return [];
        }
    }
}
