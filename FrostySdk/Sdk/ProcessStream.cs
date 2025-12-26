using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Frosty.Sdk.Sdk;

/// <summary>
/// Represents a <see cref="Stream"/> that can read from another <see cref="System.Diagnostics.Process"/>.
/// </summary>
public unsafe partial class ProcessStream : Stream
{
    /*
     * Use of this `Stream` should be reserved for when we need to scan a game booted externally, such as
     * when a particular game does not support the injected workflow.
     */

    [LibraryImport("Kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ReadProcessMemory(
        SafeProcessHandle hProcess, nint lpBaseAddress, nint lpBuffer, nint nSize, out nint lpNumberOfBytesRead);

    public override bool CanRead  => true;
    public override bool CanSeek  => true;
    public override bool CanWrite => false;

    public override long Length => (Process.MainModule!.ModuleMemorySize);

    public override long Position { get; set; }

    /// <summary>
    /// Gets or sets the stream's position as a pointer relative to the base address of its <see cref="Process"/>.
    /// </summary>
    public nint PositionPointer
    {
        get => (nint)(Process.MainModule!.BaseAddress + Position);
        set => Position = (value - Process.MainModule!.BaseAddress);
    }

    /// <summary>
    /// The <see cref="System.Diagnostics.Process"/> that this <see cref="ProcessStream"/> will read from.
    /// </summary>
    public Process Process { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessStream"/> class with the given parameters.
    /// </summary>
    /// <param name="process">The <see cref="System.Diagnostics.Process"/> that will be read from.</param>
    public ProcessStream(Process process) => Process = process;

    public override void Flush() =>
        throw new NotImplementedException();

    public override long Seek(long offset, SeekOrigin origin) => origin switch
    {
        SeekOrigin.Begin =>
            (Position = (Process.MainModule!.BaseAddress + offset)),

        SeekOrigin.Current =>
            (Position += offset),

        SeekOrigin.End =>
            (Position = ((Process.MainModule!.BaseAddress + Process.MainModule!.ModuleMemorySize) - offset)),

        _ => throw new ArgumentException()
    };

    public override int Read(byte[] buffer, int offset, int count)
    {
        fixed (byte *pinned = buffer)
        {
            /* 
             * In the event that this method is getting directly accessed, we will need to ensure that we're
             * not overwriting past the bounds of the buffer.
             */
            if (count > buffer.Length)
            {
                return 0;
            }

            ReadProcessMemory(Process.SafeHandle, PositionPointer, (nint)(pinned), count, out nint read);
            Position += read;

            /*
             * We do not throw exceptions here, as that is seemingly the responsibility of a `Reader` to do.
             */
            return (int)(read);
        }
    }

    public override void SetLength(long value) =>
        throw new NotImplementedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
}
