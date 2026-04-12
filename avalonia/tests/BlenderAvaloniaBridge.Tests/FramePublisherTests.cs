using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using BlenderAvaloniaBridge.Bridge;
using Xunit;

namespace BlenderAvaloniaBridge.Tests;

public sealed class FramePublisherTests
{
    [Fact]
    public async Task ExtractBgraBytes_ReturnsExpectedByteLength()
    {
        var runtimeThread = HeadlessRuntimeThread.Shared;
        var frame = await runtimeThread.InvokeAsync(() =>
        {
            var window = new Window
            {
                Width = 40,
                Height = 30,
                Background = Brushes.CornflowerBlue,
                Content = new Border
                {
                    Width = 40,
                    Height = 30,
                    Background = Brushes.Crimson
                }
            };

            window.Show();
            var bitmap = window.CaptureRenderedFrame();
            Assert.NotNull(bitmap);
            return FramePublisher.ExtractFrame(bitmap!, 11);
        });

        Assert.Equal("frame", frame.Header.Type);
        Assert.Equal(11, frame.Header.Seq);
        Assert.Equal(40, frame.Header.Width);
        Assert.Equal(30, frame.Header.Height);
        Assert.Equal(160, frame.Header.Stride);
        Assert.Equal(4800, frame.Payload.Length);
    }
}
