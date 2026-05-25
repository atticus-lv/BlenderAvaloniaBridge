using System.Runtime.InteropServices;

namespace BlenderAvaloniaBridge.Runtime.MacOS;

internal static partial class MacMetalNative
{
    private const string CoreFoundationPath = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string IOSurfacePath = "/System/Library/Frameworks/IOSurface.framework/IOSurface";
    private const string ObjCPath = "/usr/lib/libobjc.A.dylib";

    [LibraryImport("/System/Library/Frameworks/Metal.framework/Metal")]
    public static partial IntPtr MTLCreateSystemDefaultDevice();

    [LibraryImport(ObjCPath, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr GetClass(string name);

    [LibraryImport(ObjCPath, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr RegisterSelector(string name);

    [LibraryImport(ObjCPath, EntryPoint = "objc_msgSend")]
    public static partial IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjCPath, EntryPoint = "objc_msgSend")]
    public static partial IntPtr IntPtr_objc_msgSend_UInt64_UInt64_UInt64_Bool(
        IntPtr receiver,
        IntPtr selector,
        ulong arg1,
        ulong arg2,
        ulong arg3,
        [MarshalAs(UnmanagedType.I1)] bool arg4);

    [LibraryImport(ObjCPath, EntryPoint = "objc_msgSend")]
    public static partial IntPtr IntPtr_objc_msgSend_IntPtr_IntPtr_UInt64(
        IntPtr receiver,
        IntPtr selector,
        IntPtr arg1,
        IntPtr arg2,
        ulong arg3);

    [LibraryImport(ObjCPath, EntryPoint = "objc_msgSend")]
    public static partial void void_objc_msgSend_UInt64(IntPtr receiver, IntPtr selector, ulong arg1);

    [LibraryImport(ObjCPath, EntryPoint = "objc_msgSend")]
    public static partial void objc_msgSend(IntPtr receiver, IntPtr selector);

    [LibraryImport(CoreFoundationPath)]
    public static partial IntPtr CFDictionaryCreateMutable(IntPtr allocator, nint capacity, IntPtr keyCallBacks, IntPtr valueCallBacks);

    [LibraryImport(CoreFoundationPath)]
    public static partial void CFDictionarySetValue(IntPtr dictionary, IntPtr key, IntPtr value);

    [LibraryImport(CoreFoundationPath)]
    public static partial IntPtr CFNumberCreate(IntPtr allocator, CFNumberType theType, ref int valuePtr);

    [LibraryImport(CoreFoundationPath)]
    public static partial void CFRelease(IntPtr cf);

    [LibraryImport(IOSurfacePath)]
    public static partial IntPtr IOSurfaceCreate(IntPtr properties);

    [LibraryImport(IOSurfacePath)]
    public static partial uint IOSurfaceGetID(IntPtr buffer);

    public static IntPtr GetSelector(string name) => RegisterSelector(name);

    public static IntPtr GetCoreFoundationExport(string name)
        => NativeLibrary.GetExport(NativeLibrary.Load(CoreFoundationPath), name);

    public static IntPtr GetCoreFoundationConstant(string name)
        => Marshal.ReadIntPtr(NativeLibrary.GetExport(NativeLibrary.Load(CoreFoundationPath), name));

    public static IntPtr GetIOSurfaceConstant(string name)
        => Marshal.ReadIntPtr(NativeLibrary.GetExport(NativeLibrary.Load(IOSurfacePath), name));
}

internal enum CFNumberType
{
    SInt32 = 3,
}
