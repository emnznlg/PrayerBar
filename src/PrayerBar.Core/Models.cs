using System.Globalization;
using System.Text.Json;

namespace PrayerBar.Core;

public enum Prayer { Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha }
public enum CalculationMethod { Diyanet = 13, ISNA = 2, MuslimWorldLeague = 3 }
public enum AsrSchool { Standard = 0, Hanafi = 1 }
public enum DisplayLanguage { Turkish, English }
public enum Appearance { Dark, Light }

public sealed class Settings
{
    public DisplayLanguage Language { get; set; } = DisplayLanguage.Turkish;
    public string City { get; set; } = "Vancouver";
    public string Country { get; set; } = "Canada";
    public string TimeZoneId { get; set; } = "America/Vancouver";
    public CalculationMethod Method { get; set; } = CalculationMethod.Diyanet;
    public AsrSchool School { get; set; }
    public bool StartWithWindows { get; set; }
    public bool LaunchMinimized { get; set; }
    public bool AlwaysOnTop { get; set; } = true;
    public bool AutoHideFullscreen { get; set; } = true;
    public bool GlobalFullscreenHide { get; set; }
    public double Opacity { get; set; } = .94;
    public double Scale { get; set; } = 1;
    public bool ShowSeconds { get; set; } = true;
    public Appearance Appearance { get; set; }
    public bool Notifications { get; set; } = true;
    public int ReminderMinutes { get; set; }
    public bool SuppressFullscreenNotifications { get; set; } = true;
    // Physical desktop coordinates; WPF content still uses device-independent units.
    public int? X { get; set; }
    public int? Y { get; set; }
    public string DataKey => $"{City.Trim().ToUpperInvariant()}|{Country.Trim().ToUpperInvariant()}|{TimeZoneId}|{(int)Method}|{(int)School}";
    public Settings Copy() => JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(this))!;
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(City) || string.IsNullOrWhiteSpace(Country)) throw new ArgumentException("Enter a city and country.");
        _ = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        if (!Enum.IsDefined(Method) || !Enum.IsDefined(School) || !Enum.IsDefined(Language) || !Enum.IsDefined(Appearance)) throw new ArgumentException("Invalid settings selection.");
        if (!double.IsFinite(Opacity) || Opacity < .35 || Opacity > 1 || !double.IsFinite(Scale) || Scale < .8 || Scale > 1.8) throw new ArgumentException("Widget opacity or size is out of range.");
        if (ReminderMinutes is not (0 or 5 or 10 or 15)) throw new ArgumentException("Choose a 0, 5, 10, or 15 minute reminder.");
    }
}

public sealed record PrayerEvent(Prayer Prayer, DateTimeOffset At);
public sealed record PrayerDay(DateOnly Date, List<PrayerEvent> Events);
public sealed record CacheSnapshot(string DataKey, DateTimeOffset? LastUpdated, List<PrayerDay> Days);

public static class PrayerEngine
{
    public static PrayerEvent? Next(IEnumerable<PrayerDay> days, DateTimeOffset now) => days
        .SelectMany(d => d.Events).Where(p => p.Prayer != Prayer.Sunrise && p.At > now).OrderBy(p => p.At).FirstOrDefault();
    public static string Countdown(PrayerEvent? next, DateTimeOffset now, bool seconds)
    {
        if (next is null) return "--:--";
        var value = TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling((next.At - now).TotalSeconds)));
        return seconds ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}" : $"{(int)value.TotalHours:00}:{value.Minutes:00}";
    }
    public static string Name(Prayer prayer, DisplayLanguage language) => language == DisplayLanguage.English ? prayer.ToString() : prayer switch
    { Prayer.Fajr => "İmsak", Prayer.Sunrise => "Güneş", Prayer.Dhuhr => "Öğle", Prayer.Asr => "İkindi", Prayer.Maghrib => "Akşam", _ => "Yatsı" };
    public static DateOnly Today(DateTimeOffset now, string zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(zone)).DateTime);
    public static DateTimeOffset LocalTime(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local)) throw new FormatException("Prayer falls in a nonexistent daylight-saving time.");
        // If the provider supplies only HH:mm during a repeated hour, choose the earlier instant explicitly.
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }
}

public static class AlAdhanParser
{
    public static PrayerDay Parse(string json, DateOnly expected, string zoneId)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.GetProperty("code").GetInt32() != 200) throw new FormatException("AlAdhan did not return a successful response.");
        var data = root.GetProperty("data");
        var date = DateOnly.ParseExact(data.GetProperty("date").GetProperty("gregorian").GetProperty("date").GetString()!, "dd-MM-yyyy", CultureInfo.InvariantCulture);
        if (date != expected) throw new FormatException("AlAdhan returned a different date.");
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var providerZone = TimeZoneInfo.FindSystemTimeZoneById(data.GetProperty("meta").GetProperty("timezone").GetString()!);
        if (!zone.HasSameRules(providerZone)) throw new FormatException("Provider timezone differs from the selected timezone.");
        var events = new List<PrayerEvent>();
        foreach (var prayer in Enum.GetValues<Prayer>())
        {
            var raw = data.GetProperty("timings").GetProperty(prayer.ToString()).GetString() ?? throw new FormatException("Missing prayer time.");
            DateTimeOffset at;
            if (raw.Length > 10 && raw[10] == 'T')
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(raw, @"(Z|[+-]\d{2}:\d{2})$")) throw new FormatException("ISO prayer time must include an explicit timezone offset.");
                if (!DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out at)) throw new FormatException("Invalid ISO prayer time.");
                // AlAdhan occasionally returns an ISO offset that disagrees with its own
                // metadata timezone around daylight-saving changes. Its date and clock
                // time are the prayer schedule; interpret that wall-clock time in the
                // selected/provider timezone instead of discarding an otherwise valid day.
                var providerClock = at.DateTime;
                if (DateOnly.FromDateTime(providerClock) != date) throw new FormatException("Prayer timestamp has an unexpected date.");
                at = PrayerEngine.LocalTime(date, TimeOnly.FromDateTime(providerClock), zone);
            }
            else
            {
                var clock = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                if (!TimeOnly.TryParseExact(clock, ["HH:mm", "H:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) throw new FormatException("Invalid prayer time.");
                at = PrayerEngine.LocalTime(date, time, zone);
            }
            events.Add(new(prayer, at));
        }
        if (!events.Zip(events.Skip(1)).All(pair => pair.First.At < pair.Second.At)) throw new FormatException("Prayer times are not in chronological order.");
        return new(date, events);
    }
}
