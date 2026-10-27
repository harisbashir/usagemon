namespace UsageMon.Core;

public readonly record struct ScreenRect(double Left, double Top, double Right, double Bottom);
public readonly record struct ScreenPoint(double X, double Y);
public readonly record struct ScreenSize(double Width, double Height);

public static class WidgetPlacement
{
    public static ScreenSize FitSize(double width, double height, double physicalScreenWidth, double dpiScale)
    {
        var scale = Math.Min(1, physicalScreenWidth / (width * dpiScale));
        return new(width * scale, height * scale);
    }

    public static ScreenPoint Clamp(double x, double y, double width, double height, ScreenRect screen) => new(
        Math.Clamp(double.IsFinite(x) ? x : screen.Left, screen.Left, Math.Max(screen.Left, screen.Right - width)),
        Math.Clamp(double.IsFinite(y) ? y : screen.Top, screen.Top, Math.Max(screen.Top, screen.Bottom - height)));

    public static ScreenPoint OnTaskbar(double width, double height, ScreenRect screen, ScreenRect workArea)
    {
        var x = screen.Right - width;
        var y = screen.Bottom - height;
        if (workArea.Bottom < screen.Bottom) y = workArea.Bottom;
        else if (workArea.Top > screen.Top) y = screen.Top;
        else if (workArea.Left > screen.Left) x = screen.Left;
        // A right taskbar already intersects the default bottom-right position.
        return Clamp(x, y, width, height, screen);
    }
}
