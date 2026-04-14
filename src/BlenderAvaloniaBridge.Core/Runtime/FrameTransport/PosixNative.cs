using System.Runtime.InteropServices;

namespace BlenderAvaloniaBridge.Runtime.FrameTransport;

internal static partial class PosixNative
{
    internal const int OpenReadWrite = 0x0002;
    internal const int OpenReadOnly = 0x0000;
    internal const int OpenCreate = 0x0200;
    internal const int ProtectionRead = 0x0001;
    internal const int ProtectionWrite = 0x0002;
    internal const int MapShared = 0x0001;
    internal const uint UserReadWrite = 0x180;
    internal static readonly nint MapFailed = new(-1);

    [LibraryImport("/usr/lib/libSystem.B.dylib", EntryPoint = "shm_open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int SharedMemoryOpen(string name, int oflag, uint mode);

    [LibraryImport("/usr/lib/libSystem.B.dylib", EntryPoint = "shm_unlink", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int SharedMemoryUnlink(string name);

    [LibraryImport("/usr/lib/libSystem.B.dylib", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int OpenFile(string path, int oflag, uint mode);

    [LibraryImport("/usr/lib/libSystem.B.dylib", EntryPoint = "unlink", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int UnlinkFile(string path);

    [LibraryImport("/usr/lib/libSystem.B.dylib", EntryPoint = "ftruncate", SetLastError = true)]
    internal static partial int Truncate(int fd, long length);

    [LibraryImport("/usr/lib/libSystem.B.dylib", EntryPoint = "mmap", SetLastError = true)]
    internal static partial nint MapMemory(nint address, nuint length, int protection, int flags, int fd, long offset);

    [LibraryImport("/usr/lib/libSystem.B.dylib", EntryPoint = "munmap", SetLastError = true)]
    internal static partial int UnmapMemory(nint address, nuint length);

    [LibraryImport("/usr/lib/libSystem.B.dylib", EntryPoint = "close", SetLastError = true)]
    internal static partial int Close(int fd);

    internal static Exception CreateIOException(string operation, string target)
    {
        var error = Marshal.GetLastPInvokeError();
        return new IOException($"{operation} failed for '{target}' (errno {error}).");
    }
}
