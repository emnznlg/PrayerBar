using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using PrayerBar.App.Infrastructure;
using PrayerBar.Core;

namespace PrayerBar.App.Views;

public partial class WidgetWindow : Window
{
    public event Action? ScheduleRequested;
    public event Action? MenuRequested;
    public event Action? ToggleRequested;
    public event Action<int, int>? PositionChanged;
    private Point down;
    private bool pressed, dragged;
    private HwndSource? source;
    private bool exiting;
    public nint Handle { get; private set; }
    public WidgetWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            source = HwndSource.FromHwnd(Handle);
            source.AddHook(Hook);
            Native.SetWindowLongPtr(Handle, -20, Native.GetWindowLongPtr(Handle, -20) | 0x08000000 | 0x80);
            if (!Native.RegisterHotKey(Handle, 1, 0x4000 | 2 | 4, 0x50)) Log.Write("Ctrl+Alt+P is already registered by another application. Tray access remains available.");
        };
        Closing += OnClosing;
        Closed += (_, _) => { Native.UnregisterHotKey(Handle, 1); source?.RemoveHook(Hook); };
    }
    private nint Hook(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x21) { handled = true; return 3; } // MA_NOACTIVATE
        if (message == 0x312) { handled = true; ToggleRequested?.Invoke(); }
        return 0;
    }
    private void OnClosing(object? sender, CancelEventArgs e) { if (!exiting) { e.Cancel = true; ToggleRequested?.Invoke(); } }
    public void Exit() { exiting = true; Close(); }
    private void OnLeftDown(object sender, MouseButtonEventArgs e) { down = e.GetPosition(this); pressed = true; dragged = false; ((UIElement)sender).CaptureMouse(); }
    private void OnMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!pressed || dragged || e.LeftButton != MouseButtonState.Pressed) return;
        var delta = e.GetPosition(this) - down;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        dragged = true;
        ((UIElement)sender).ReleaseMouseCapture();
        try { DragMove(); } catch (InvalidOperationException ex) { Log.Write("Widget drag interrupted.", ex); }
        pressed = false;
        KeepOnScreen();
        if (Native.GetWindowRect(Handle, out var rect)) PositionChanged?.Invoke(rect.Left, rect.Top);
    }
    private void OnLeftUp(object sender, MouseButtonEventArgs e) { ((UIElement)sender).ReleaseMouseCapture(); if (pressed && !dragged) ScheduleRequested?.Invoke(); pressed = false; }
    private void OnRightUp(object sender, MouseButtonEventArgs e) => MenuRequested?.Invoke();
    public void Place(Settings settings, bool reset = false)
    {
        Width = 228 * settings.Scale; Height = 34 * settings.Scale;
        ContentScale(settings.Scale);
        Opacity = settings.Opacity; Topmost = settings.AlwaysOnTop;
        UpdateLayout();
        if (!reset && settings.X is int x && settings.Y is int y) Native.SetWindowPos(Handle, 0, x, y, 0, 0, 0x0015);
        else
        {
            var work = Native.Monitor(Handle).Work;
            Native.GetWindowRect(Handle, out var rect);
            Native.SetWindowPos(Handle, 0, work.Right - (rect.Right - rect.Left) - 18, work.Bottom - (rect.Bottom - rect.Top) - 18, 0, 0, 0x0015);
        }
        KeepOnScreen();
    }
    private void ContentScale(double scale) => ((FrameworkElement)Content).LayoutTransform = new System.Windows.Media.ScaleTransform(scale, scale);
    public void KeepOnScreen()
    {
        if (Handle == 0 || !Native.GetWindowRect(Handle, out var rect)) return;
        var work = Native.Monitor(Handle).Work;
        var x = Math.Clamp(rect.Left, work.Left, Math.Max(work.Left, work.Right - (rect.Right - rect.Left)));
        var y = Math.Clamp(rect.Top, work.Top, Math.Max(work.Top, work.Bottom - (rect.Bottom - rect.Top)));
        if (x != rect.Left || y != rect.Top) Native.SetWindowPos(Handle, 0, x, y, 0, 0, 0x0015);
    }
}
