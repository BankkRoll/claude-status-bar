using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace ClaudeStatusBar;

/// <summary>
/// A borderless overlay docked into one monitor's taskbar. Polls the shared status file,
/// renders the icon/label, and positions itself just before that monitor's tray cluster.
/// </summary>
public partial class OverlayWindow : Window
{
    private static readonly Color Brand = Color.FromRgb(0xD9, 0x77, 0x57); // Anthropic accent
    private static readonly Color Amber = Color.FromRgb(0xF2, 0xBA, 0x2E); // awaiting-permission dot

    private const double TrayMarginDip = 8;   // gap between the widget and the tray cluster
    private const double VerticalInset = 6;    // total top+bottom gap inside the taskbar band
    private const double VerbRotateSeconds = 4;

    private readonly Settings _settings = Settings.Shared;
    private readonly StateStore _store = new();
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromMilliseconds(400) };

    private readonly string _targetDevice; // monitor device name; empty resolves to primary
    private readonly bool _isController;    // only the controller window owns app shutdown

    private bool _closed;

    // Animation, driven by CompositionTarget.Rendering gated on elapsed time.
    private bool _animating;
    private TimeSpan _lastRenderTime = TimeSpan.Zero;
    private double _frameAccumulator;
    private int _frame;

    private double _startedAt; // turn start (unix seconds), 0 when no timer should show

    private string _previousState = "";
    private MediaPlayer? _chime;

    private (double left, double top, double height, double right) _lastGeometry = (-1, -1, -1, -1);

    private readonly DateTime _launchedAt = DateTime.UtcNow;
    private DateTime? _idleSince;
    private bool _sawSession;

    private string _verb = "Thinking";
    private double _verbStartedAt;
    private int _verbIndex = -1;

    public OverlayWindow(string targetDevice = "", bool isController = true)
    {
        _targetDevice = targetDevice ?? "";
        _isController = isController;
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
    }

    private Color IconColor => Brand;

    private double Fps => _settings.Style switch
    {
        AnimStyle.Spark => 9,
        AnimStyle.Code => 5 * 18 / 3.8,
        AnimStyle.Crab => 12.5,
        _ => 9,
    };

    private int FrameCount => _settings.Style switch
    {
        AnimStyle.Spark => IconRenderer.SparkFrameCount,
        AnimStyle.Code => 5 * 18,
        AnimStyle.Crab => IconRenderer.CrabFrameCount,
        _ => IconRenderer.SparkFrameCount,
    };

    private IntPtr _hwnd;

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        SyncMenuChecks();
        ApplyWindowStyles();
        AssertTopmost();
        Reposition();

        Closed += OnClosed;

        _poll.Tick += (_, _) => Tick();
        _poll.Start();
        CompositionTarget.Rendering += OnRender;
        Tick();
    }

    // Keep the overlay out of Alt-Tab. With uiAccess=true in the manifest, a topmost window also
    // sits above the Start menu and shell flyouts, so it stays visible at all times.
    private void ApplyWindowStyles()
    {
        int ex = Native.GetWindowLong(_hwnd, Native.GWL_EXSTYLE) | Native.WS_EX_TOOLWINDOW;
        Native.SetWindowLong(_hwnd, Native.GWL_EXSTYLE, ex);
    }

    private void AssertTopmost()
    {
        if (_closed || _hwnd == IntPtr.Zero) return;
        Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        _poll.Stop();
        CompositionTarget.Rendering -= OnRender;
    }

    // ----- status polling -----

    private void Tick()
    {
        if (_closed) return;
        CheckLifecycle();
        Reposition();
        AssertTopmost(); // recover z-order each tick (cheap; no flicker since pos/size unchanged)

        var state = _store.Poll();
        _startedAt = state.StartedAt;
        MaybeChime(state);

        switch (state.State)
        {
            case "thinking":
                Render(ThinkingLabel(), animate: true);
                break;
            case "tool":
                _verbStartedAt = 0;
                Render(string.IsNullOrEmpty(state.Label) ? "Working…" : state.Label, animate: true);
                break;
            case "permission":
                _verbStartedAt = 0;
                _startedAt = 0;
                Render("Awaiting permission", animate: false, dot: true);
                break;
            default:
                _verbStartedAt = 0;
                _startedAt = 0;
                Render("Claude", animate: false);
                break;
        }
        UpdateTimer();
    }

    // ----- positioning -----

    private double DipScale()
    {
        var src = PresentationSource.FromVisual(this);
        return src?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
    }

    /// <summary>Dock into the target monitor's horizontal taskbar band, anchored before the tray.</summary>
    private void Reposition()
    {
        if (_closed) return;

        var monitors = Native.GetMonitors();
        var mon = monitors.FirstOrDefault(m => m.Device == _targetDevice)
                  ?? monitors.FirstOrDefault(m => m.Primary)
                  ?? monitors.FirstOrDefault();
        if (mon is null) { Visibility = Visibility.Collapsed; return; }

        var full = mon.Monitor;
        var work = mon.Work;
        bool bottom = work.Bottom < full.Bottom;
        bool top = work.Top > full.Top;
        if (!bottom && !top) { Visibility = Visibility.Collapsed; return; } // vertical or no taskbar

        double s = DipScale();
        double bandTop = (bottom ? work.Bottom : full.Top) * s;
        double bandHeight = (bottom ? full.Bottom - work.Bottom : work.Top - full.Top) * s;

        // Right edge tracks the tray dynamically so the pill always sits just before it.
        double rightBound;
        if (mon.Primary)
        {
            var tray = Native.GetTrayRect();
            rightBound = tray is { } t ? t.Left * s - TrayMarginDip : full.Right * s - 220;
        }
        else
        {
            rightBound = full.Right * s - 120;
        }

        Visibility = Visibility.Visible;
        Root.Height = Math.Max(0, bandHeight - VerticalInset);

        double left = rightBound - Width;
        var geometry = (Math.Round(left), Math.Round(bandTop), Math.Round(bandHeight), Math.Round(rightBound));
        if (geometry == _lastGeometry) return;
        _lastGeometry = geometry;

        Top = bandTop;
        Left = left;
    }

    // ----- rendering -----

    private void Render(string label, bool animate, bool dot = false)
    {
        StatusText.Text = label;
        if (animate)
        {
            if (!_animating) { _animating = true; _lastRenderTime = TimeSpan.Zero; }
        }
        else
        {
            _animating = false;
            _frame = 0;
            IconImg.Source = dot ? IconRenderer.Dot(Amber) : RestingIcon();
        }
    }

    private ImageSource RestingIcon() =>
        _settings.Style == AnimStyle.Crab ? IconRenderer.Crab(0) : IconRenderer.Logo(IconColor);

    private void OnRender(object? sender, EventArgs e)
    {
        if (_closed || !_animating) return;

        var now = ((RenderingEventArgs)e).RenderingTime;
        if (_lastRenderTime == TimeSpan.Zero) { _lastRenderTime = now; return; }
        double dt = (now - _lastRenderTime).TotalSeconds;
        _lastRenderTime = now;

        _frameAccumulator += dt * Fps;
        if (_frameAccumulator >= 1)
        {
            _frame = (_frame + (int)_frameAccumulator) % FrameCount;
            _frameAccumulator %= 1;
            DrawFrame();
        }
        UpdateTimer();
    }

    private void DrawFrame()
    {
        IconImg.Source = _settings.Style switch
        {
            AnimStyle.Crab => IconRenderer.Crab(_frame),
            _ => IconRenderer.Spark(_frame % IconRenderer.SparkFrameCount, IconColor),
        };
    }

    private void UpdateTimer()
    {
        if (_settings.ShowTimer && _startedAt > 0)
        {
            int secs = Math.Max(0, (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - _startedAt));
            TimerText.Text = secs >= 60 ? $"{secs / 60}m {secs % 60}s" : $"{secs}s";
        }
        else TimerText.Text = "";
    }

    // ----- thinking verbs -----

    private string ThinkingLabel()
    {
        double now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (_verbStartedAt == 0 || now - _verbStartedAt >= VerbRotateSeconds)
        {
            _verbStartedAt = now;
            _verb = PickVerb();
        }
        return _verb + "…";
    }

    private string PickVerb()
    {
        var verbs = ThinkingVerbs.All;
        if (verbs.Length == 0) return "Thinking";
        int next = _verbIndex;
        for (int i = 0; i < 3 && next == _verbIndex; i++)
            next = (_verbIndex + 7 + (int)(DateTimeOffset.UtcNow.Ticks % (verbs.Length - 1)) + 1) % verbs.Length;
        _verbIndex = next;
        return verbs[next];
    }

    // ----- completion chime -----

    private void MaybeChime(ClaudeState state)
    {
        if (state.State == "done" && _previousState != "done" && _settings.PlayCompletionSound)
            PlayChime();
        _previousState = state.State;
    }

    private void PlayChime()
    {
        try
        {
            if (_chime is null)
            {
                var path = ExtractChime();
                if (path is null) return;
                _chime = new MediaPlayer { Volume = 0.7 };
                _chime.Open(new Uri(path));
            }
            _chime.Position = TimeSpan.Zero;
            _chime.Play();
        }
        catch { }
    }

    // The chime is embedded in the assembly; MediaPlayer needs a file URI, so extract it once
    // to a temp file on first use.
    private static string? ExtractChime()
    {
        try
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var name = Array.Find(asm.GetManifestResourceNames(), n => n.EndsWith("completion.mp3", StringComparison.OrdinalIgnoreCase));
            if (name is null) return null;

            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ClaudeStatusBar.completion.mp3");
            if (!System.IO.File.Exists(path))
            {
                using var src = asm.GetManifestResourceStream(name);
                if (src is null) return null;
                using var dst = System.IO.File.Create(path);
                src.CopyTo(dst);
            }
            return path;
        }
        catch { return null; }
    }

    // ----- lifecycle -----

    private void CheckLifecycle()
    {
        if (!_isController) return;
        if ((DateTime.UtcNow - _launchedAt).TotalSeconds < 8) return;

        if (_store.SessionCount() > 0) { _sawSession = true; _idleSince = null; return; }
        if (!_sawSession) return;

        _idleSince ??= DateTime.UtcNow;
        if ((DateTime.UtcNow - _idleSince.Value).TotalSeconds >= 30)
            Application.Current.Shutdown();
    }

    // ----- context menu -----

    private void OnAnyClick(object sender, MouseButtonEventArgs e)
    {
        if (Menu == null) return;
        Activate();
        Menu.PlacementTarget = Root;
        Menu.Placement = PlacementMode.Top;
        Menu.IsOpen = true;
        e.Handled = true;
    }

    private void SyncMenuChecks()
    {
        MiTimer.IsChecked = _settings.ShowTimer;
        MiSound.IsChecked = _settings.PlayCompletionSound;
        MiStyleSpark.IsChecked = _settings.Style == AnimStyle.Spark;
        MiStyleCode.IsChecked = _settings.Style == AnimStyle.Code;
        MiStyleCrab.IsChecked = _settings.Style == AnimStyle.Crab;
        MiVersion.Header = $"Version {AppVersion}";
    }

    private static string AppVersion =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";

    private void OnToggleTimer(object sender, RoutedEventArgs e)
    {
        _settings.ShowTimer = !_settings.ShowTimer;
        _settings.Save();
        OverlayManager.Instance?.SyncAll();
    }

    private void OnToggleSound(object sender, RoutedEventArgs e)
    {
        _settings.PlayCompletionSound = !_settings.PlayCompletionSound;
        _settings.Save();
        OverlayManager.Instance?.SyncAll();
    }

    private void OnStyleSpark(object sender, RoutedEventArgs e) => SetStyle(AnimStyle.Spark);
    private void OnStyleCode(object sender, RoutedEventArgs e) => SetStyle(AnimStyle.Code);
    private void OnStyleCrab(object sender, RoutedEventArgs e) => SetStyle(AnimStyle.Crab);

    private void SetStyle(AnimStyle style)
    {
        _settings.Style = style;
        _settings.Save();
        OverlayManager.Instance?.SyncAll();
    }

    /// <summary>Re-read shared settings into this window after another window changed them.</summary>
    public void ApplySettings()
    {
        if (_closed) return;
        _animating = false;
        _frame = 0;
        _lastRenderTime = TimeSpan.Zero;
        SyncMenuChecks();
        Tick();
    }

    private void OnQuit(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void OnMonitorsOpening(object sender, RoutedEventArgs e)
    {
        MiMonitors.Items.Clear();
        var monitors = Native.GetMonitors();
        for (int i = 0; i < monitors.Count; i++)
        {
            var m = monitors[i];
            bool shown = _settings.Monitors.Count == 0 ? m.Primary : _settings.Monitors.Contains(m.Device);
            var item = new MenuItem
            {
                Header = $"Display {i + 1}{(m.Primary ? " (primary)" : "")}  {m.Monitor.Width}×{m.Monitor.Height}",
                IsCheckable = true,
                IsChecked = shown,
                Tag = m.Device,
            };
            item.Click += OnToggleMonitor;
            MiMonitors.Items.Add(item);
        }
    }

    private void OnToggleMonitor(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not string device) return;
        var monitors = Native.GetMonitors();

        var set = new HashSet<string>(_settings.Monitors.Count == 0
            ? monitors.Where(m => m.Primary).Select(m => m.Device)
            : _settings.Monitors);

        if (item.IsChecked) set.Add(device); else set.Remove(device);
        if (set.Count == 0)
        {
            var primary = monitors.FirstOrDefault(m => m.Primary);
            if (primary != null) set.Add(primary.Device);
        }

        _settings.Monitors = set.ToList();
        _settings.Save();
        OverlayManager.Instance?.Rebuild();
    }
}
