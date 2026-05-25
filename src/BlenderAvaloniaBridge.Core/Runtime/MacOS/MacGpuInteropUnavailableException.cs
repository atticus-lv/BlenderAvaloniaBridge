namespace BlenderAvaloniaBridge.Runtime.MacOS;

internal sealed class MacGpuInteropUnavailableException : Exception
{
    public MacGpuInteropUnavailableException(string message)
        : base(message)
    {
    }

    public MacGpuInteropUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
