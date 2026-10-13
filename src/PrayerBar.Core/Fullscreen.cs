namespace PrayerBar.Core;

public readonly record struct Rectangle(int Left, int Top, int Right, int Bottom)
{
    public bool Covers(Rectangle other, int tolerance = 2) => Left <= other.Left + tolerance && Top <= other.Top + tolerance && Right >= other.Right - tolerance && Bottom >= other.Bottom - tolerance;
}
public sealed record WindowGeometry(Rectangle Bounds, Rectangle Monitor, bool OwnWindow, bool ShellWindow, bool Visible, bool Minimized, bool Maximized, bool HasCaption, bool SameMonitor);
public static class FullscreenClassifier
{
    public static bool IsFullscreen(WindowGeometry window, bool global) => !window.OwnWindow && !window.ShellWindow && window.Visible && !window.Minimized
        && (global || window.SameMonitor) && !(window.Maximized && window.HasCaption) && window.Bounds.Covers(window.Monitor);
}
public sealed class VisibilityDebouncer
{
    private bool candidate;
    private int samples;
    public bool IsFullscreen { get; private set; }
    public bool Update(bool detected)
    {
        if (candidate != detected) { candidate = detected; samples = 1; } else samples++;
        if (samples >= 2) IsFullscreen = candidate;
        return IsFullscreen;
    }
}
