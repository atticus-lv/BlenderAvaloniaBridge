using SkiaSharp;

namespace BlenderAvaloniaBridge.Runtime.MacOS;

internal sealed class MacMetalContextLease : IDisposable
{
    private bool _isDisposed;

    public MacMetalContextLease(GRContext grContext, GRMtlBackendContext backendContext, IntPtr device, IntPtr queue)
    {
        GrContext = grContext;
        BackendContext = backendContext;
        Device = device;
        Queue = queue;
    }

    public GRContext GrContext { get; }

    public GRMtlBackendContext BackendContext { get; }

    public IntPtr Device { get; }

    public IntPtr Queue { get; }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        GrContext.Dispose();
        BackendContext.Dispose();
        MacMetalNative.objc_msgSend(Queue, MacMetalNative.GetSelector("release"));
        MacMetalNative.objc_msgSend(Device, MacMetalNative.GetSelector("release"));
    }
}
