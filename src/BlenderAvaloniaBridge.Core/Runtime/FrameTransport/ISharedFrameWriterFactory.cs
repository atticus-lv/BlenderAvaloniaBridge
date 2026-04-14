namespace BlenderAvaloniaBridge.Runtime.FrameTransport;

internal interface ISharedFrameWriterFactory
{
    void ValidatePlatformSupport();

    ISharedFrameWriter Create(string mappingName, int slotSize, int slotCount);
}
