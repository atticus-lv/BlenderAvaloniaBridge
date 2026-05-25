using SkiaSharp;

namespace BlenderAvaloniaBridge.Runtime.MacOS;

internal static class MacMetalContextProvider
{
    public static MacMetalContextLease Create()
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new MacGpuInteropUnavailableException("macOS Metal GPU interop is only available on macOS.");
        }

        var device = IntPtr.Zero;
        var queue = IntPtr.Zero;
        GRMtlBackendContext? backendContext = null;
        GRContext? grContext = null;

        try
        {
            device = MacMetalNative.MTLCreateSystemDefaultDevice();
            if (device == IntPtr.Zero)
            {
                throw new MacGpuInteropUnavailableException("MTLCreateSystemDefaultDevice returned null.");
            }

            queue = MacMetalNative.IntPtr_objc_msgSend(device, MacMetalNative.GetSelector("newCommandQueue"));
            if (queue == IntPtr.Zero)
            {
                throw new MacGpuInteropUnavailableException("Could not create a Metal command queue.");
            }

            backendContext = new GRMtlBackendContext
            {
                DeviceHandle = device,
                QueueHandle = queue,
            };
            grContext = GRContext.CreateMetal(backendContext);
            if (grContext is null)
            {
                throw new MacGpuInteropUnavailableException("Skia could not create a Metal GRContext.");
            }

            var lease = new MacMetalContextLease(grContext, backendContext, device, queue);
            device = IntPtr.Zero;
            queue = IntPtr.Zero;
            backendContext = null;
            grContext = null;
            return lease;
        }
        catch (MacGpuInteropUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new MacGpuInteropUnavailableException("Could not initialize macOS Metal GPU interop.", ex);
        }
        finally
        {
            grContext?.Dispose();
            backendContext?.Dispose();

            if (queue != IntPtr.Zero)
            {
                MacMetalNative.objc_msgSend(queue, MacMetalNative.GetSelector("release"));
            }

            if (device != IntPtr.Zero)
            {
                MacMetalNative.objc_msgSend(device, MacMetalNative.GetSelector("release"));
            }
        }
    }
}
