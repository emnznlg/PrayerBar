using System.Text.Json;
using PrayerBar.Core;
using Xunit;

namespace PrayerBar.Tests;

public sealed class EngineTests
{
    private static readonly DateOnly Date = new(2026, 1, 15);
    internal static PrayerDay Day(DateOnly date) => new(date, Enum.GetValues<Prayer>().Select((p, i) => new PrayerEvent(p, PrayerEngine.LocalTime(date, new TimeOnly(new[] { 6, 8, 12, 15, 17, 19 }[i], 0), TimeZoneInfo.FindSystemTimeZoneById("America/Vancouver")))).ToList());
    [Theory]
    [InlineData(5, Prayer.Fajr)] [InlineData(6, Prayer.Dhuhr)] [InlineData(8, Prayer.Dhuhr)] [InlineData(12, Prayer.Asr)] [InlineData(15, Prayer.Maghrib)] [InlineData(17, Prayer.Isha)]
    public void NextPrayerExcludesSunriseAndTransitionsAtBoundary(int hour, Prayer expected)
    {
        var day = Day(Date); var now = new DateTimeOffset(2026, 1, 15, hour, 0, 0, TimeSpan.FromHours(-8));
        Assert.Equal(expected, PrayerEngine.Next([day], now)!.Prayer);
    }
    [Fact] public void IshaTransitionsToTomorrowFajr()
    {
        var next = PrayerEngine.Next([Day(Date), Day(Date.AddDays(1))], new(2026, 1, 15, 19, 0, 0, TimeSpan.FromHours(-8)));
        Assert.Equal(Prayer.Fajr, next!.Prayer); Assert.Equal(Date.AddDays(1), DateOnly.FromDateTime(next.At.DateTime));
    }
    [Fact] public void MidnightUsesLocationDate()
    {
        var now = new DateTimeOffset(2026, 1, 16, 8, 0, 0, TimeSpan.Zero);
        Assert.Equal(Date.AddDays(1), PrayerEngine.Today(now, "America/Vancouver"));
        Assert.Equal(Prayer.Fajr, PrayerEngine.Next([Day(Date), Day(Date.AddDays(1))], now)!.Prayer);
    }
    [Fact] public void NoDataDoesNotInventTimes() { Assert.Null(PrayerEngine.Next([], DateTimeOffset.UtcNow)); Assert.Equal("--:--", PrayerEngine.Countdown(null, DateTimeOffset.UtcNow, true)); }
    [Fact] public void CountdownUsesClockAndClampsNegative()
    {
        var prayer = Day(Date).Events[0]; Assert.Equal("00:01:30", PrayerEngine.Countdown(prayer, prayer.At.AddSeconds(-90), true));
        Assert.Equal("00:00:00", PrayerEngine.Countdown(prayer, prayer.At.AddHours(1), true));
    }
    [Fact] public void DstSpringHasCorrectOffsetAndRejectsGap()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        Assert.Equal(TimeSpan.FromHours(-4), PrayerEngine.LocalTime(new(2026, 3, 8), new(6, 0), zone).Offset);
        Assert.Throws<FormatException>(() => PrayerEngine.LocalTime(new(2026, 3, 8), new(2, 30), zone));
    }
    [Fact] public void DstFallChoosesEarlierRepeatedInstant()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        Assert.Equal(TimeSpan.FromHours(-4), PrayerEngine.LocalTime(new(2026, 11, 1), new(1, 30), zone).Offset);
        Assert.Equal(TimeSpan.FromHours(-5), PrayerEngine.LocalTime(new(2026, 11, 1), new(6, 0), zone).Offset);
    }
    [Fact] public void SettingsRoundTripAndValidation()
    {
        var settings = new Settings { X = -2200, Language = DisplayLanguage.English, School = AsrSchool.Hanafi, Opacity = .8 };
        var restored = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings))!;
        restored.Validate(); Assert.Equal(settings.DataKey, restored.DataKey); Assert.Equal(-2200, restored.X); Assert.Equal(settings.Language, restored.Language);
        restored.Opacity = double.NaN; Assert.Throws<ArgumentException>(restored.Validate);
    }
    [Fact] public void UriEncodesLocationAndIncludesDocumentedParameters()
    {
        var uri = AlAdhanProvider.RequestUri(new Settings { City = "A & B" }, Date);
        Assert.Contains("city=A%20%26%20B", uri.AbsoluteUri); Assert.Contains("method=13", uri.Query); Assert.Contains("school=0", uri.Query); Assert.Contains("iso8601=true", uri.Query);
    }
    [Fact] public void NotificationsExcludeSunriseAndAvoidDuplicates()
    {
        var settings = new Settings { ReminderMinutes = 10 }; var day = Day(Date); var at = day.Events[0].At;
        var notices = NotificationEngine.Due([day], settings, at, new HashSet<string>()).ToList(); Assert.Single(notices);
        Assert.Empty(NotificationEngine.Due([day], settings, at, notices.Select(n => n.Key).ToHashSet()));
        Assert.Empty(NotificationEngine.Due([day], settings, day.Events[1].At, new HashSet<string>()));
        Assert.Single(NotificationEngine.Due([day], settings, at.AddMinutes(-10), new HashSet<string>()));
        Assert.Empty(NotificationEngine.Due([day], settings, at.AddMinutes(2), new HashSet<string>()));
    }
}
