namespace BlenderAvaloniaBridge;

public sealed class BlenderBridgeOptions
{
    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; }

    public int Width { get; set; } = 800;

    public int Height { get; set; } = 600;

    public double RenderScaling { get; set; } = 1.25;

    public int TargetFps { get; set; } = 60;

    public int IdleHeartbeatFps { get; set; } = 4;

    public int ContinuousFrameWindowMs { get; set; } = 1000;

    public bool UseSharedMemory { get; set; } = true;

    public bool EnableDiagnostics { get; set; } = true;

    internal TimeSpan ActiveFrameInterval =>
        TargetFps > 0 ? TimeSpan.FromMilliseconds(1000.0 / TargetFps) : TimeSpan.Zero;

    internal TimeSpan IdleHeartbeatInterval =>
        IdleHeartbeatFps > 0 ? TimeSpan.FromMilliseconds(1000.0 / IdleHeartbeatFps) : TimeSpan.Zero;

    internal TimeSpan ContinuousFrameWindow =>
        ContinuousFrameWindowMs > 0 ? TimeSpan.FromMilliseconds(ContinuousFrameWindowMs) : TimeSpan.Zero;

    internal BlenderBridgeOptions Clone()
    {
        return new BlenderBridgeOptions
        {
            Host = Host,
            Port = Port,
            Width = Width,
            Height = Height,
            RenderScaling = RenderScaling,
            TargetFps = TargetFps,
            IdleHeartbeatFps = IdleHeartbeatFps,
            ContinuousFrameWindowMs = ContinuousFrameWindowMs,
            UseSharedMemory = UseSharedMemory,
            EnableDiagnostics = EnableDiagnostics,
        };
    }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new ArgumentException("Host must not be empty.", nameof(Host));
        }

        if (Port < 0 || Port > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(Port));
        }

        if (Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width));
        }

        if (Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Height));
        }

        if (double.IsNaN(RenderScaling) || double.IsInfinity(RenderScaling) || RenderScaling <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RenderScaling));
        }

        if (TargetFps < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TargetFps));
        }

        if (IdleHeartbeatFps < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(IdleHeartbeatFps));
        }

        if (ContinuousFrameWindowMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ContinuousFrameWindowMs));
        }
    }
}
