using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace BlenderAvaloniaBridge.Runtime;

internal static class HeadlessFrameCapture
{
    public static WriteableBitmap Capture(Window window, int maxAttempts = 3)
    {
        ArgumentNullException.ThrowIfNull(window);

        for (var i = 0; i < maxAttempts; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
            Dispatcher.UIThread.RunJobs();

            var bitmap = window.GetLastRenderedFrame();
            if (bitmap is not null)
            {
                return bitmap;
            }
        }

        // Avalonia's headless capture API performs its own flush and is the
        // most reliable fallback when the last rendered frame has not been
        // published yet.
        return window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("Headless renderer did not produce a frame.");
    }
}
