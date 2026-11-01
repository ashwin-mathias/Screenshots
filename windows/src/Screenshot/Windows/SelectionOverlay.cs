using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Screenshot.Interop;

namespace Screenshot.Windows;

/// <summary>
/// The Windows selection screen (CAP-002, DESIGN §6): the frozen frame of one display, dimmed,
/// with a crosshair. Drag to select; Esc, right-click or a tiny selection cancels.
/// Returns the selection in the frame's physical pixels.
/// </summary>
internal sealed class SelectionOverlay : Window
{
    private static readonly Brush Dim = Frozen(new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)));
    private static readonly Brush Outline = Frozen(new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0)));
    private static readonly Brush LabelBack = Frozen(new SolidColorBrush(Color.FromArgb(0xB3, 0, 0, 0)));

    private readonly BitmapSource _frame;
    private readonly Display _display;
    private readonly TaskCompletionSource<Int32Rect?> _result = new();
    private readonly Image _image;
    private readonly Path _dim;
    private readonly Rectangle _outer;
    private readonly Rectangle _inner;
    private readonly Border _label;
    private readonly TextBlock _labelText;
    private Point? _start;
    private Point _current;

    public SelectionOverlay(BitmapSource frame, Display display)
    {
        _frame = frame;
        _display = display;

        Title = "Screenshot selection";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Background = Brushes.Black;
        Cursor = Cursors.Cross;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        Focusable = true;

        _image = new Image { Source = frame, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        _dim = new Path { Fill = Dim, IsHitTestVisible = false };
        _outer = new Rectangle { Stroke = Outline, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        _inner = new Rectangle { Stroke = Brushes.White, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        _labelText = new TextBlock { Foreground = Brushes.White, FontSize = 12, FontFamily = new FontFamily("Segoe UI") };
        _label = new Border
        {
            Background = LabelBack, CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 3, 6, 3),
            Child = _labelText, IsHitTestVisible = false, Visibility = Visibility.Collapsed,
        };

        var canvas = new Canvas { IsHitTestVisible = false };
        canvas.Children.Add(_dim);
        canvas.Children.Add(_outer);
        canvas.Children.Add(_inner);
        canvas.Children.Add(_label);
        var root = new Grid();
        root.Children.Add(_image);
        root.Children.Add(canvas);
        Content = root;

        SizeChanged += (_, _) => Redraw();
        MouseLeftButtonDown += OnDown;
        MouseMove += OnMove;
        MouseLeftButtonUp += OnUp;
        MouseRightButtonDown += (_, e) => { e.Handled = true; Finish(null); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Finish(null); } };
        Deactivated += (_, _) => Finish(null); // never leave a stuck overlay if focus is taken away
        Closed += (_, _) => _result.TrySetResult(null);
    }

    /// <summary>Shows the overlay on its display and waits for the user.</summary>
    public Task<Int32Rect?> SelectAsync()
    {
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        // Put it on the right display first, then maximise there: covers that display exactly at its own DPI.
        var b = _display.Bounds;
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, b.Left, b.Top, Math.Max(1, b.Width / 2), Math.Max(1, b.Height / 2), Native.SWP_NOACTIVATE);
        WindowState = WindowState.Maximized;
        Show();
        Activate();
        Native.SetForegroundWindow(hwnd);
        Focus();
        Keyboard.Focus(this);
        Redraw();
        return _result.Task;
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _start = Clamp(e.GetPosition(_image));
        _current = _start.Value;
        CaptureMouse();
        Redraw();
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_start is null) return;
        _current = Clamp(e.GetPosition(_image)); // stays on this display (CAP-002 step 4)
        Redraw();
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (_start is null) return;
        _current = Clamp(e.GetPosition(_image));
        ReleaseMouseCapture();
        var rect = ToPixels(_start.Value, _current);
        Finish(rect.Width < 2 || rect.Height < 2 ? null : rect);
    }

    private void Finish(Int32Rect? rect)
    {
        if (_result.Task.IsCompleted) return;
        _result.TrySetResult(rect);
        if (IsMouseCaptured) ReleaseMouseCapture();
        Close();
    }

    private Point Clamp(Point p) =>
        new(Math.Clamp(p.X, 0, _image.ActualWidth), Math.Clamp(p.Y, 0, _image.ActualHeight));

    /// <summary>DIPs on screen → the frozen frame's physical pixels.</summary>
    private Int32Rect ToPixels(Point a, Point b)
    {
        if (_image.ActualWidth <= 0 || _image.ActualHeight <= 0) return Int32Rect.Empty;
        double sx = _frame.PixelWidth / _image.ActualWidth, sy = _frame.PixelHeight / _image.ActualHeight;
        int x0 = (int)Math.Round(Math.Min(a.X, b.X) * sx), y0 = (int)Math.Round(Math.Min(a.Y, b.Y) * sy);
        int x1 = (int)Math.Round(Math.Max(a.X, b.X) * sx), y1 = (int)Math.Round(Math.Max(a.Y, b.Y) * sy);
        x0 = Math.Clamp(x0, 0, _frame.PixelWidth); x1 = Math.Clamp(x1, 0, _frame.PixelWidth);
        y0 = Math.Clamp(y0, 0, _frame.PixelHeight); y1 = Math.Clamp(y1, 0, _frame.PixelHeight);
        return new Int32Rect(x0, y0, x1 - x0, y1 - y0);
    }

    private void Redraw()
    {
        var full = new Rect(0, 0, Math.Max(0, _image.ActualWidth), Math.Max(0, _image.ActualHeight));
        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        group.Children.Add(new RectangleGeometry(full));
        if (_start is null)
        {
            _dim.Data = group;
            _outer.Visibility = _inner.Visibility = _label.Visibility = Visibility.Collapsed;
            return;
        }

        var sel = new Rect(_start.Value, _current);
        group.Children.Add(new RectangleGeometry(sel));
        _dim.Data = group;

        // 1 physical pixel white border inside a 1 px half-black outline (DESIGN §6).
        var px = 1.0 / VisualTreeHelper.GetDpi(this).DpiScaleX;
        Place(_inner, sel, px);
        Place(_outer, new Rect(sel.X - px, sel.Y - px, sel.Width + 2 * px, sel.Height + 2 * px), px);

        var pixels = ToPixels(_start.Value, _current);
        _labelText.Text = $"{pixels.Width} × {pixels.Height}";
        _label.Visibility = Visibility.Visible;
        _label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _label.DesiredSize;
        var x = sel.Right - size.Width;
        var y = sel.Bottom + 8;
        if (y + size.Height > full.Height) y = sel.Bottom - size.Height - 8; // no room below: move inside
        Canvas.SetLeft(_label, Math.Max(0, x));
        Canvas.SetTop(_label, Math.Max(0, y));
    }

    private static void Place(Rectangle r, Rect at, double thickness)
    {
        r.StrokeThickness = thickness;
        r.Width = Math.Max(0, at.Width);
        r.Height = Math.Max(0, at.Height);
        Canvas.SetLeft(r, at.X);
        Canvas.SetTop(r, at.Y);
        r.Visibility = Visibility.Visible;
    }

    private static Brush Frozen(Brush b)
    {
        b.Freeze();
        return b;
    }
}
