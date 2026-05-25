namespace BlenderAvaloniaBridge.Runtime;

internal enum BridgeFrameTransport
{
    InlineBgra,
    SharedMemoryLinearRgba,
    MacOSIOSurface,
}

internal static class BridgeFrameTransportNames
{
    public const string InlineBgra = "inline_bgra";
    public const string SharedMemory = "shared_memory";
    public const string MacOSIOSurface = "macos_iosurface";

    public static string ToProtocolName(BridgeFrameTransport transport)
    {
        return transport switch
        {
            BridgeFrameTransport.SharedMemoryLinearRgba => SharedMemory,
            BridgeFrameTransport.MacOSIOSurface => MacOSIOSurface,
            _ => InlineBgra,
        };
    }
}
