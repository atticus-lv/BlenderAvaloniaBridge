using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using BlenderAvaloniaBridge.Bridge;
using Xunit;

namespace BlenderAvaloniaBridge.Tests;

public sealed class HeadlessRuntimeThreadTests
{
    [Fact]
    public async Task HeadlessRuntimeThread_CanRenderAFrame()
    {
        var runtimeThread = HeadlessRuntimeThread.Shared;

        var bitmap = await runtimeThread.InvokeAsync(() =>
        {
            var window = new Window
            {
                Width = 32,
                Height = 24,
                Background = Brushes.DarkSlateBlue,
                Content = new Border
                {
                    Width = 32,
                    Height = 24,
                    Background = Brushes.Goldenrod
                }
            };

            window.Show();
            return window.CaptureRenderedFrame();
        });

        Assert.NotNull(bitmap);
        Assert.Equal(32, bitmap!.PixelSize.Width);
        Assert.Equal(24, bitmap.PixelSize.Height);
    }
}
