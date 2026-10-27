using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using UsageMon.Core;
using Forms = System.Windows.Forms;

namespace UsageMon.App;

public partial class MainWindow : Window
{
    private readonly WidgetViewModel model = new();
    private readonly WindowPlacement placement;
    private readonly Forms.NotifyIcon tray;
    private readonly string settingsPath = Path.Combine(WidgetViewModel.DataDirectory, "settings.json");
    private WidgetSettings settings;
    private bool exiting;
    private HwndSource? source;
    private SetupWindow? setup;
    private readonly DispatcherTimer topmostTimer = new() { Interval = TimeSpan.FromSeconds(5) };

    public MainWindow()
    {
        InitializeComponent();
        DataContext = model;
        settings = SettingsStore.Load(settingsPath);
        placement = new WindowPlacement(this);
        tray = new Forms.NotifyIcon
        {
            Text = "UsageMon · Claude / Codex / CPU / RAM",
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true
        };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowWidget);
        UpdateTrayMenu();
        ContextMenu = BuildMenu();
        Loaded += (_, _) =>
        {
            source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source?.AddHook(WindowMessage);
            placement.Restore(settings);
            model.Start();
            topmostTimer.Tick += (_, _) => { if (IsVisible) placement.KeepOnTop(); };
            topmostTimer.Start();
        };
        Closing += OnClosing;
        Closed += (_, _) => { source?.RemoveHook(WindowMessage); tray.Dispose(); };
        Application.Current.SessionEnding += OnSessionEnding;
    }

    private void ShowWidget()
    {
        Show();
        placement.EnsureVisible();
        placement.KeepOnTop();
    }

    private void DragWidget(object sender, MouseButtonEventArgs e)
    {
        if (settings.Locked || e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase) return;
        try { DragMove(); }
        catch (InvalidOperationException) { return; }
        placement.EnsureVisible();
        SaveSettings();
    }

    protected override void OnContextMenuOpening(ContextMenuEventArgs e)
    {
        ContextMenu = BuildMenu();
        ContextMenu.IsOpen = true;
        e.Handled = true;
        base.OnContextMenuOpening(e);
    }

    private void OpenMenu(object sender, RoutedEventArgs e)
    {
        var menu = BuildMenu();
        menu.PlacementTarget = (UIElement)sender;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        Add(menu, "Connection setup…", OpenSetup);
        Add(menu, "Refresh usage", model.Refresh);
        menu.Items.Add(new Separator());
        Add(menu, "Lock position", ToggleLock, settings.Locked);
        Add(menu, "Snap over taskbar", Snap);
        Add(menu, "Launch at sign-in", ToggleStartup, StartupEnabled());
        menu.Items.Add(new Separator());
        Add(menu, "Hide widget", Hide);
        Add(menu, "Exit UsageMon", () => _ = ExitAsync());
        return menu;
    }

    private static void Add(ContextMenu menu, string label, Action action, bool check = false)
    {
        var item = new MenuItem { Header = label, IsChecked = check };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }

    private void UpdateTrayMenu()
    {
        var previous = tray.ContextMenuStrip;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show widget", null, (_, _) => Dispatcher.Invoke(ShowWidget));
        menu.Items.Add("Connection setup…", null, (_, _) => Dispatcher.Invoke(OpenSetup));
        menu.Items.Add("Refresh usage", null, (_, _) => Dispatcher.Invoke(model.Refresh));
        menu.Items.Add("Lock position", null, (_, _) => Dispatcher.Invoke(ToggleLock));
        menu.Items.Add("Snap over taskbar", null, (_, _) => Dispatcher.Invoke(Snap));
        menu.Items.Add("Launch at sign-in", null, (_, _) => Dispatcher.Invoke(ToggleStartup));
        ((Forms.ToolStripMenuItem)menu.Items[3]).Checked = settings.Locked;
        ((Forms.ToolStripMenuItem)menu.Items[5]).Checked = StartupEnabled();
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Hide widget", null, (_, _) => Dispatcher.Invoke(Hide));
        menu.Items.Add("Exit UsageMon", null, (_, _) => Dispatcher.Invoke(() => _ = ExitAsync()));
        tray.ContextMenuStrip = menu;
        previous?.Dispose();
    }

    private void OpenSetup()
    {
        if (setup is not null) { setup.Activate(); return; }
        setup = new SetupWindow();
        setup.Closed += (_, _) => setup = null;
        setup.Show();
    }

    private void ToggleLock()
    {
        settings = settings with { Locked = !settings.Locked };
        SaveSettings();
        UpdateTrayMenu();
    }

    private void Snap()
    {
        Show();
        placement.Snap();
        SaveSettings();
    }

    private void SaveSettings()
    {
        var position = placement.Position;
        settings = settings with { X = position.X, Y = position.Y };
        try { SettingsStore.Save(settingsPath, settings); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { MessageBox.Show("UsageMon could not save its position. Check write access to %LOCALAPPDATA%\\UsageMon.", "UsageMon"); }
    }

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static bool StartupEnabled()
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue("UsageMon") is string; }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return false; }
    }

    private void ToggleStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (StartupEnabled()) key.DeleteValue("UsageMon", throwOnMissingValue: false);
            else key.SetValue("UsageMon", $"\"{Environment.ProcessPath}\"");
            UpdateTrayMenu();
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        { MessageBox.Show("Windows did not allow the sign-in startup setting to change.", "UsageMon"); }
    }

    private nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message is 0x007E or 0x02E0) // WM_DISPLAYCHANGE / WM_DPICHANGED
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(placement.EnsureVisible));
        return 0;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (exiting) return;
        e.Cancel = true;
        Hide();
    }

    private async void OnSessionEnding(object sender, SessionEndingCancelEventArgs e) => await ExitAsync();

    private async Task ExitAsync()
    {
        if (exiting) return;
        exiting = true;
        topmostTimer.Stop();
        SaveSettings();
        tray.Visible = false;
        await model.StopAsync();
        Application.Current.SessionEnding -= OnSessionEnding;
        setup?.Close();
        tray.Dispose();
        Application.Current.Shutdown();
    }
}
