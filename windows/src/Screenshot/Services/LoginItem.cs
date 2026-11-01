using StartupTask = global::Windows.ApplicationModel.StartupTask;
using StartupTaskState = global::Windows.ApplicationModel.StartupTaskState;

namespace Screenshot.Services;

/// <summary>Launch at login through the MSIX startup task (LOGIN-001). The system's state is the truth.</summary>
internal static class LoginItem
{
    public const string TaskId = "ScreenshotStartup";

    public enum State { Enabled, Disabled, DisabledByUser, DisabledByPolicy, Unavailable }

    public static async Task<State> GetStateAsync()
    {
        if (!AppInfo.IsPackaged) return State.Unavailable;
        try
        {
            var task = await StartupTask.GetAsync(TaskId);
            return Map(task.State);
        }
        catch (Exception e) when (e is ArgumentException or System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            Log.Write($"Startup task unavailable: {e.Message}");
            return State.Unavailable;
        }
    }

    /// <summary>Applies the menu choice. Returns the new state.</summary>
    public static async Task<State> ToggleAsync()
    {
        var task = await StartupTask.GetAsync(TaskId);
        switch (task.State)
        {
            case StartupTaskState.Enabled:
            case StartupTaskState.EnabledByPolicy:
                task.Disable();
                break;
            case StartupTaskState.Disabled:
                await task.RequestEnableAsync();
                break;
        }
        return Map(task.State);
    }

    private static State Map(StartupTaskState s) => s switch
    {
        StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy => State.Enabled,
        StartupTaskState.DisabledByUser => State.DisabledByUser,
        StartupTaskState.DisabledByPolicy => State.DisabledByPolicy,
        _ => State.Disabled,
    };
}
