namespace BlenderAvaloniaBridge.Runtime.FrameTransport;

internal sealed class PosixFileMappedRegion : IDisposable
{
    private readonly nuint _length;
    private readonly string _path;
    private nint _address;
    private int _fd;

    private PosixFileMappedRegion(string path, int fd, nint address, nuint length)
    {
        _path = path;
        _fd = fd;
        _address = address;
        _length = length;
    }

    public static PosixFileMappedRegion OpenExisting(string path, long length)
    {
        ValidatePath(path);
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        var fd = PosixNative.OpenFile(path, PosixNative.OpenReadWrite, 0);
        if (fd < 0)
        {
            throw PosixNative.CreateIOException("open", path);
        }

        try
        {
            var address = PosixNative.MapMemory(
                nint.Zero,
                checked((nuint)length),
                PosixNative.ProtectionRead | PosixNative.ProtectionWrite,
                PosixNative.MapShared,
                fd,
                0);

            if (address == PosixNative.MapFailed)
            {
                throw PosixNative.CreateIOException("mmap", path);
            }

            return new PosixFileMappedRegion(path, fd, address, checked((nuint)length));
        }
        catch
        {
            PosixNative.Close(fd);
            throw;
        }
    }

    public unsafe Span<byte> GetWritableSpan(long offset, int length)
    {
        ObjectDisposedException.ThrowIf(_address == nint.Zero, this);

        if (offset < 0 || length < 0 || offset + length > (long)_length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        return new Span<byte>((void*)(_address + offset), length);
    }

    public void Read(long offset, Span<byte> destination)
    {
        GetWritableSpan(offset, destination.Length).CopyTo(destination);
    }

    public void Dispose()
    {
        if (_address != nint.Zero)
        {
            PosixNative.UnmapMemory(_address, _length);
            _address = nint.Zero;
        }

        if (_fd >= 0)
        {
            PosixNative.Close(_fd);
            _fd = -1;
        }
    }

    private static void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Mapped file path must not be empty.", nameof(path));
        }
    }
}
