using Avalonia.Media.Imaging;

namespace BlenderAvaloniaBridge.Bridge;

public static class FramePublisher
{
    public static ProtocolPacket ExtractFrame(WriteableBitmap bitmap, int seq)
    {
        using var locked = bitmap.Lock();
        var payload = new byte[locked.RowBytes * locked.Size.Height];
        unsafe
        {
            var source = new Span<byte>(locked.Address.ToPointer(), payload.Length);
            source.CopyTo(payload);
        }

        return ProtocolPacket.CreateFrame(
            new ProtocolEnvelope
            {
                Type = "frame",
                Seq = seq,
                Width = locked.Size.Width,
                Height = locked.Size.Height,
                PixelFormat = "bgra8",
                Stride = locked.RowBytes
            },
            payload);
    }
}
