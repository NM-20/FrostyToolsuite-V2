namespace FrostyEditor.Native;

internal struct IMAGE_DATA_DIRECTORY
{
    public const int SIZE = 8;

    public uint VirtualAddress;
    public uint Size;
}

/// <summary>
/// DOS .EXE header
/// </summary>
internal unsafe struct IMAGE_DOS_HEADER
{
    #pragma warning disable IDE1006
    /// <summary>
    /// Magic number
    /// </summary>
    public ushort e_magic;

    /// <summary>
    /// Bytes on last page of file
    /// </summary>
    public ushort e_cblp;

    /// <summary>
    /// Pages in file
    /// </summary>
    public ushort e_cp;

    /// <summary>
    /// Relocations
    /// </summary>
    public ushort e_crlc;

    /// <summary>
    /// Size of header in paragraphs
    /// </summary>
    public ushort e_cparhdr;

    /// <summary>
    /// Minimum extra paragraphs needed
    /// </summary>
    public ushort e_minalloc;

    /// <summary>
    /// Maximum extra paragraphs needed
    /// </summary>
    public ushort e_maxalloc;

    /// <summary>
    /// Initial (relative) SS value
    /// </summary>
    public ushort e_ss;

    /// <summary>
    /// Initial SP value
    /// </summary>
    public ushort e_sp;

    /// <summary>
    /// Checksum
    /// </summary>
    public ushort e_csum;

    /// <summary>
    /// Initial IP value
    /// </summary>
    public ushort e_ip;

    /// <summary>
    /// Initial (relative) CS value
    /// </summary>
    public ushort e_cs;

    /// <summary>
    /// File address of relocation table
    /// </summary>
    public ushort e_lfarlc;

    /// <summary>
    /// Overlay number
    /// </summary>
    public ushort e_ovno;

    /// <summary>
    /// Reserved words
    /// </summary>
    public fixed ushort e_res[4];

    /// <summary>
    /// OEM identifier (for e_oeminfo)
    /// </summary>
    public ushort e_oemid;

    /// <summary>
    /// OEM information; e_oemid specific
    /// </summary>
    public ushort e_oeminfo;

    /// <summary>
    /// Reserved words
    /// </summary>
    public fixed ushort e_res2[10];

    /// <summary>
    /// File address of new exe header
    /// </summary>
    public int e_lfanew;
    #pragma warning restore IDE1006
}

internal struct IMAGE_FILE_HEADER
{
    public ushort Machine;
    public ushort NumberOfSections;
    public uint   TimeDateStamp;
    public uint   PointerToSymbolTable;
    public uint   NumberOfSymbols;
    public ushort SizeOfOptionalHeader;
    public ushort Characteristics;
}

internal struct IMAGE_NT_HEADERS
{
    public uint Signature;
    public IMAGE_FILE_HEADER FileHeader;
    public IMAGE_OPTIONAL_HEADER OptionalHeader;
}

internal unsafe struct IMAGE_OPTIONAL_HEADER
{
    public const int IMAGE_NUMBEROF_DIRECTORY_ENTRIES = 16;

    public ushort Magic;
    public byte   MajorLinkerVersion;
    public byte   MinorLinkerVersion;
    public uint   SizeOfCode;
    public uint   SizeOfInitializedData;
    public uint   SizeOfUninitializedData;
    public uint   AddressOfEntryPoint;
    public uint   BaseOfCode;
    public nuint  ImageBase;
    public uint   SectionAlignment;
    public uint   FileAlignment;
    public ushort MajorOperatingSystemVersion;
    public ushort MinorOperatingSystemVersion;
    public ushort MajorImageVersion;
    public ushort MinorImageVersion;
    public ushort MajorSubsystemVersion;
    public ushort MinorSubsystemVersion;
    public uint   Win32VersionValue;
    public uint   SizeOfImage;
    public uint   SizeOfHeaders;
    public uint   CheckSum;
    public ushort Subsystem;
    public ushort DllCharacteristics;
    public nuint  SizeOfStackReserve;
    public nuint  SizeOfStackCommit;
    public nuint  SizeOfHeapReserve;
    public nuint  SizeOfHeapCommit;
    public uint   LoaderFlags;
    public uint   NumberOfRvaAndSizes;
    public fixed byte DataDirectory[IMAGE_NUMBEROF_DIRECTORY_ENTRIES * IMAGE_DATA_DIRECTORY.SIZE];
}
