using System.Drawing;
using System.Reflection;
using Forms = System.Windows.Forms;

namespace PrayerBar.App.Services;

internal sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly Icon image;
    private readonly Forms.ContextMenuStrip menu;
    private readonly Forms.ToolStripMenuItem top, startup, toggle;
    public TrayService(Action toggleWidget, Action schedule, Action settings, Action toggleTop, Action toggleStartup, Action refresh, Action exit)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PrayerBar.App.Resources.PrayerBar.ico") ?? throw new InvalidOperationException("Embedded tray icon is missing.");
        image = new Icon(stream);
        menu = new Forms.ContextMenuStrip();
        toggle = Item("Hide Widget", toggleWidget);
        Item("Today's Prayer Times", schedule);
        Item("Settings", settings);
        menu.Items.Add(new Forms.ToolStripSeparator());
        top = Item("Always on Top", toggleTop);
        startup = Item("Start with Windows", toggleStartup);
        Item("Refresh Prayer Times", refresh);
        menu.Items.Add(new Forms.ToolStripSeparator());
        Item("Exit", exit);
        // WinForms NotifyIcon registers TaskbarCreated and re-adds its icon after Explorer restarts.
        icon = new Forms.NotifyIcon { Icon = image, Text = "PrayerBar", ContextMenuStrip = menu, Visible = true };
        icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) toggleWidget(); };
    }
    private Forms.ToolStripMenuItem Item(string text, Action action)
    {
        var item = new Forms.ToolStripMenuItem(text);
        item.Click += (_, _) => action();
        menu.Items.Add(item);
        return item;
    }
    public void Update(bool shown, bool alwaysTop, bool start) { toggle.Text = shown ? "Hide Widget" : "Show Widget"; top.Checked = alwaysTop; startup.Checked = start; }
    public void Notify(string title, string message) { icon.BalloonTipTitle = title; icon.BalloonTipText = message; icon.ShowBalloonTip(6000); }
    public void OpenMenu() => menu.Show(Forms.Cursor.Position);
    public void Dispose() { icon.Visible = false; icon.Dispose(); menu.Dispose(); image.Dispose(); }
}
