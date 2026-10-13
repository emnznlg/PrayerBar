using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using PrayerBar.Core;

namespace PrayerBar.App.ViewModels;

public sealed class WidgetViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private string prayer = "PrayerBar", time = "--:--", countdown = "--:--:--", status = "Connecting to AlAdhan…";
    private Brush surface = new SolidColorBrush(Color.FromRgb(21, 28, 41)), text = Brushes.WhiteSmoke, muted = Brushes.LightSlateGray, accent = new SolidColorBrush(Color.FromRgb(232, 199, 127));
    public string Prayer { get => prayer; set => Set(ref prayer, value); }
    public string Time { get => time; set => Set(ref time, value); }
    public string Countdown { get => countdown; set => Set(ref countdown, value); }
    public string Status { get => status; set => Set(ref status, value); }
    public Brush Surface { get => surface; set => Set(ref surface, value); }
    public Brush Text { get => text; set => Set(ref text, value); }
    public Brush Muted { get => muted; set => Set(ref muted, value); }
    public Brush Accent { get => accent; set => Set(ref accent, value); }
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (Equals(field, value)) return; field = value; PropertyChanged?.Invoke(this, new(name)); }
    public void Apply(Settings settings)
    {
        var light = settings.Appearance == Appearance.Light;
        Surface = new SolidColorBrush(light ? Color.FromRgb(243, 245, 250) : Color.FromRgb(21, 28, 41));
        Text = light ? Brushes.MidnightBlue : Brushes.WhiteSmoke;
        Muted = light ? Brushes.SlateGray : Brushes.LightSlateGray;
        Accent = new SolidColorBrush(light ? Color.FromRgb(133, 99, 34) : Color.FromRgb(232, 199, 127));
    }
}
