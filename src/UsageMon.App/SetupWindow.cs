using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
        Text(stack, "Install the Codex CLI, then run codex login in a terminal and sign in with your ChatGPT account. Restart UsageMon after installation so it sees your updated PATH. Limits refresh every minute; failures retry with backoff up to five minutes.");
        Heading(stack, "Claude");
        Text(stack, "Use a current Claude Code version signed in with your Claude subscription. Add the statusLine setting below to %USERPROFILE%\\.claude\\settings.json, merging it with your other settings. Usage updates as Claude Code reports status; readings older than five minutes are marked stale.");
        var script = Path.Combine(AppContext.BaseDirectory, "scripts", "claude-statusline.ps1");
        var command = $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{script}\"";
        var config = System.Text.Json.JsonSerializer.Serialize(new { statusLine = new { type = "command", command } }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        var box = new TextBox
        {
            Text = config, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas"), FontSize = 12, Margin = new Thickness(0, 10, 0, 8),
            Padding = new Thickness(12), Background = new SolidColorBrush(Color.FromRgb(32, 42, 55)), Foreground = Brushes.WhiteSmoke
        };
        stack.Children.Add(box);
        var copy = new Button { Content = "Copy Claude configuration", Padding = new Thickness(12, 6, 12, 6), HorizontalAlignment = HorizontalAlignment.Left };
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(config); copy.Content = "Copied"; }
            catch (System.Runtime.InteropServices.COMException) { MessageBox.Show(this, "Clipboard is busy. Select and copy the text above.", "UsageMon"); }
        };
        stack.Children.Add(copy);
        Text(stack, "Already have a status line? Keep it. Call the bridge with the same stdin JSON from your existing script; see the README for an example. UsageMon never edits your Claude settings automatically. Keep the extracted app in a permanent folder so this path remains valid.");
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
