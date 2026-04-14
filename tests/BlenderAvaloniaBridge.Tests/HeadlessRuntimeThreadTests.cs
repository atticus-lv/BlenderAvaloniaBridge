using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using BlenderAvaloniaBridge.Runtime;
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
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
            return window.GetLastRenderedFrame();
        });

        Assert.NotNull(bitmap);
        Assert.Equal(32, bitmap!.PixelSize.Width);
        Assert.Equal(24, bitmap.PixelSize.Height);
    }

    [Fact]
    public async Task HeadlessRuntimeThread_AsyncContinuationsStayOnRuntimeThread()
    {
        var runtimeThread = HeadlessRuntimeThread.Shared;

        var asyncProbe = await runtimeThread.InvokeAsync(async () =>
        {
            var beforeAwaitThreadId = Environment.CurrentManagedThreadId;
            await Task.Yield();
            var afterAwaitThreadId = Environment.CurrentManagedThreadId;
            return (beforeAwaitThreadId, afterAwaitThreadId);
        });

        var (beforeAwaitThreadId, afterAwaitThreadId) = await asyncProbe.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(beforeAwaitThreadId, afterAwaitThreadId);
    }
}
