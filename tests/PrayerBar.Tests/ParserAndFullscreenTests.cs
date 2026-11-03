using System.Text.Json;
using PrayerBar.Core;
using Xunit;

namespace PrayerBar.Tests;

public sealed class ParserAndFullscreenTests
{
    private static readonly DateOnly Date = new(2026, 1, 15);
    private static string Response(string fajr = "06:00 (PST)", string zone = "America/Vancouver", string date = "15-01-2026") => JsonSerializer.Serialize(new
    {
        code = 200, data = new { date = new { gregorian = new { date } }, meta = new { timezone = zone }, timings = new { Fajr = fajr, Sunrise = "08:00", Dhuhr = "12:00", Asr = "15:00", Maghrib = "17:00", Isha = "19:00" } }
    });
    [Fact] public void ParsesDecoratedTimes() { var day = AlAdhanParser.Parse(Response(), Date, "America/Vancouver"); Assert.Equal(6, day.Events[0].At.Hour); Assert.Equal(TimeSpan.FromHours(-8), day.Events[0].At.Offset); }
    [Fact] public void ParsesIsoTimestamps() => Assert.Equal(6, AlAdhanParser.Parse(Response("2026-01-15T06:00:00-08:00"), Date, "America/Vancouver").Events[0].At.Hour);
    [Theory] [InlineData("bad")] [InlineData("25:10")] [InlineData("2026-01-16T06:00:00-08:00")] [InlineData("20:00")]
    public void RejectsInvalidTiming(string time) => Assert.Throws<FormatException>(() => AlAdhanParser.Parse(Response(time), Date, "America/Vancouver"));
    [Fact] public void NormalizesProviderWallClockWhenIsoOffsetDisagreesWithTimezone()
    {
        var prayer = AlAdhanParser.Parse(Response("2026-01-15T06:00:00+00:00"), Date, "America/Vancouver").Events[0];
        Assert.Equal(6, prayer.At.Hour);
        Assert.Equal(TimeSpan.FromHours(-8), prayer.At.Offset);
    }
    [Fact] public void RejectsWrongDateAndZone()
    {
        Assert.Throws<FormatException>(() => AlAdhanParser.Parse(Response(date: "16-01-2026"), Date, "America/Vancouver"));
        Assert.Throws<FormatException>(() => AlAdhanParser.Parse(Response(zone: "America/New_York"), Date, "America/Vancouver"));
        Assert.Throws<FormatException>(() => AlAdhanParser.Parse("{\"code\":400}", Date, "America/Vancouver"));
        Assert.ThrowsAny<JsonException>(() => AlAdhanParser.Parse("broken", Date, "America/Vancouver"));
    }
    [Theory]
    [InlineData(false, false, false, false, true, true)]
    [InlineData(false, false, true, true, true, false)]
    [InlineData(true, false, false, false, true, false)]
    [InlineData(false, true, false, false, true, false)]
    [InlineData(false, false, false, false, false, false)]
    public void ClassifiesFullscreen(bool own, bool shell, bool maximized, bool caption, bool sameMonitor, bool expected)
    {
        var full = new Rectangle(0, 0, 1920, 1080);
        var window = new WindowGeometry(full, full, own, shell, true, false, maximized, caption, sameMonitor);
        Assert.Equal(expected, FullscreenClassifier.IsFullscreen(window, false));
    }
    [Fact] public void MaximizedWorkAreaDoesNotHideWidget()
    {
        var window = new WindowGeometry(new(0, 0, 1920, 1040), new(0, 0, 1920, 1080), false, false, true, false, true, false, true);
        Assert.False(FullscreenClassifier.IsFullscreen(window, true));
        Assert.False(FullscreenClassifier.IsFullscreen(window with { Minimized = true }, true));
    }
    [Fact] public void GlobalHideAndNegativeMonitorCoordinatesWork()
    {
        var rect = new Rectangle(-1920, 0, 0, 1080); var window = new WindowGeometry(rect, rect, false, false, true, false, false, false, false);
        Assert.True(FullscreenClassifier.IsFullscreen(window, true));
    }
    [Fact] public void DebounceRequiresTwoSamples()
    {
        var debounce = new VisibilityDebouncer(); Assert.False(debounce.Update(true)); Assert.True(debounce.Update(true));
        Assert.True(debounce.Update(false)); Assert.False(debounce.Update(false));
    }
}
