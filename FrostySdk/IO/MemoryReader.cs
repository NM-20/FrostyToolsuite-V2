using Frosty.Sdk.Sdk;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Frosty.Sdk.IO;

public sealed unsafe partial class MemoryReader : IDisposable
{
    private PatternScanner m_scanner;

    /// <summary>
    /// The reader's current position within the <see cref="System.IO.Stream">.
    /// </summary>
    public long Position { get; set; }

    /// <summary>
    /// The <see cref="System.IO.Stream" that is currently being read from. />
    /// </summary>
    public Stream Stream { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryReader"/> class with the given parameters.
    /// </summary>
    /// <param name="stream">The <see cref="System.IO.Stream"/> to read from.</param>
    public MemoryReader(Stream stream)
    {
        Stream = stream;
        m_scanner = new PatternScanner(stream);
    }

    /*
     * `PatternScanner` is disposable as well, and since we passed our stream to it, we will want to
     * allow it to do the disposing.
     */
    public void Dispose() => m_scanner.Dispose();

    /// <summary>
    /// Aligns the reader's current position to the specified alignment if it is not already aligned.
    /// </summary>
    /// <param name="alignment">The alignment.</param>
    public void Pad(int alignment)
    {
        if (Position % alignment != 0)
        {
            Position += (alignment - (Position % alignment));
        }
    }

    /// <summary>
    /// Reads a <see cref="byte"> from the underlying stream.
    /// </summary>
    /// <returns>The <see cref="byte"/>.</returns>
    public byte ReadByte()
    {
        Span<byte> buffer = stackalloc byte[sizeof(byte)];
        ReadExactly(buffer);
        return buffer[0];
    }

    /// <summary>
    /// Reads a <see cref="short"/> from the underlying stream.
    /// </summary>
    /// <param name="pad">Whether the reader's position gets aligned to sizeof(<see cref="short"/>).</param>
    /// <returns>The <see cref="short"/>.</returns>
    public short ReadShort(bool pad = true)
    {
        if (pad)
        {
            Pad(sizeof(short));
        }

        Span<byte> buffer = stackalloc byte[sizeof(short)];
        ReadExactly(buffer);

        return BinaryPrimitives.ReadInt16LittleEndian(buffer);
    }

    /// <summary>
    /// Reads a <see cref="ushort"/> from the underlying stream.
    /// </summary>
    /// <param name="pad">Whether the reader's position gets aligned to sizeof(<see cref="ushort"/>).</param>
    /// <returns>The <see cref="ushort"/>.</returns>
    public ushort ReadUShort(bool pad = true)
    {
        return (ushort)(ReadShort(pad));
    }

    /// <summary>
    /// Reads an <see cref="int"/> from the underlying stream.
    /// </summary>
    /// <param name="pad">Whether the reader's position gets aligned to sizeof(<see cref="int"/>).</param>
    /// <returns>The <see cref="int"/>.</returns>
    public int ReadInt(bool pad = true)
    {
        if (pad)
        {
            Pad(sizeof(int));
        }

        Span<byte> buffer = stackalloc byte[sizeof(int)];
        ReadExactly(buffer);

        return BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    /// <summary>
    /// Reads a <see cref="uint"/> from the underlying stream.
    /// </summary>
    /// <param name="pad">Whether the reader's position gets aligned to sizeof(<see cref="uint"/>).</param>
    /// <returns>The <see cref="uint"/>.</returns>
    public uint ReadUInt(bool pad = true)
    {
        return (uint)(ReadInt(pad));
    }

    /// <summary>
    /// Reads a <see cref="long"/> from the underlying stream.
    /// </summary>
    /// <param name="pad">Whether the reader's position gets aligned to sizeof(<see cref="long"/>).</param>
    /// <returns>The <see cref="long"/>.</returns>
    public long ReadLong(bool pad = true)
    {
        if (pad)
        {
            Pad(sizeof(long));
        }

        Span<byte> buffer = stackalloc byte[sizeof(long)];
        ReadExactly(buffer);

        return BinaryPrimitives.ReadInt64LittleEndian(buffer);
    }

    /// <summary>
    /// Reads a <see cref="ulong"/> from the underlying stream.
    /// </summary>
    /// <param name="pad">Whether the reader's position gets aligned to sizeof(<see cref="ulong"/>).</param>
    /// <returns>The <see cref="ulong"/>.</returns>
    public ulong ReadULong(bool pad = true)
    {
        return (ulong)(ReadLong(pad));
    }

    /// <summary>
    /// Reads a <see cref="float"/> from the underlying stream.
    /// </summary>
    /// <param name="pad">Whether the reader's position gets aligned to sizeof(<see cref="float"/>).</param>
    /// <returns>The <see cref="float"/>.</returns>
    public float ReadSingle(bool pad = true)
    {
        if (pad)
        {
            Pad(sizeof(float));
        }

        Span<byte> buffer = stackalloc byte[sizeof(float)];
        ReadExactly(buffer);

        return BinaryPrimitives.ReadSingleLittleEndian(buffer);
    }

    /// <summary>
    /// Reads a <see cref="double"/> from the underlying stream.
    /// </summary>
    /// <param name="pad">Whether the reader's position gets aligned to sizeof(<see cref="double"/>).</param>
    /// <returns>The <see cref="double"/>.</returns>
    public double ReadDouble(bool pad = true)
    {
        if (pad)
        {
            Pad(sizeof(double));
        }

        Span<byte> buffer = stackalloc byte[sizeof(double)];
        ReadExactly(buffer);

        return BinaryPrimitives.ReadDoubleLittleEndian(buffer);
    }

    /// <summary>
    /// Reads a <see cref="Guid"/> from the underlying stream.
    /// </summary>
    /// <param name="pad">Whether the reader's position gets aligned for a <see cref="Guid"/>.</param>
    /// <returns>The <see cref="Guid"/>.</returns>
    public Guid ReadGuid(bool pad = true)
    {
        if (pad)
        {
            Pad(4);
        }

        Span<byte> span = stackalloc byte[sizeof(Guid)];
        ReadExactly(span);

        return new Guid(span);
    }

    /// <summary>
    /// Reads an <see cref="Sha1"/> from the underlying stream.
    /// </summary>
    /// <returns>The <see cref="Sha1"/>.</returns>
    public Sha1 ReadSha1()
    {
        Span<byte> span = stackalloc byte[sizeof(Sha1)];
        ReadExactly(span);

        return new Sha1(span);
    }

    /// <summary>
    /// Reads a pointer to a null-terminated string, then returns to the reader's original position.
    /// </summary>
    /// <param name="pad">Whether the reader's position gets aligned to sizeof(<see cref="nuint"/>).</param>
    /// <returns>The <see cref="string"/>.</returns>
    public string ReadNullTerminatedString(bool pad = true)
    {
        if (pad)
        {
            Pad(sizeof(long));
        }

        long offset = ReadLong();
        long orig = Position;
        Position = offset;

        StringBuilder sb = new();
        while (true)
        {
            char c = (char)ReadByte();
            if (c == 0x00)
            {
                break;
            }

            sb.Append(c);
        }

        Position = orig;
        return sb.ToString();
    }

    /// <summary>
    /// Reads the exact amount of bytes specified. If the stream can't sustain the requested read, throws an
    /// <see cref="EndOfStreamException"/>.
    /// </summary>
    /// <param name="buffer">The buffer to read into.</param>
    public void ReadExactly(Span<byte> buffer) => Stream.ReadExactly(buffer);

    /// <summary>
    /// Scans the full address range for sequences of bytes matching the provided pattern.
    /// This does not perform any caching.
    /// For caching support, see <see cref="PatternScanner"/>.
    /// </summary>
    /// <param name="pattern">
    /// The pattern, which may be composed of any of the following:
    /// - Dereferences:          [01 02 03 04]
    /// - Exact Matches:          01 02 03 04
    /// - Partial/Full Wildcards: ?1 ?? ?? 0? 
    /// </param>
    /// <returns>
    /// The addresses of matching sequences of bytes if found. Otherwise, zero is returned.
    /// </returns>
    public nint ScanPattern(string pattern) => m_scanner.ScanOne(pattern, false);
}