using System.Windows;
using PrayerBar.App.ViewModels;
using PrayerBar.Core;

namespace PrayerBar.App.Views;
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel model;
    private readonly Func<Settings, bool, Task> save;
    public SettingsWindow(Settings current, Func<Settings, bool, Task> save)
    {
        InitializeComponent(); model = new(current); DataContext = model; this.save = save;
    }
    private void Cancel(object sender, RoutedEventArgs e) => Close();
    private async void Save(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        try { model.Draft.Validate(); await save(model.Draft, model.ResetPosition); Close(); }
        catch (Exception ex) { Infrastructure.Log.Write("Settings could not be saved.", ex); Error.Text = ex.Message; }
        finally { SaveButton.IsEnabled = true; }
    }
}
