using PrayerBar.Core;

namespace PrayerBar.App.ViewModels;

public sealed class SettingsViewModel(Settings settings)
{
    public Settings Draft { get; } = settings.Copy();
    public DisplayLanguage[] Languages { get; } = Enum.GetValues<DisplayLanguage>();
    public CalculationMethod[] Methods { get; } = Enum.GetValues<CalculationMethod>();
    public AsrSchool[] Schools { get; } = Enum.GetValues<AsrSchool>();
    public Appearance[] Appearances { get; } = Enum.GetValues<Appearance>();
    public int[] Reminders { get; } = [0, 5, 10, 15];
    public bool ResetPosition { get; set; }
}
