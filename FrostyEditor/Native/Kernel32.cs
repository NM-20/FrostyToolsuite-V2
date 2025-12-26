global using static FrostyEditor.Native.Kernel32;

using System.Runtime.InteropServices;

namespace FrostyEditor.Native;

internal static partial class Kernel32
{
    [LibraryImport("Kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandleW(string? lpModuleName);
}
