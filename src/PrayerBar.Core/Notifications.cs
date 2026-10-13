namespace PrayerBar.Core;

public sealed record PrayerNotice(string Key, PrayerEvent Event, bool Advance);
public static class NotificationEngine
{
    // Only the current 90-second window is eligible; resume does not flood the user with missed prayers.
    public static IEnumerable<PrayerNotice> Due(IEnumerable<PrayerDay> days, Settings settings, DateTimeOffset now, ISet<string> delivered)
    {
        if (!settings.Notifications) yield break;
        foreach (var prayer in days.SelectMany(d => d.Events).Where(e => e.Prayer != Prayer.Sunrise))
        foreach (var advance in new[] { false, true })
        {
            if (advance && settings.ReminderMinutes == 0) continue;
            var at = advance ? prayer.At.AddMinutes(-settings.ReminderMinutes) : prayer.At;
            var key = $"{settings.DataKey}|{prayer.Prayer}|{prayer.At.ToUnixTimeSeconds()}|{(advance ? settings.ReminderMinutes : 0)}";
            if (now >= at && now - at < TimeSpan.FromSeconds(90) && !delivered.Contains(key)) yield return new(key, prayer, advance);
        }
    }
}
