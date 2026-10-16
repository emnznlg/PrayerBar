# PrayerBar

A native .NET 10 / WPF prayer-times widget for Windows 11 x64. It stays in the tray, keeps a clock-based countdown, and hides during fullscreen content on its monitor.

## Install and use

Run `PrayerBar-Setup-1.0.0-win-x64.exe` to install PrayerBar for the current Windows user. The installer places the app in `%LOCALAPPDATA%\Programs\PrayerBar`, adds a Start menu entry and uninstall entry, and can optionally add a desktop shortcut and launch at sign-in. It does not need administrator approval. The application itself is self-contained and needs no separate .NET runtime. It is currently unsigned.

- Left-click the widget: today's schedule, including sunrise.
- Drag the widget: reposition; desktop pixel coordinates persist across restarts.
- Right-click the widget: tray context menu.
- Tray left-click: show/hide; right-click: schedule, settings, topmost, startup, refresh, exit.
- **Ctrl+Alt+P**: show/hide globally. Showing while fullscreen overrides hiding until fullscreen ends. If another app has this hotkey, use the tray or open PrayerBar again.
- Opening PrayerBar a second time opens the existing instance's settings; `PrayerBar.exe --settings` opens settings on first launch too.
- Closing settings or the schedule leaves PrayerBar running. Choose **Exit** in the context menu to shut it down.

The compact 228 × 34 DIP widget shows a single row: prayer name, prayer time, and countdown. It has no decorative icon or source label; source and cache details are available in today's schedule. It defaults to dark, rounded, translucent styling. Settings include light appearance, opacity, size, language, seconds, topmost, launch minimized, position reset, fullscreen behavior, location and timezone, calculation method, Asr school, notifications, and 5/10/15-minute advance reminders.

## Prayer data and timezone behavior

Defaults: Vancouver, Canada; `America/Vancouver`; AlAdhan Diyanet method **13**; standard Asr school **0**. Turkish prayer names: İmsak, Güneş, Öğle, İkindi, Akşam, Yatsı. İmsak represents the provider's **Fajr**, not its separate optional Imsak field. Sunrise appears in the schedule but is excluded from the upcoming prayer rotation and notifications.

