using Microsoft.Win32;

namespace PrayerBar.App.Services;

internal static class StartupService
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key, true);
        if (!enabled) { key.DeleteValue("PrayerBar", false); return; }
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate PrayerBar executable.");
        if (!string.Equals(System.IO.Path.GetFileName(executable), "PrayerBar.exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Startup registration requires the packaged PrayerBar.exe. Launch the executable directly.");
        key.SetValue("PrayerBar", $"\"{executable}\" --startup", RegistryValueKind.String);
    }
}
