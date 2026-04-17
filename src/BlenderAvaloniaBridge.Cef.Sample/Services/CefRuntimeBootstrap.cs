using System.IO;
using System.Threading;
using Xilium.CefGlue;
using Xilium.CefGlue.Common;

namespace BlenderAvaloniaBridge.Cef.Sample.Services;

internal static class CefRuntimeBootstrap
{
    private static int _initialized;
    private static string? _cachePath;

    public static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
        {
            return;
        }

        _cachePath = Path.Combine(
            Path.GetTempPath(),
            "BlenderAvaloniaBridge.Cef.Sample",
            Guid.NewGuid().ToString("N"));

        AppDomain.CurrentDomain.ProcessExit += (_, _) => Cleanup();

        CefRuntimeLoader.Initialize(new CefSettings
        {
            RootCachePath = _cachePath,
            // Keep browser rendering capturable when the app is driven through the
            // existing headless bridge pipeline.
            WindowlessRenderingEnabled = true,
        });
    }

    private static void Cleanup()
    {
        try
        {
            CefRuntime.Shutdown();
        }
        catch
        {
        }

        if (string.IsNullOrWhiteSpace(_cachePath))
        {
            return;
        }

        try
        {
            var directory = new DirectoryInfo(_cachePath);
            if (directory.Exists)
            {
                directory.Delete(true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
