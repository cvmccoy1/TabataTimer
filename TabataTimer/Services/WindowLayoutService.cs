using System.Windows;
using TabataTimer.Models;

namespace TabataTimer.Services;

public interface IWindowLayoutService
{
    void ApplyLayoutTo(Window window, WindowLayout layout, double minWidth, double minHeight);
    WindowLayout CaptureLayoutFrom(Window window);
}

public class WindowLayoutService : IWindowLayoutService
{
    public void ApplyLayoutTo(Window window, WindowLayout layout, double minWidth, double minHeight)
    {
        if (!double.IsNaN(layout.Left) && !double.IsNaN(layout.Top))
        {
            window.Left = layout.Left;
            window.Top = layout.Top;
        }
        window.Width = Math.Max(layout.Width, minWidth);
        window.Height = Math.Max(layout.Height, minHeight);
    }

    public WindowLayout CaptureLayoutFrom(Window window)
        => new()
        {
            Left = window.Left,
            Top = window.Top,
            Width = window.Width,
            Height = window.Height
        };
}
