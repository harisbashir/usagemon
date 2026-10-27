using UsageMon.Core;

namespace UsageMon.Tests;

public class WidgetPlacementTests
{
    [Fact]
    public void RemovedMonitorPositionIsClampedIntoVisibleMonitor()
    {
        var result = WidgetPlacement.Clamp(3000, -100, 600, 44, new ScreenRect(0, 0, 1920, 1080));
        Assert.Equal(1320, result.X);
        Assert.Equal(0, result.Y);
    }

    [Theory]
    [InlineData(0, 0, 1920, 1040, 1320, 1036)]
    [InlineData(0, 40, 1920, 1080, 1320, 0)]
    [InlineData(40, 0, 1920, 1080, 0, 1036)]
    [InlineData(0, 0, 1880, 1080, 1320, 1036)]
    public void SnapsToEachTaskbarEdge(double l, double t, double r, double b, double x, double y)
    {
        var result = WidgetPlacement.OnTaskbar(600, 44, new ScreenRect(0, 0, 1920, 1080), new ScreenRect(l, t, r, b));
        Assert.Equal(x, result.X);
        Assert.Equal(y, result.Y);
    }

    [Fact]
    public void NonFiniteSavedCoordinatesAreRecovered()
    {
        var result = WidgetPlacement.Clamp(double.NaN, double.PositiveInfinity, 600, 44, new ScreenRect(-1920, 0, 0, 1080));
        Assert.Equal(-1920, result.X);
        Assert.Equal(0, result.Y);
    }
}
