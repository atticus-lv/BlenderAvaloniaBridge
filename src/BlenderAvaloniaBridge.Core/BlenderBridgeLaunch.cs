namespace BlenderAvaloniaBridge;

public sealed record BlenderBridgeLaunch(
    string[] AppArgs,
    CommandLineOptions? BridgeCommandLine)
{
    public bool IsBridgeMode => BridgeCommandLine?.Mode == LaunchMode.BlenderBridge;

    public BlenderBridgeOptions GetRequiredBridgeOptions()
    {
        return BridgeCommandLine?.ToBridgeOptions()
            ?? throw new InvalidOperationException("Bridge options are only available in Blender bridge mode.");
    }
}
