namespace BlenderAvaloniaBridge.Runtime.FrameTransport;

internal sealed class SharedFrameWriterFactory : ISharedFrameWriterFactory
{
    public static SharedFrameWriterFactory Instance { get; } = new();

    private SharedFrameWriterFactory()
    {
    }

    public void ValidatePlatformSupport()
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            return;
        }

        throw new PlatformNotSupportedException("Shared-memory bridge mode is currently supported on Windows and macOS only.");
    }

    public ISharedFrameWriter Create(string mappingName, int slotSize, int slotCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mappingName);

        if (OperatingSystem.IsWindows())
        {
            return new WindowsSharedMemoryFrameWriter(mappingName, slotSize, slotCount);
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacSharedMemoryFrameWriter(mappingName, slotSize, slotCount);
        }

        throw new PlatformNotSupportedException("Shared-memory bridge mode is currently supported on Windows and macOS only.");
    }
}
