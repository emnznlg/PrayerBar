# Verification

## Executed in this workspace

- Windows x64, local .NET SDK 10.0.401.
- Release WPF application build: succeeded, 0 warnings, 0 errors.
- xUnit: 35 passed, 0 failed, 0 skipped. TRX: `artifacts/test-results/results.trx`.
- Self-contained `win-x64`, untrimmed single-file publish: succeeded.
- Packaged executable launched and wrote its startup log.
- Live AlAdhan default-location synchronization created a cache with actual provider data.
- Interactive UI inspection was stopped by the user with Escape. Remaining UI/hardware checks below have not been claimed as verified.
- The compact redesign uses a 228 × 34 DIP single-row widget without its decorative icon or source label. At the user's request, interactive UI testing of this revision is left to the user.

Tests cover next prayer and exact boundaries, sunrise exclusion, after-Isha rollover, midnight/location timezone, countdown clock correction, DST gaps and repeated hours, settings serialization and validation, request encoding, parser invalid dates/times/timezones, cache persistence/offline recovery/expiration/location isolation, notification eligibility/deduplication, fullscreen geometry/maximized/shell/own-window/multi-monitor rules, and debounce.

## Remaining release checks

These need real Windows interaction/hardware and must not be inferred from the unit tests:

- Inspect widget typography and light/dark appearance at 100%, 150%, 200% DPI; drag between differently scaled monitors and restart.
- Verify settings save/cancel, schedule, all tray menu commands and single-instance activation.
- Check F11/video fullscreen, borderless/exclusive games, media players, and PowerPoint. Test both monitor-local and global hide; confirm ordinary maximize stays visible and restoration does not flicker.
- Disconnect a monitor containing the widget; confirm it returns within another monitor's work area. Repeat after sleep/resume.
- Test actual sleep/resume, manually changed clock and computer timezone. The configured location timezone should remain authoritative.
- Restart Explorer and verify the tray icon returns.
- Enable startup from a permanent EXE location, sign out/in, confirm one instance; disable startup and verify registry removal. This setting is not enabled automatically during testing.
- Test notifications with Windows Do Not Disturb on/off, fullscreen suppression, and duplicate prevention across restarts near prayer boundaries.
- Test offline launch with a current cache, with expired dates, and without a cache.
- Exit through the tray and check the process and icon disappear. Verify installation on a clean Windows 11 x64 machine without .NET installed.

There is no installer, code signature, auto-updater, official Diyanet schedule ingestion, GPS location, or notification audio. The portable ZIP is the current distribution format.
