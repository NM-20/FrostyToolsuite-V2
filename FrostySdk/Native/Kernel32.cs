global using static Frosty.Sdk.Native.Kernel32;

using System;
using System.Runtime.InteropServices;

namespace Frosty.Sdk.Native;

internal static partial class Kernel32
{
    public const int GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS       = 0x00000004;
    public const int GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT = 0x00000002;

    [LibraryImport("Kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool GetModuleHandleExW(
        uint dwFlags, nint lpModuleName, out nint phModule);
}
