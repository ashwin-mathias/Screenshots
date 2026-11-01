using System.Threading;
using System.Windows;
using Screenshot.Interop;
using Screenshot.Services;

namespace Screenshot;

/// <summary>Entry point: one copy only (LIFE-001), tray app with no main window.</summary>
public partial class App : Application
{
    private const string MutexName = @"Local\AshwinMathias.Screenshot";
    private Mutex? _single;
    private AppController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _single = new Mutex(initiallyOwned: true, MutexName, out var first);
        if (!first)
        {
            // Another copy is running: ask it to show Welcome, then leave.
            MessageHost.SignalRunningCopy();
            _single.Dispose();
            _single = null;
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Write($"Unexpected error: {args.Exception}");
            args.Handled = true;
        };

        _controller = new AppController(this);
        _controller.Start(updatedRestart: e.Args.Contains("--updated"));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        if (_single is not null)
        {
            _single.ReleaseMutex();
            _single.Dispose();
        }
        base.OnExit(e);
    }
}
