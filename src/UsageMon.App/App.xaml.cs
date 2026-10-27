using System.Windows;

namespace UsageMon.App;

public partial class App : Application
{
    private Mutex? singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        singleInstance = new Mutex(true, @"Local\UsageMon.Widget", out var first);
        if (!first)
        {
            MessageBox.Show("UsageMon is already running. Use its system tray icon to show the widget.", "UsageMon");
            Shutdown();
            return;
        }
        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        singleInstance?.Dispose();
        base.OnExit(e);
    }
}
