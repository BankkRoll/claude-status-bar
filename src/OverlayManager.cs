using System.Windows;

namespace ClaudeStatusBar;

/// <summary>
/// Creates and tracks one overlay window per selected monitor. The first window created is the
/// controller and owns the application's idle-shutdown lifecycle.
/// </summary>
public sealed class OverlayManager
{
    public static OverlayManager? Instance { get; private set; }

    private readonly List<OverlayWindow> _windows = new();

    public OverlayManager() => Instance = this;

    public void Start() => Rebuild();

    /// <summary>Recreate overlay windows to match the current monitor selection.</summary>
    public void Rebuild() => Application.Current?.Dispatcher.BeginInvoke(RebuildCore);

    private void RebuildCore()
    {
        foreach (var window in _windows)
        {
            try { window.Close(); } catch { }
        }
        _windows.Clear();

        var monitors = Native.GetMonitors();
        if (monitors.Count == 0) return;

        var selected = Settings.Shared.Monitors.Count > 0
            ? monitors.Where(m => Settings.Shared.Monitors.Contains(m.Device)).ToList()
            : monitors.Where(m => m.Primary).ToList();
        if (selected.Count == 0) selected = monitors.Where(m => m.Primary).ToList();
        if (selected.Count == 0) selected = new() { monitors[0] };

        bool controller = true;
        foreach (var monitor in selected)
        {
            var window = new OverlayWindow(monitor.Device, controller);
            controller = false;
            _windows.Add(window);
            window.Show();
        }
    }

    /// <summary>Push shared-settings changes to every open overlay window.</summary>
    public void SyncAll()
    {
        foreach (var window in _windows)
            window.ApplySettings();
    }
}
