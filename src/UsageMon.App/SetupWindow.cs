using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UsageMon.Core;

namespace UsageMon.App;

public sealed class SetupWindow : Window
{
    public SetupWindow()
    {
        Title = "UsageMon · Connection setup";
        Width = 660; Height = 580; MinWidth = 520; MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(21, 27, 36));
        Foreground = Brushes.WhiteSmoke;
        FontFamily = new FontFamily("Segoe UI");
        var stack = new StackPanel { Margin = new Thickness(24) };
        Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Heading(stack, "Connect your subscriptions");
        Text(stack, "UsageMon reads usage from your installed CLI tools. It never needs an API key or sends chat requests.");
        Heading(stack, "Codex");
        Text(stack, "Install the Codex CLI, then run codex login in a terminal and sign in with your ChatGPT account. Restart UsageMon after installation so it sees your updated PATH. Limits refresh every minute; failures retry automatically.");
        Heading(stack, "Claude");
        Text(stack, "Sign in to a current Claude Code with your Claude subscription, then click below. UsageMon installs its built-in helper and adds the status line setting for you. Existing settings are backed up; custom status lines are preserved.");
        var configure = new Button { Content = "Set up Claude automatically", Padding = new Thickness(14, 9, 14, 9), HorizontalAlignment = HorizontalAlignment.Left };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 12), LineHeight = 21 };
        configure.Click += async (_, _) =>
        {
            configure.IsEnabled = false;
            try
            {
                var result = await Task.Run(() => ClaudeSetup.Configure(ClaudeBridge.SettingsPath, WidgetViewModel.DataDirectory, ClaudeBridge.Content));
                if (result.Configured)
                {
                    status.Foreground = Brushes.MediumAquamarine;
                    status.Text = "Claude setup is ready. Restart Claude Code and work normally; the widget will update when Claude reports usage." +
                        (result.BackupPath is null ? "" : $"\nYour previous settings were backed up to:\n{result.BackupPath}");
                }
                else
                {
                    await Task.Run(() => ClaudeSetup.InstallBridge(WidgetViewModel.DataDirectory, ClaudeBridge.Content));
                    status.Foreground = Brushes.Goldenrod;
                    status.Text = "You already have a custom status line, so your settings were left unchanged. The helper is installed. Use the manual integration instructions below to keep your current status line.";
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            {
                status.Foreground = Brushes.Salmon;
                status.Text = error is JsonException
                    ? "Your Claude settings could not be read as JSON and were left unchanged. Fix the settings file, then try again."
                    : "Setup could not finish. Check write access to your Claude settings and local UsageMon folder, then try again.";
            }
            finally { configure.IsEnabled = true; }
        };
        stack.Children.Add(configure);
        stack.Children.Add(status);
        Text(stack, "Claude readings update when Claude Code reports status and are marked stale after five minutes without a report. You only need the UsageMon EXE; the helper is built in.");

        var manual = new StackPanel();
        var command = ClaudeSetup.Command(WidgetViewModel.DataDirectory);
        var config = JsonSerializer.Serialize(new { statusLine = new { type = "command", command } }, new JsonSerializerOptions { WriteIndented = true });
        Text(manual, $"Manual configuration for {ClaudeBridge.SettingsPath}. Merge the statusLine property with your other settings. If you already have a custom status line, forward its input to the helper; see the documentation.");
        var box = new TextBox
        {
            Text = config, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas"), FontSize = 12, Margin = new Thickness(0, 4, 0, 8),
            Padding = new Thickness(12), Background = new SolidColorBrush(Color.FromRgb(32, 42, 55)), Foreground = Brushes.WhiteSmoke
        };
        manual.Children.Add(box);
        var copy = new Button { Content = "Install helper and copy configuration", Padding = new Thickness(12, 6, 12, 6), HorizontalAlignment = HorizontalAlignment.Left };
        copy.Click += (_, _) =>
        {
            try
            {
                ClaudeSetup.InstallBridge(WidgetViewModel.DataDirectory, ClaudeBridge.Content);
                Clipboard.SetText(config);
                copy.Content = "Copied; helper installed";
            }
            catch (System.Runtime.InteropServices.COMException) { MessageBox.Show(this, "Clipboard is busy. Select and copy the text above.", "UsageMon"); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
            { MessageBox.Show(this, "The helper could not be installed. Check access to your local UsageMon folder.", "UsageMon"); }
        };
        manual.Children.Add(copy);
        stack.Children.Add(new Expander { Header = "Manual setup / existing custom status line", Content = manual, Margin = new Thickness(0, 8, 0, 18) });
        var docs = new Button { Content = "Open setup documentation", Padding = new Thickness(12, 6, 12, 6), HorizontalAlignment = HorizontalAlignment.Left };
        docs.Click += (_, _) => OpenDocs();
        stack.Children.Add(docs);
    }

    public static void OpenDocs()
    {
        try { Process.Start(new ProcessStartInfo("https://github.com/harisbashir/usagemon#connecting-your-accounts") { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception) { MessageBox.Show("Open https://github.com/harisbashir/usagemon in your browser.", "UsageMon"); }
    }

    private static void Heading(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 8) });
    private static void Text(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, LineHeight = 21, Margin = new Thickness(0, 0, 0, 12) });
}
