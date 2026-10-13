using PrayerBar.Core;
using Xunit;

namespace PrayerBar.Tests;

public sealed class CacheTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "PrayerBarTests-" + Guid.NewGuid());
    private readonly Settings settings = new();
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.FromHours(-8));
    private JsonStore Store => new(directory, (_, _) => { });
    private sealed class Provider(bool fail = false) : IPrayerProvider
    {
        public int Calls { get; private set; }
        public Task<PrayerDay> FetchAsync(Settings settings, DateOnly date, CancellationToken ct)
        {
            Calls++; if (fail) throw new HttpRequestException("offline"); return Task.FromResult(EngineTests.Day(date));
        }
    }
    [Fact] public async Task SyncPersistsRealProviderDataAndOfflineKeepsCache()
    {
        var provider = new Provider(); var repo = new PrayerRepository(provider, Store, (_, _) => { });
        var empty = await repo.LoadAsync(settings);
        var fresh = await repo.SyncAsync(settings, empty, Now, false, default); Assert.Equal(14, fresh.Cache.Days.Count); Assert.Equal(Now, fresh.Cache.LastUpdated);
        var loaded = await repo.LoadAsync(settings); Assert.Equal(14, loaded.Days.Count);
        var offline = new PrayerRepository(new Provider(true), Store, (_, _) => { });
        var result = await offline.SyncAsync(settings, loaded, Now.AddDays(1), true, default);
        Assert.True(result.Offline); Assert.NotNull(result.Error); Assert.Equal(13, result.Cache.Days.Count); Assert.NotNull(PrayerEngine.Next(result.Cache.Days, Now.AddDays(1)));
    }
    [Fact] public async Task FreshCacheDoesNotMakeNetworkRequests()
    {
        var provider = new Provider(); var repo = new PrayerRepository(provider, Store, (_, _) => { });
        var cache = (await repo.SyncAsync(settings, new(settings.DataKey, null, []), Now, false, default)).Cache;
        await repo.SyncAsync(settings, cache, Now.AddMinutes(1), false, default); Assert.Equal(14, provider.Calls);
        Assert.True(PrayerRepository.NeedsRefresh(cache, settings, Now.AddHours(12)));
        Assert.True(PrayerRepository.NeedsRefresh(cache, settings, Now.AddDays(-1)));
        Assert.True(PrayerRepository.NeedsRefresh(cache, settings, Now.AddDays(1)));
    }
    [Fact] public async Task CorruptJsonRecoversAndSettingsRoundTrip()
    {
        Directory.CreateDirectory(directory); await File.WriteAllTextAsync(Path.Combine(directory, "settings.json"), "{bad}");
        Assert.Null(await Store.ReadAsync<Settings>("settings.json"));
        await Store.WriteAsync("settings.json", settings); Assert.Equal(settings.DataKey, (await Store.ReadAsync<Settings>("settings.json"))!.DataKey);
    }
    [Fact] public async Task LocationChangeCannotReuseOldCache()
    {
        var cache = new CacheSnapshot(settings.DataKey, Now, [EngineTests.Day(new(2026, 1, 15))]);
        var changed = settings.Copy(); changed.City = "Toronto";
        var result = await new PrayerRepository(new Provider(true), Store, (_, _) => { }).SyncAsync(changed, cache, Now, false, default);
        Assert.Empty(result.Cache.Days); Assert.Null(result.Cache.LastUpdated);
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
