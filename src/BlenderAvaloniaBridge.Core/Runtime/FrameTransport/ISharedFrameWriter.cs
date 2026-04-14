namespace BlenderAvaloniaBridge.Runtime.FrameTransport;

internal interface ISharedFrameWriter : IDisposable
{
    int WriteLinearRgbaFrameFromRgba(ReadOnlySpan<byte> payload, int sequence);
}
