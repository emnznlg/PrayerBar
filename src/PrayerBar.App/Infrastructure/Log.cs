using System.IO;
using System.Text.Json;

namespace PrayerBar.App.Infrastructure;

public static class Log
{
    private static readonly object Gate = new();
    public static string DirectoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrayerBar");
    public static void Write(string message, Exception? exception = null)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                var path = Path.Combine(DirectoryPath, "PrayerBar.log");
                if (File.Exists(path) && new FileInfo(path).Length > 1_000_000) File.Move(path, path + ".old", true);
                File.AppendAllText(path, JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow, message, error = exception?.ToString() }) + Environment.NewLine);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { System.Diagnostics.Trace.WriteLine(ex); }
        }
    }
}
