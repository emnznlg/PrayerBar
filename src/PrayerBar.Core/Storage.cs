using System.Text.Json;

namespace PrayerBar.Core;

public sealed class JsonStore(string directory, Action<string, Exception?> log)
{
    private readonly SemaphoreSlim gate = new(1);
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public async Task<T?> ReadAsync<T>(string file, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            var path = Path.Combine(directory, file);
            if (!File.Exists(path)) return default;
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, Options, ct);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            log($"Unable to read {file}; recovering with defaults or fresh data.", ex);
            return default;
        }
        finally { gate.Release(); }
    }
    public async Task WriteAsync<T>(string file, T value, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, file);
            var temp = path + ".tmp";
            await using (var stream = File.Create(temp)) await JsonSerializer.SerializeAsync(stream, value, Options, ct);
            File.Move(temp, path, true);
        }
        finally { gate.Release(); }
    }
}

public interface IPrayerProvider
{
    Task<PrayerDay> FetchAsync(Settings settings, DateOnly date, CancellationToken ct);
}

public sealed class AlAdhanProvider(HttpClient client) : IPrayerProvider
{
    public static Uri RequestUri(Settings settings, DateOnly date) => new($"https://api.aladhan.com/v1/timingsByCity/{date:dd-MM-yyyy}?city={Uri.EscapeDataString(settings.City.Trim())}&country={Uri.EscapeDataString(settings.Country.Trim())}&method={(int)settings.Method}&school={(int)settings.School}&timezonestring={Uri.EscapeDataString(settings.TimeZoneId)}&iso8601=true");
    public async Task<PrayerDay> FetchAsync(Settings settings, DateOnly date, CancellationToken ct)
    {
        using var response = await client.GetAsync(RequestUri(settings, date), ct);
        response.EnsureSuccessStatusCode();
        return AlAdhanParser.Parse(await response.Content.ReadAsStringAsync(ct), date, settings.TimeZoneId);
    }
}

public sealed record SyncResult(CacheSnapshot Cache, string? Error, bool Offline);

public sealed class PrayerRepository(IPrayerProvider provider, JsonStore store, Action<string, Exception?> log)
{
    private readonly SemaphoreSlim syncGate = new(1);
    public static bool NeedsRefresh(CacheSnapshot? cache, Settings settings, DateTimeOffset now)
    {
        var today = PrayerEngine.Today(now, settings.TimeZoneId);
        return cache is null || cache.DataKey != settings.DataKey || cache.LastUpdated is null || now - cache.LastUpdated >= TimeSpan.FromHours(12)
            || cache.LastUpdated > now || Enumerable.Range(0, 14).Any(i => !cache.Days.Any(d => d.Date == today.AddDays(i)));
    }
    public async Task<CacheSnapshot> LoadAsync(Settings settings, CancellationToken ct = default)
    {
        var cache = await store.ReadAsync<CacheSnapshot>(CacheFile(settings), ct);
        if (cache is null || cache.DataKey != settings.DataKey || cache.Days is null) return new(settings.DataKey, null, []);
        var valid = cache.Days.Where(d => d is not null && ValidDay(d, settings)).DistinctBy(d => d.Date).ToList();
        if (valid.Count != cache.Days.Count) log("Corrupt cache entries were discarded.", null);
        return cache with { Days = valid };
    }
    private static bool ValidDay(PrayerDay day, Settings settings)
    {
        if (day.Events is null || day.Events.Count != 6 || day.Events.Any(e => e is null) || day.Events.Select(e => e.Prayer).Distinct().Count() != 6) return false;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
        return day.Events.All(e => Enum.IsDefined(e.Prayer) && DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(e.At, zone).DateTime) == day.Date)
            && day.Events.OrderBy(e => e.Prayer).Zip(day.Events.OrderBy(e => e.Prayer).Skip(1)).All(p => p.First.At < p.Second.At);
    }
    public async Task<SyncResult> SyncAsync(Settings settings, CacheSnapshot cache, DateTimeOffset now, bool force, CancellationToken ct)
    {
        await syncGate.WaitAsync(ct);
        try
        {
            if (cache.DataKey != settings.DataKey) cache = new(settings.DataKey, null, []);
            if (!force && !NeedsRefresh(cache, settings, now)) return new(cache, null, false);
            var today = PrayerEngine.Today(now, settings.TimeZoneId);
            var days = cache.Days.Where(d => d.Date >= today && d.Date <= today.AddDays(20)).ToDictionary(d => d.Date);
            string? error = null;
            var successes = 0;
            for (var i = 0; i < 14; i++)
            {
                ct.ThrowIfCancellationRequested();
                var date = today.AddDays(i);
                try { days[date] = await provider.FetchAsync(settings, date, ct); successes++; }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or FormatException or KeyNotFoundException or InvalidOperationException or TimeZoneNotFoundException)
                {
                    ct.ThrowIfCancellationRequested();
                    error = "Refresh failed. Using available cached dates.";
                    log($"Prayer fetch failed for {date}.", ex);
                    break; // Do not hammer an unavailable provider.
                }
            }
            var result = new CacheSnapshot(settings.DataKey, successes == 14 ? now : cache.LastUpdated, days.Values.OrderBy(d => d.Date).ToList());
            if (successes > 0) await store.WriteAsync(CacheFile(settings), result, ct);
            return new(result, error, error is not null);
        }
        finally { syncGate.Release(); }
    }
    private static string CacheFile(Settings settings) => "cache-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(settings.DataKey)))[..20] + ".json";
}
