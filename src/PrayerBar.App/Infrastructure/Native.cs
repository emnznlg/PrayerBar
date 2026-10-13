using System.Runtime.InteropServices;
using System.Text;
using PrayerBar.Core;

namespace PrayerBar.App.Infrastructure;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; public readonly Rectangle Geometry => new(Left, Top, Right, Bottom); }
    [StructLayout(LayoutKind.Sequential)] internal struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(nint window, int attribute, out Rect rect, int size);
    [DllImport("user32.dll")] internal static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll")] internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] internal static extern bool IsZoomed(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(nint window, StringBuilder text, int length);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] internal static extern nint SetWindowLongPtr(nint window, int index, nint value);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(nint window, int id);
    internal static MonitorInfo Monitor(nint window)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(window, 2), ref info)) throw new InvalidOperationException("Monitor information is unavailable.");
        return info;
    }
    internal static bool Fullscreen(nint widget, bool global)
    {
        var foreground = GetForegroundWindow();
        if (foreground == 0) return false;
        GetWindowThreadProcessId(foreground, out var pid);
        var name = new StringBuilder(256);
        GetClassName(foreground, name, name.Capacity);
        var shell = name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
        if (!GetWindowRect(foreground, out var bounds)) return false;
        if (DwmGetWindowAttribute(foreground, 9, out var extended, Marshal.SizeOf<Rect>()) == 0) bounds = extended;
        var monitor = Monitor(foreground);
        var style = (long)GetWindowLongPtr(foreground, -16);
        return FullscreenClassifier.IsFullscreen(new(bounds.Geometry, monitor.Monitor.Geometry, pid == Environment.ProcessId, shell,
            IsWindowVisible(foreground), IsIconic(foreground), IsZoomed(foreground), (style & 0x00C00000) != 0,
            MonitorFromWindow(widget, 2) == MonitorFromWindow(foreground, 2)), global);
    }
}