Source: [AlAdhan's documented API](https://aladhan.com/prayer-times-api), using `/v1/timingsByCity/{dd-MM-yyyy}` with `city`, `country`, `method`, `school`, `timezonestring`, and `iso8601=true`. Supported methods are Diyanet (13), ISNA (2), and Muslim World League (3). These are calculated AlAdhan times and may differ from official published Diyanet schedules. No production sample times or local invented fallback calculations are used.

Prayer timestamps carry explicit offsets; each second the countdown compares the next event to `DateTimeOffset.UtcNow`. The location timezone determines the date, independent of the computer timezone. Exact prayer boundaries advance immediately, and Isha rolls to tomorrow's Fajr. Changing the configured location/timezone/method loads a separate cache. Sleep/resume and clock changes trigger synchronization checks; UI ticks always recalculate from the actual clock.

First synchronization fetches 14 days. The synchronization loop checks every 15 minutes, refreshes after 12 hours or for missing dates, and refreshes on date change or resume. Requests have an 18-second timeout. An unavailable provider stops the batch after the first failed request. Manual **Refresh** forces a new batch. Current cached dates remain usable offline; errors/stale/incomplete data are labeled and last successful batch update is shown in the schedule. Missing current dates never masquerade as a distant next prayer. An incomplete first fetch can show the dates retrieved so far without inventing a successful batch timestamp.

The parser accepts offset-bearing ISO times and decorated `HH:mm` values. Dates, timezone rules, offset consistency and chronological order are validated. For a legacy `HH:mm` in a repeated DST hour, it explicitly chooses the earlier instant; nonexistent DST times are rejected. The current endpoint is requested in ISO mode to avoid this ambiguity.

## Fullscreen and notifications

Every 500 ms, foreground window geometry is compared with the physical monitor rectangle using Win32 and DWM. Two successive samples are required before visibility changes. Captioned maximized windows, shell windows, minimized windows, and PrayerBar windows are excluded. The default only hides when fullscreen occupies the widget's monitor; global hide is optional. Countdown, synchronization and tray access remain active while hidden.

Fullscreen detection is heuristic. Some games use unusual foreground/overlay windows; test your applications and disable auto-hide or use Ctrl+Alt+P if needed. Only the foreground window is inspected, so fullscreen content running behind another foreground window will not trigger hiding.

Notifications use the Windows tray balloon mechanism and follow Windows notification preferences. Notification keys are persisted to prevent duplicates across restarts. Missed events older than 90 seconds are not replayed after sleep. Fullscreen suppresses notifications on any monitor by default; suppressed events are consumed, not queued for later. No audio playback or intrusive popups are used.

## Local storage and startup

`%LOCALAPPDATA%\PrayerBar` contains `settings.json`, location-specific `cache-*.json`, `notifications.json` and rotating JSON-line logs (`PrayerBar.log`, up to 1 MB plus one prior file). JSON writes use a temporary file and atomic replacement. Unreadable/corrupt files are logged and recovered; corrupt schedule entries are discarded. Never delete a working cache just because a network request failed.

Startup is per-user `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `PrayerBar`, quoting the executable path and adding `--startup`. It needs no administrator rights, services, scheduled tasks, or Explorer changes. Startup registration is only available when running `PrayerBar.exe` directly. The tray uses .NET's native `System.Windows.Forms.NotifyIcon` with an embedded crescent icon and Explorer restart support. A named mutex enforces one instance in the current Windows session; an activation event routes a second launch to the existing app.

## Build and test

Install the **.NET 10 SDK** on Windows. Visual Studio must support .NET 10 and have the .NET desktop development workload. Open `PrayerBar.sln`, or run:

```powershell
dotnet build PrayerBar.sln -c Release
dotnet test PrayerBar.sln -c Release
dotnet publish src/PrayerBar.App/PrayerBar.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
```

`scripts/build-installer.ps1` runs tests, publishes, and creates `artifacts/installer/PrayerBar-Setup-1.0.0-win-x64.exe`. It uses Inno Setup 6, which can be installed with `winget install --id JRSoftware.InnoSetup --exact`. Close PrayerBar before updating an existing installation. The local SDK used during development is ignored by source control. Network access is needed for initial SDK/package/runtime restores and actual prayer synchronization; automated tests use fake providers and do not access the network or require a desktop.

WPF resources are compiled into the assembly; the tray icon is embedded. Single-file publishing is self-contained, untrimmed, and includes native libraries for self-extraction. PDBs are separate debug artifacts and are not required to run the ZIP. Do not enable trimming for WPF.

## Architecture

```text
PrayerBar.sln
src/PrayerBar.Core/       Settings, time parsing, selection, notifications,
                         cache repository, JSON storage, fullscreen classifier
src/PrayerBar.App/        WPF views/view models, application lifecycle,
                         native APIs, tray, logging and startup
tests/PrayerBar.Tests/    xUnit tests for core logic (no UI or network)
scripts/publish.ps1       Repeatable test/publish/package workflow
docs/VERIFICATION.md      Executed checks and remaining Windows hardware checks
```

Dependencies: WPF/WinForms from the .NET Windows desktop runtime; xUnit and Microsoft.NET.Test.Sdk only in the test project. Core logic has no external package dependency. HTTP, storage and orchestration use asynchronous operations; the per-second display timer is separate from synchronization. Shutdown cancels and waits for synchronization, removes system-event subscriptions, unregisters the hotkey, disposes tray resources and releases the mutex.

See [verification notes](docs/VERIFICATION.md) before treating the build as fully validated for your monitor/game setup.
