global using static FrostyEditor.Native.User32;

using System.Runtime.InteropServices;

namespace FrostyEditor.Native;

internal static partial class User32
{
    public const int MB_ICONERROR = 0x00000010;

    public const int MB_OK = 0x00000000;

    /*
     * This should ONLY be used when we're dealing with early parts of the bootflow, i.e. whenever
     * a profile cannot be found, etc.
     */
    [LibraryImport("User32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial int MessageBoxW(nint hWnd, string lpText, string lpCaption, uint uType);
}
