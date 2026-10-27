using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using UsageMon.Core;

namespace UsageMon.App;

/// <summary>Use physical pixels for monitor placement; WPF handles each monitor's UI scale.</summary>
public sealed class WindowPlacement(Window window)
{
    private nint Handle => new WindowInteropHelper(window).Handle;

    public ScreenPoint Position
    {
        get { GetWindowRect(Handle, out var rect); return new(rect.Left, rect.Top); }
    }

    public void Restore(WidgetSettings settings)
    {
        if (settings.X is { } x && settings.Y is { } y && double.IsFinite(x) && double.IsFinite(y)
            && x is >= int.MinValue and <= int.MaxValue && y is >= int.MinValue and <= int.MaxValue)
            Move(new(x, y));
        else Snap();
        EnsureVisible();
    }

    public void EnsureVisible()
    {
        FitToMonitor();
        var (rect, monitor) = Bounds();
        var point = WidgetPlacement.Clamp(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, Convert(monitor.Monitor));
        Move(point);
    }

    public void Snap()
    {
        FitToMonitor();
        var (rect, monitor) = Bounds();
        Move(WidgetPlacement.OnTaskbar(rect.Right - rect.Left, rect.Bottom - rect.Top, Convert(monitor.Monitor), Convert(monitor.Work)));
    }

    public void KeepOnTop() => SetWindowPos(Handle, new nint(-1), 0, 0, 0, 0, 0x0013); // NOMOVE | NOSIZE | NOACTIVATE

    private void FitToMonitor()
    {
        var (_, monitor) = Bounds();
        var dpi = GetDpiForWindow(Handle);
        var size = WidgetPlacement.FitSize(650, 48, monitor.Monitor.Right - monitor.Monitor.Left, (dpi == 0 ? 96 : dpi) / 96.0);
        window.Width = size.Width;
        window.Height = size.Height;
        window.UpdateLayout();
    }

    private (Rect Rect, MonitorInfo Monitor) Bounds()
    {
        GetWindowRect(Handle, out var rect);
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        GetMonitorInfo(MonitorFromWindow(Handle, 2), ref info);
        return (rect, info);
    }

    private void Move(ScreenPoint point) => SetWindowPos(Handle, new nint(-1), (int)Math.Round(point.X), (int)Math.Round(point.Y), 0, 0, 0x0011);
    private static ScreenRect Convert(Rect rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public uint Size; public Rect Monitor, Work; public uint Flags; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);
}
