using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using PrayerBar.App.Infrastructure;
using PrayerBar.App.Services;
using PrayerBar.App.ViewModels;
using PrayerBar.App.Views;
using PrayerBar.Core;

namespace PrayerBar.App;

public partial class App : System.Windows.Application
{
    private Mutex? mutex;
    private EventWaitHandle? activation;
    private RegisteredWaitHandle? activationRegistration;
    private bool ownsMutex;
    private readonly CancellationTokenSource lifetime = new();
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(18) };
    private readonly JsonStore store = new(Log.DirectoryPath, Log.Write);
    private readonly WidgetViewModel model = new();
    private readonly VisibilityDebouncer debounce = new();
    private Settings settings = new();
    private CacheSnapshot cache = new("", null, []);
    private PrayerRepository repository = null!;
    private WidgetWindow widget = null!;
    private TrayService? tray;
    private ScheduleWindow? schedule;
    private SettingsWindow? settingsWindow;
    private DispatcherTimer? clock, fullscreenTimer;
    private Task? syncLoop;
    private Task? currentRefresh;
    private bool manualHidden, fullscreen, overrideFullscreen, syncing, exiting, noticeBusy;
    private HashSet<string> delivered = [];
    private DateOnly observedDate;
    private string? syncError;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        mutex = new Mutex(true, @"Local\PrayerBar." + Environment.UserName, out ownsMutex);
        activation = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\PrayerBar.Activate." + Environment.UserName);
        if (!ownsMutex) { activation.Set(); Shutdown(); return; }
        DispatcherUnhandledException += (_, args) => { Log.Write("Unexpected UI error.", args.Exception); args.Handled = true; tray?.Notify("PrayerBar encountered an error", "See the log in %LOCALAPPDATA%\\PrayerBar. Restart if the problem continues."); };
        try
        {
            settings = await store.ReadAsync<Settings>("settings.json") ?? new();
            try { settings.Validate(); } catch (Exception ex) { Log.Write("Invalid settings; restoring defaults.", ex); settings = new(); }
            delivered = await store.ReadAsync<HashSet<string>>("notifications.json") ?? [];
            repository = new(new AlAdhanProvider(http), store, Log.Write);
            cache = await repository.LoadAsync(settings, lifetime.Token);
            widget = new() { DataContext = model };
            MainWindow = widget;
            widget.ScheduleRequested += ShowSchedule;
            widget.MenuRequested += () => tray?.OpenMenu();
            widget.ToggleRequested += ToggleWidget;
            widget.PositionChanged += (x, y) => Run(async () => { settings.X = x; settings.Y = y; await store.WriteAsync("settings.json", settings, lifetime.Token); });
            tray = new(ToggleWidget, ShowSchedule, ShowSettings,
                () => Run(async () => { var draft = settings.Copy(); draft.AlwaysOnTop = !draft.AlwaysOnTop; await SaveSettings(draft, false); }),
                () => Run(async () => { var draft = settings.Copy(); draft.StartWithWindows = !draft.StartWithWindows; await SaveSettings(draft, false); }),
                () => Run(() => Refresh(true)), () => Run(ExitAsync));
            model.Apply(settings);
            widget.Show(); widget.Place(settings);
            manualHidden = settings.LaunchMinimized;
            ApplyVisibility();
            activationRegistration = ThreadPool.RegisterWaitForSingleObject(activation, (_, _) => Dispatcher.BeginInvoke(() =>
            {
                if (exiting) return;
                manualHidden = false; overrideFullscreen = fullscreen; ApplyVisibility(); ShowSettings();
            }), null, Timeout.Infinite, false);
            if (e.Args.Contains("--settings")) ShowSettings();
            clock = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Tick(), Dispatcher);
            fullscreenTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) => CheckFullscreen(), Dispatcher);
            SystemEvents.DisplaySettingsChanged += DisplayChanged;
            SystemEvents.PowerModeChanged += PowerChanged;
            SystemEvents.TimeChanged += TimeChanged;
            observedDate = PrayerEngine.Today(DateTimeOffset.UtcNow, settings.TimeZoneId);
            Tick();
            Log.Write("PrayerBar started.");
            syncLoop = SynchronizeLoop();
        }
        catch (Exception ex) { Log.Write("Startup failed.", ex); System.Windows.MessageBox.Show($"PrayerBar could not start: {ex.Message}", "PrayerBar"); await ExitAsync(); }
    }
    private async Task SynchronizeLoop()
    {
        try
        {
            await Refresh(false);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
            while (await timer.WaitForNextTickAsync(lifetime.Token)) await Refresh(false);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex) { Log.Write("Synchronization loop failed.", ex); syncError = "Synchronization failed; restart or refresh from tray."; Tick(); }
    }
    private Task Refresh(bool force)
    {
        if (exiting) return Task.CompletedTask;
        if (currentRefresh is { IsCompleted: false }) return currentRefresh;
        currentRefresh = RefreshCore(force);
        return currentRefresh;
    }
    private async Task RefreshCore(bool force)
    {
        var selection = settings.Copy();
        syncing = true; Tick();
        try
        {
            var result = await repository.SyncAsync(selection, cache, DateTimeOffset.UtcNow, force, lifetime.Token);
            if (settings.DataKey == selection.DataKey) { cache = result.Cache; syncError = result.Error; }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex) { Log.Write("Refresh failed.", ex); syncError = "Refresh failed; cached data may be available."; }
        finally { syncing = false; if (!exiting) Tick(); }
    }
    private void Tick()
    {
        if (exiting) return;
        var now = DateTimeOffset.UtcNow;
        var today = PrayerEngine.Today(now, settings.TimeZoneId);
        if (today != observedDate) { observedDate = today; Run(() => Refresh(false)); }
        // Never skip a missing today/tomorrow and falsely count down to a more distant cached date.
        var relevant = cache.Days.Where(d => d.Date == today || d.Date == today.AddDays(1)).ToList();
        var next = relevant.Any(d => d.Date == today) ? PrayerEngine.Next(relevant, now) : null;
        model.Prayer = next is null ? (settings.Language == DisplayLanguage.Turkish ? "Veri yok" : "Unavailable") : PrayerEngine.Name(next.Prayer, settings.Language);
        model.Time = next?.At.ToString("HH:mm") ?? "--:--";
        model.Countdown = next is null ? (settings.ShowSeconds ? "--:--:--" : "--:--") : PrayerEngine.Countdown(next, now, settings.ShowSeconds);
        model.Status = Status();
        schedule?.Update(settings, cache, model.Status);
        if (!noticeBusy) Run(NotifyDue);
    }
    private string Status()
    {
        if (syncing) return "Updating · AlAdhan";
        if (syncError is not null) return "Offline / cached · AlAdhan";
        if (cache.LastUpdated is null) return "Incomplete data · AlAdhan";
        if (DateTimeOffset.UtcNow - cache.LastUpdated >= TimeSpan.FromHours(12)) return "Stale cache · AlAdhan";
        return $"AlAdhan · {settings.Method}";
    }
    private async Task NotifyDue()
    {
        noticeBusy = true;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var notices = NotificationEngine.Due(cache.Days, settings, now, delivered).ToList();
            foreach (var notice in notices)
            {
                // Mark suppressed notices consumed so leaving fullscreen does not interrupt the user later.
                delivered.Add(notice.Key);
                await store.WriteAsync("notifications.json", delivered, lifetime.Token);
                if (settings.SuppressFullscreenNotifications && Native.Fullscreen(widget.Handle, true)) continue;
                var name = PrayerEngine.Name(notice.Event.Prayer, settings.Language);
                tray?.Notify(name, notice.Advance ? $"{name} in {settings.ReminderMinutes} minutes · {settings.City}" : $"{name} begins now · {settings.City} · AlAdhan {settings.Method}");
            }
            if (delivered.Count > 1500)
            {
                var cutoff = now.AddDays(-30).ToUnixTimeSeconds();
                delivered.RemoveWhere(key => { var pieces = key.Split('|'); return pieces.Length >= 3 && long.TryParse(pieces[^2], out var at) && at < cutoff; });
                await store.WriteAsync("notifications.json", delivered, lifetime.Token);
            }
        }
        finally { noticeBusy = false; }
    }
    private void CheckFullscreen()
    {
        if (exiting) return;
        try
        {
            var detected = settings.AutoHideFullscreen && Native.Fullscreen(widget.Handle, settings.GlobalFullscreenHide);
            var newState = debounce.Update(detected);
            if (!newState) overrideFullscreen = false;
            if (fullscreen != newState) { fullscreen = newState; ApplyVisibility(); }
        }
        catch (Exception ex) { Log.Write("Fullscreen detection failed.", ex); }
    }
    private void ToggleWidget()
    {
        if (widget.IsVisible) { manualHidden = true; overrideFullscreen = false; }
        else { manualHidden = false; overrideFullscreen = fullscreen; }
        ApplyVisibility();
    }
    private void ApplyVisibility()
    {
        var show = !manualHidden && (!fullscreen || overrideFullscreen);
        if (show && !widget.IsVisible) { widget.KeepOnScreen(); widget.Show(); }
        if (!show && widget.IsVisible) widget.Hide();
        tray?.Update(show, settings.AlwaysOnTop, settings.StartWithWindows);
    }
    private void ShowSchedule()
    {
        if (schedule is null) { schedule = new(); schedule.Closed += (_, _) => schedule = null; }
        schedule.Update(settings, cache, Status()); schedule.Show(); schedule.Activate();
    }
    private void ShowSettings()
    {
        if (settingsWindow is null) { settingsWindow = new(settings, SaveSettings); settingsWindow.Closed += (_, _) => settingsWindow = null; }
        settingsWindow.Show(); settingsWindow.Activate();
    }
    private async Task SaveSettings(Settings draft, bool reset)
    {
        draft.Validate();
        var previous = settings;
        if (reset) { draft.X = null; draft.Y = null; }
        else { draft.X = settings.X; draft.Y = settings.Y; }
        if (draft.StartWithWindows != previous.StartWithWindows) StartupService.Set(draft.StartWithWindows);
        try { await store.WriteAsync("settings.json", draft, lifetime.Token); }
        catch { if (draft.StartWithWindows != previous.StartWithWindows) StartupService.Set(previous.StartWithWindows); throw; }
        if (previous.DataKey != draft.DataKey) cache = new(draft.DataKey, null, []);
        settings = draft.Copy(); model.Apply(settings); widget.Place(settings, reset);
        ApplyVisibility(); Tick();
        if (previous.DataKey != settings.DataKey)
        {
            cache = await repository.LoadAsync(settings, lifetime.Token); syncError = null; Tick();
            // Finish the old request before starting the new selection, without applying old results.
            if (currentRefresh is { IsCompleted: false }) await currentRefresh;
            await Refresh(false);
        }
    }
    private void DisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() => widget.KeepOnScreen());
    private void PowerChanged(object sender, PowerModeChangedEventArgs e) { if (e.Mode == PowerModes.Resume) Dispatcher.BeginInvoke(() => { Tick(); Run(() => Refresh(false)); }); }
    private void TimeChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() => { Tick(); Run(() => Refresh(false)); });
    private async void Run(Func<Task> action)
    {
        try { await action(); }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex) { Log.Write("Operation failed.", ex); tray?.Notify("PrayerBar", ex.Message); }
    }
    private async Task ExitAsync()
    {
        if (exiting) return;
        exiting = true;
        clock?.Stop(); fullscreenTimer?.Stop(); lifetime.Cancel();
        activationRegistration?.Unregister(null);
        SystemEvents.DisplaySettingsChanged -= DisplayChanged; SystemEvents.PowerModeChanged -= PowerChanged; SystemEvents.TimeChanged -= TimeChanged;
        if (currentRefresh is not null) await currentRefresh;
        if (syncLoop is not null) await syncLoop;
        tray?.Dispose(); tray = null;
        settingsWindow?.Close(); schedule?.Close(); widget?.Exit();
        Log.Write("PrayerBar stopped."); Shutdown();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose(); http.Dispose(); lifetime.Dispose();
        activation?.Dispose();
        if (ownsMutex) mutex?.ReleaseMutex(); mutex?.Dispose();
        base.OnExit(e);
    }
}
