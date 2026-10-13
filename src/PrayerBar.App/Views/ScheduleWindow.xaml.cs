using System.Windows;
using PrayerBar.Core;

namespace PrayerBar.App.Views;
public partial class ScheduleWindow : Window
{
    public ScheduleWindow() { InitializeComponent(); }
    public void Update(Settings settings, CacheSnapshot cache, string status)
    {
        var date = PrayerEngine.Today(DateTimeOffset.UtcNow, settings.TimeZoneId);
        Location.Text = $"{settings.City} · {date:dddd, dd MMMM}\n{settings.TimeZoneId}";
        var day = cache.Days.FirstOrDefault(d => d.Date == date);
        Times.ItemsSource = day?.Events.Select(e => new { Name = PrayerEngine.Name(e.Prayer, settings.Language), Time = e.At.ToString("HH:mm") });
        SourceLabel.Text = $"AlAdhan · {settings.Method} · {settings.School}\n{status}\nLast successful update: {(cache.LastUpdated is { } at ? TimeZoneInfo.ConvertTime(at, TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId)).ToString("g") : "unavailable")}";
        if (day is null) SourceLabel.Text = "Today's prayer times are unavailable.\n" + SourceLabel.Text;
    }
}
