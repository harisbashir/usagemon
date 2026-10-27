using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows;
using UsageMon.App;
using UsageMon.Core;

namespace UsageMon.WindowsTests;

public class NativeTests
{
    [Fact]
    public void SystemMetricsProduceRealBoundedSamples()
    {
        var metrics = new WindowsMetrics();
        metrics.Sample();
        Thread.Sleep(150);
        var (cpu, ram) = metrics.Sample();
        Assert.NotNull(cpu);
        Assert.NotNull(ram);
        Assert.InRange(cpu.Value, 0, 100);
        Assert.InRange(ram.Value, 0, 100);
    }

    [Fact]
    public void NativePlacementRecoversOffscreenPositionAndKeepsTopmostStyle()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new Window { Width = 650, Height = 48, WindowStyle = WindowStyle.None, Topmost = true, ShowInTaskbar = false };
                try
                {
                    window.Show();
                    var placement = new WindowPlacement(window);
                    placement.Restore(new WidgetSettings { X = 300000, Y = 300000 });
                    var restored = placement.Position;
                    Assert.InRange(restored.X, -100000, 100000);
                    Assert.InRange(restored.Y, -100000, 100000);
                    placement.Snap();
                    placement.KeepOnTop();
                    var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                    Assert.NotEqual(0, GetWindowLong(handle, -20) & 0x8); // WS_EX_TOPMOST
                }
                finally { window.Close(); }
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "WPF placement did not finish.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(nint window, int index);
}
