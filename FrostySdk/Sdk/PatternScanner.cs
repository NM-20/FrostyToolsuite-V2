using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Frosty.Sdk.Sdk;

/// <summary>
/// General-purpose pattern scanner that can search within various streams or, through the constructors,
/// processes.
/// </summary>
public sealed unsafe class PatternScanner : IDisposable
{
    /* With V2's goals involving code injection, we'll need a quick method to find addresses within the
     * game executables. In combination with caching, this class should provide decent performance.
     */
    private ref struct Context
    {
        public byte[] Buffer;
        public nint End;
        public nint Start;

        public int CurrentPosition;
        public int Length;
        public nint MatchedPosition;

        /*
         * This is only used by dereferences when we're caching their results; we'll need to be able to
         * discern the address at which the dereference was performed for a memory hash.
         */
        public nint OriginPosition;
    }

    private enum PatternElementKind
    {
        Dereference   = 0,
        Exact         = 1,
        FullWildcard  = 2,
        LeftWildcard  = 3,
        RightWildcard = 4,
    }
    
    private record struct PatternElement
    (
        PatternElementKind Kind,
        uint ByteValue = 0,
        List<PatternElement>? Children = null
    );

    private record struct UnbasedScan
    (
        nint MatchedPosition,
        nint OriginPosition
    );

    private const int c_memoryHashMaximumSize = 16;

    /*
     * We provide the `ParsePattern` process one character of look-ahead for a sliding window approach.
     */
    private const int c_parsePatternLookahead = 1;

    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true
    };
    private static JsonObject s_cache = new();

    /* To cache results, this must be set to either a `ProcessStream` or an `UnmanagedMemoryStream`. */
    private readonly Stream m_stream;

    /// <summary>
    /// Initializes a new instance of the <see cref="PatternScanner"/> class.
    /// </summary>
    public PatternScanner()
    {
        Process process = Process.GetCurrentProcess();
        m_stream = new UnmanagedMemoryStream(
            (byte *)(process.MainModule!.BaseAddress), process.MainModule!.ModuleMemorySize);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PatternScanner"/> class with the given parameters.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> to scan.</param>
    public PatternScanner(Stream stream) => m_stream = stream;

    /// <summary>
    /// Initializes a new instance of the <see cref="PatternScanner"/> class with the given parameters.
    /// </summary>
    /// <param name="module">The name of a module within the current process to scan.</param>
    public PatternScanner(string module)
    {
        /* 
         * Since we're in the context of a constructor, we'll have to throw an exception if we couldn't
         * find the named module.
         */
        Process process = Process.GetCurrentProcess();
        foreach (ProcessModule current in process.Modules)
        {
            if (current.FileName.Contains(module, StringComparison.OrdinalIgnoreCase))
            {
                m_stream =
                    new UnmanagedMemoryStream((byte *)(current.BaseAddress), current.ModuleMemorySize);
                return;
            }
        }

        throw new ArgumentException("The named module could not be found within the current process!");
    }

    private bool ExtendBuffer(ref Context context)
    {
        m_stream.Position = ((m_stream.Position - context.Length) +
            context.OriginPosition);
        context.Length =
            m_stream.Read(context.Buffer, 0, context.Buffer.Length);

        context.CurrentPosition = 0;
        context.OriginPosition = 0;
        return (context.Length is not 0);
    }

    private bool GetBaseAddressFromScanBase(out nint @base)
    {
        if (m_stream is ProcessStream process)
        {
            return GetBaseAddressFromProcessScanBase(process, out @base);
        }
        else if (m_stream is UnmanagedMemoryStream unmanaged)
        {
            return GetModuleHandleExW(
                (GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT),
                (nint)(unmanaged.PositionPointer - unmanaged.Position), out @base);
        }

        @base = 0;
        return false;
    }

    private bool GetCachedScanAllResult(string pattern, nint start, nint end, List<nint> results)
    {
        /*
         * We'll first need to determine if the address range is fit for caching, as in it points to
         * static memory.
         * TODO: Is there a better way to detect this?
         */

        GetBaseAddressFromScanBase(out nint @base);

        /*
         * Once we've determined that the memory is static and we have a handle (effectively the base
         * address), we can begin formatting our key. We'll need an Fnv-1 hash of the pattern and
         * both the start and end address as relative values.
         */
        string key = $"{Utils.Utils.HashString(pattern):X8}:{start:X8}:{end:X8}";

        if (!s_cache.ContainsKey(key))
        {
            return false;
        }

        /* 
         * Beyond checking if the key exists, we now need to validate that the value is defined with
         * the proper formatting.
         */
        if (s_cache[key] is not JsonArray cached || cached.Count is 0)
        {
            return false;
        }

        /* 
         * Knowing that indexing the key results in an array, we'll want to iterate over each element
         * and verify that all of them are objects.
         */
        foreach (JsonNode? current in cached)
        {
            if (current is not JsonObject)
            {
                return false;
            }

            /*
             * The indexer returns null if the key doesn't have an associated value, so we'll need a
             * check for this.
             */
            JsonNode? relative = current["RelativePosition"];
            if (relative is not JsonValue || relative.GetValueKind() is not JsonValueKind.Number)
            {
                return false;
            }

            var absolute = (nint)(relative.GetValue<int>());

            /*
             * In Denuvo-protected games, the executable size is pretty variable, so we will want to
             * ensure the absolute address is still in range. This also slightly protects against
             * incorrectly edited cache files.
             */
            if (absolute < start || absolute >= end)
            {
                return false;
            }
            absolute += @base;

            JsonNode? origin = current["OriginPosition"];
            if (origin is not JsonValue || origin.GetValueKind() is not JsonValueKind.Number)
            {
                return false;
            }

            JsonNode? hash = current["MemoryHash"];

            /* 
             * Since we have ruled out non-static memory addresses, we can hash memory at the result
             * address to determine if some update has occurred.
             */
            if (hash is not JsonValue || hash.GetValueKind() is not JsonValueKind.Number ||
                hash.GetValue<int>() != GetMemoryHash(origin.GetValue<int>()))
            {
                return false;
            }

            /*
             * Finally, once we've validated that the memory hashes match, we can write our absolute
             * address to the out param.
             */
            results.Add(absolute);
        }

        return true;
    }

    private bool GetCachedScanOneResult(string pattern, nint start, nint end, out nint result)
    {
        result = 0;

        /*
         * We'll first need to determine if the address range is fit for caching, as in it points to
         * static memory.
         * TODO: Is there a better way to detect this?
         */
        GetBaseAddressFromScanBase(out nint @base);

        /* Once we've determined that the memory is static and we have a handle (effectively the base
         * address), we can begin formatting our key. We'll need an Fnv-1 hash of the pattern and
         * both the start and end address as relative values.
        */
        string key = $"{Utils.Utils.HashString(pattern):X8}:{start:X8}:{end:X8}";

        if (!s_cache.ContainsKey(key))
        {
            return false;
        }

        /* 
         * Beyond checking if the key exists, we now need to validate that the value is defined with
         * the proper formatting.
         */
        if (s_cache[key] is not JsonObject cached)
        {
            return false;
        }

        /*
         * The indexer returns null if the key doesn't have an associated value, so we'll need a
         * check for this.
         */
        JsonNode? relative = cached["RelativePosition"];
        if (relative is not JsonValue || relative.GetValueKind() is not JsonValueKind.Number)
        {
            return false;
        }

        var absolute = (nint)(relative.GetValue<int>());

        /*
         * In Denuvo-protected games, the executable size is pretty variable, so we will want to
         * ensure the absolute address is still in range. This also slightly protects against
         * incorrectly edited cache files.
         */
        if (absolute < start || absolute >= end)
        {
            return false;
        }
        absolute += @base;

        JsonNode? origin = cached["OriginPosition"];
        if (origin is not JsonValue || origin.GetValueKind() is not JsonValueKind.Number)
        {
            return false;
        }

        JsonNode? hash = cached["MemoryHash"];

        /* 
         * Since we have ruled out non-static memory addresses, we can hash memory at the result
         * address to determine if some update has occurred.
         */
        if (hash is not JsonValue || hash.GetValueKind() is not JsonValueKind.Number ||
            hash.GetValue<int>() != GetMemoryHash(origin.GetValue<int>()))
        {
            return false;
        }

        /*
         * Finally, once we've validated that the memory hashes match, we can write our absolute
         * address to the out param.
         */
        result = absolute;

        return true;
    }

    private int GetMemoryHash(nint start)
    {
        Span<byte> buffer = stackalloc byte[c_memoryHashMaximumSize];

        /* Ensure that the buffer is zero-initialized, as it isn't guaranteed that the `Stream` will do
         * this for us.
         */
        buffer.Clear();

        m_stream.Position = start;

        /* We want to ensure that we're not throwing any exceptions in the event a full read cannot get
         * performed, so we'll want to use `Read`.
         */
        return Utils.Utils.HashBuffer(buffer, m_stream.Read(buffer));
    }

    private bool GetBaseAddressFromProcessScanBase(ProcessStream stream, out nint @base)
    {
        /* 
         * While the start of the scan won't necessarily always be the base address, it is something we
         * can use to acquire the base address.
         */
        var start = (nint)(stream.PositionPointer - stream.Position);

        @base = 0;

        /*
         * Ensure we are reflecting the most recent state of the process, including its loaded modules.
         */
        stream.Process.Refresh();

        foreach (ProcessModule current in stream.Process.Modules)
        {
            var end = (current.BaseAddress +
                current.ModuleMemorySize);

            /* 
             * The base address of a module is equal to its handle, so technically we are returning the
             * handle.
             */
            var handle = current.BaseAddress;

            if (start >= handle && start <= end)
            {
                @base = handle;
                return true;
            }
        }

        /* If our loop never ran or we never found a matching address range, we will want to return. */
        return false;
    }

    private bool MatchBasic(ref Context context, PatternElement element)
    {
        /* 
         * Here, "Basic" refers to all wildcards and exact match requests. We'll be using this function
         * in matching children of dereferences.
         */

        /* 
         * We need to verify that we aren't outside the bounds of the specified range. This might occur
         * during dereference matches.
         */
        if (context.CurrentPosition >= context.Length && !ExtendBuffer(ref context))
        {
            return false;
        }

        if (element.Kind is PatternElementKind.Exact)
        {
            if (element.ByteValue != context.Buffer[context.CurrentPosition])
            {
                return false;
            }

            /* 
             * We can safely increase the CurrentPosition field as ScanInternal will reset it back into
             * our loop's value.
             */
            context.CurrentPosition++;

            return true;
        }

        /* 
         * If we are dealing with a full wildcard, no further checks are necessary; we are able to move
         * onto the next byte.
         */
        if (element.Kind is PatternElementKind.FullWildcard)
        {
            context.CurrentPosition++;
            return true;
        }

        if (element.Kind is PatternElementKind.LeftWildcard)
        {
            if (element.ByteValue != ((context.Buffer[context.CurrentPosition] >> 0x00) & 0x0F))
            {
                return false;
            }

            context.CurrentPosition++;

            return true;
        }

        /* 
         * Beyond right wildcards, we have no other basic types to handle. We only have dereferences to
         * handle, and they'll be handled elsewhere.
         */
        if (element.Kind is PatternElementKind.RightWildcard)
        {
            if (element.ByteValue != ((context.Buffer[context.CurrentPosition] >> 0x04) & 0x0F))
            {
                return false;
            }

            context.CurrentPosition++;

            return true;
        }

        /* 
         * This should never happen unless we're implementing a new element, but if it does this should
         * stop processing.
         */
        throw new NotImplementedException();
    }

    private bool MatchDereference(ref Context context, PatternElement element)
    {
        /*
         * We'll want to store our position before iterating over each element, as we require this when
         * retrieving the relative offset.
        */
        var origin = ((m_stream.Position - context.Length) + context.CurrentPosition);

        foreach (PatternElement current in element.Children!)
        {
            /* 
             * If any of the dereference's children fail to match, we can return early, since the whole
             * sequence needs to match to change the scan result.
             */
            if (!MatchBasic(ref context, current))
            {
                return false;
            }
        }

        var after = (nint)(origin + element.Children!.Count);

        /* 
         * We've successfully matched the dereference sequence, so we will want to dereference into the
         * result address.
         */

        m_stream.Position = origin;
        nint relative;
        /* 
         * Based on the amount of children that the dereference holds, we can auto-detect the relative
         * pointer's offset.
         */
        m_stream.ReadExactly(
            new Span<byte>(&relative, Math.Min(element.Children!.Count, sizeof(nuint))));

        m_stream.Position = after;

        nint rip = (after + relative);
        var eip = (nint)(rip & 0xFFFFFFFF);

        /* 
         * Once we have got the lower 32 bits of the addition result, we can use it for calculating the
         * absolute address.
         */
        context.MatchedPosition = (nint)((context.CurrentPosition & ~0xFFFFFFFF) | eip);

        return true;
    }

    private bool MatchPattern(ref Context context, List<PatternElement> pattern)
    {
        /* 
         * For each step we take in memory, we'll need to go through each PatternElement and compare as
         * required.
         */

        context.MatchedPosition = context.CurrentPosition;
        context.OriginPosition = context.CurrentPosition;

        foreach (PatternElement current in pattern)
        {
            /*
             * Dereferences are the only non-basic type, so we can use this as a means to detect if the
             * current element is basic.
             */
            if (current.Kind is not PatternElementKind.Dereference)
            {
                /* 
                 * When called, MatchBasic will increase the scan position for us, so we do not have to
                 * worry about that here. 
                 */
                if (!MatchBasic(ref context, current))
                {
                    context.CurrentPosition = (int)(context.OriginPosition + sizeof(byte));
                    return false;
                }
            }
            else
            {
                /* 
                 * If the dereference fails, the scan address will not be adjusted, so we don't have to
                 * worry about resetting.
                 */
                if (!MatchDereference(ref context, current))
                {
                    context.CurrentPosition = (int)(context.OriginPosition + sizeof(byte));
                    return false;
                }
            }
        }

        /* 
         * This would essentially end a scan from `ScanInternal`, so we don't have to worry about doing
         * any reverting to `CurrentPosition`; a new scan will begin.
         */
        return true;
    }

    private bool ParsePattern(string pattern, List<PatternElement> parsed)
    {
        /*
         * We only allow two layers of scope in our patterns, as we don't allow multi-level dereference
         * since they wouldn't work with our implementation.
         */
        uint depth = 0;
        List<PatternElement>?[] scopes = [parsed, null];

        /*
         * We can try to reserve an estimate of how much memory our parsed pattern will take up to save
         * on numerous reallocations.
         * TODO: Make this more memory-efficient.
         */
        parsed.EnsureCapacity(pattern.Length);

        for (int i = 0; i < (pattern.Length - c_parsePatternLookahead); i++)
        {
            ReadOnlySpan<char> current = pattern.AsSpan(i, 2);
            List<PatternElement> scope = scopes[depth]!;

            if (char.IsAsciiHexDigit(current[0]) && char.IsAsciiHexDigit(current[1]))
            {
                PatternElement hexadecimal = new(PatternElementKind.Exact,
                    uint.Parse(current, NumberStyles.HexNumber));

                scope.Add(hexadecimal);

                /*
                 * Exacts are composed of two characters, so we'll want to increase the iterator twice:
                 * one at the completion expression, and one here.
                 */
                i++;
                continue;
            }

            if (current is "??")
            {
                PatternElement wildcard = new(PatternElementKind.FullWildcard);
                scope.Add(wildcard);

                /*
                 * Wildcards are composed of two characters, so we want to increase the iterator twice:
                 * one at the completion expression, and one here.
                 */
                i++;
                continue;
            }

            if (current.StartsWith('['))
            {
                /* 
                 * Dereferences are only supported when we're dealing with data coming from memory, and
                 * so if a dereference is attempted on a generic `Stream`, end the scan.
                 */
                if (m_stream is not ProcessStream or UnmanagedMemoryStream)
                {
                    return false;
                }

                if ((++depth) >= scopes.Length)
                {
                    return false;
                }

                PatternElement dereference = new(PatternElementKind.Dereference);
                dereference.Children = new List<PatternElement>();
                dereference.Children.EnsureCapacity(pattern.Length);

                scope.Add(dereference);

                scopes[depth] = dereference.Children;

                /*
                 * No additional increment is required here, as a dereference's opening bracket uses up
                 * one character.
                 */
                continue;
            }

            if (current.StartsWith(']'))
            {
                /* There's a chance a closing brace might've been accidentally left in after removal of
                 * a dereference, so we will account for that.
                */
                if (depth == 0)
                {
                    return false;
                }

                depth--;
                continue;
            }

            if (current[0] is '?' && char.IsAsciiHexDigit(current[1]))
            {
                PatternElement left = new(PatternElementKind.LeftWildcard, uint.Parse(current.Slice(1, 1),
                    NumberStyles.HexNumber));

                scope.Add(left);

                /*
                 * Wildcards are composed of two characters, so we want to increase the iterator twice:
                 * one at the completion expression, and one here.
                 */
                i++;
                continue;
            }

            if (char.IsAsciiHexDigit(current[0]) && current[1] is '?')
            {
                PatternElement right = new(PatternElementKind.RightWildcard, uint.Parse(current.Slice(0, 1),
                    NumberStyles.HexNumber));

                scope.Add(right);

                /*
                 * Wildcards are composed of two characters, so we want to increase the iterator twice:
                 * one at the completion expression, and one here.
                 */
                i++;
                continue;
            }
        }

        return true;
    }

    private bool ScanInternal(ref Context context, string pattern)
    {
        /* 
         * First, we will need to parse the pattern given to us. Without it, this whole function can't
         * continue to execute.
         */
        List<PatternElement> parsed = new();

        if (!ParsePattern(pattern, parsed))
        {
            return false;
        }

        m_stream.Position = context.Start;

        /* 
         * We will be using a buffer that can fit four pages of memory, but if needed, adjust this for
         * additional pages.
         */
        context.Buffer = new byte[
            4 * 4096];
        ExtendBuffer(ref context);

        /*
         * Once we've got our parsed pattern and our necessary variables, we can begin scanning within
         * the provided memory range.
         */
        while (context.Length is not 0)
        {
            if (MatchPattern(ref context, parsed))
            {
                context.MatchedPosition += (nint)(m_stream.Position - context.Length);
                return true;
            }
        }

        return false;
    }

    private void SetCachedScanAllResult(ref Context context, string pattern, List<UnbasedScan> results)
    {
        /* 
         * We want to avoid caching addresses that could not match anything, as this can invoke access
         * violations at runtime.
         */
        if (results.Count is 0)
        {
            return;
        }

        /*
         * Once we've determined that the memory is static and we have a handle (effectively the base
         * address), we can begin formatting our key. We'll need an Fnv-1 hash of the pattern and
         * both the start and end address as relative values.
         */
        string key = $"{Utils.Utils.HashString(pattern):X8}:{context.Start:X8}:{context.End:X8}";

        JsonArray cached = new();
        s_cache[key] = cached;

        /* There's very little error checking we need to perform here, since we're overwriting things
         * that may be invalid already (if there is invalid data).
        */
        foreach (UnbasedScan current in results)
        {
            JsonObject definition = new();
            cached.Add(definition);

            definition["MemoryHash"] = GetMemoryHash(current.OriginPosition);
            definition["OriginPosition"] = current.OriginPosition;
            definition["RelativePosition"] = current.MatchedPosition;
        }

        using StreamWriter writer = new(Path.Combine(Utils.Utils.BaseDirectory, "scan_cache.json"));
        writer.Write(JsonSerializer.Serialize(s_cache, s_options));
    }

    private void SetCachedScanOneResult(ref Context context, string pattern)
    {
        /*
         * Once we've determined that the memory is static and we have a handle (effectively the base
         * address), we can begin formatting our key. We'll need an Fnv-1 hash of the pattern and
         * both the start and end address as relative values.
         */
        string key = $"{Utils.Utils.HashString(pattern):X8}:{context.Start:X8}:{context.End:X8}";

        JsonObject cached = new();
        s_cache[key] = cached;

        /* There's very little error checking we need to perform here, since we're overwriting things
         * that may be invalid already (if there is invalid data).
        */
        cached["MemoryHash"] = GetMemoryHash(context.OriginPosition);
        cached["OriginPosition"] = context.OriginPosition;
        cached["RelativePosition"] = context.MatchedPosition;

        using StreamWriter writer = new(Path.Combine(Utils.Utils.BaseDirectory, "scan_cache.json"));
        writer.Write(JsonSerializer.Serialize(s_cache, s_options));
    }

    /// <summary>
    /// Performs all static initialization required for <see cref="PatternScanner"/> instances.
    /// </summary>
    public static void Initialize()
    {
        /* 
         * It's best that we do this initialization manually, as static constructors would require us
         * to rely on an undefined order of initialization.
         */
        string path = Path.Combine(Utils.Utils.BaseDirectory, "scan_cache.json");

        if (!File.Exists(path))
        {
            return;
        }

        /* 
         * We can now stream in our cache, but we'll have to ensure the data was not modified into an
         * unsupported format.
         */
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read);

        /*
         * There's quite a bit that can go wrong in parsing, i.e a somehow corrupted JSON file, so we
         * will catch exceptions.
         */
        try
        {
            JsonNode? node = JsonSerializer.Deserialize<JsonNode>(stream, s_options);
            /* TODO: Try to figure out when this would be returning a `null` value. */
            if (node is JsonObject @object)
            {
                s_cache = @object;
            }
            else
            {
                throw new JsonException();
            }
        }
        catch (JsonException)
        {
            /* We've already assigned a default value for `s_cache`, so we can safely return here. */
            return;
        }
    }

    public void Dispose() => m_stream.Dispose();

    /// <summary>
    /// Scans the specified address range for sequences of bytes matching the provided pattern.
    /// </summary>
    /// <param name="pattern">
    /// The pattern, which may be composed of any of the following:
    /// - Dereferences:          [01 02 03 04]
    /// - Exact Matches:          01 02 03 04
    /// - Partial/Full Wildcards: ?1 ?? ?? 0? 
    /// </param>
    /// <param name="start">The beginning of the address range to scan.</param>
    /// <param name="end">The end of the address range to scan.</param>
    /// <param name="cache">
    /// Whether or not the cache should be used in this scan. If true, the cache is first checked for
    /// existing results. If no results are found, a scan will be performed and its results will be
    /// cached.
    /// </param>
    /// <returns>
    /// The addresses of matching sequences of bytes if found. Otherwise, an empty vector and nothing
    /// will be cached.
    /// </returns>
    public List<nint> ScanAll(string pattern, nint start, nint end, bool cache = true)
    {
        /* 
         * We might receive calls from different threads simultaneously (i.e. calls coming from hooks
         * and the editor), so we'll use synchronization.
         */
        lock (s_cache)
        {
            List<nint> result = new();

            if (pattern is "" || start >= end)
            {
                return result;
            }

            /* 
             * If specified, we'll want to attempt to retrieve a past result from the cache. If there
             * isn't one, begin a new scan and cache it.
             */
            if (cache && GetCachedScanAllResult(pattern, start, end, result))
            {
                return result;
            }

            Context context = new();
            context.End = end;
            context.Start = start;

            List<UnbasedScan> unbased = new();
            while (ScanInternal(ref context, pattern))
            {
                /*
                 * We will need to increment the scan position so as to not repeatedly match the same
                 * address. We shouldn't have to worry about bounds
                 * checking, as ScanInternal implicitly bounds checks the scan position in its loop.
                 */
                context.Start = (context.MatchedPosition + sizeof(byte));
                unbased.Add(new UnbasedScan(context.MatchedPosition, context.OriginPosition));
            }

            /*
             * We've modified `Start` in the loop, so we'll need to reset it for the cache key to be
             * formatted correctly.
             */
            context.Start = start;

            if (cache)
            {
                SetCachedScanAllResult(ref context, pattern,
                    unbased);
            }

            result.EnsureCapacity(unbased.Count);

            /*
             * The SetCachedScan functions automatically determine if the address is fit for caching,
             * so we can return.
             */
            if (!GetBaseAddressFromScanBase(
                out nint @base))
            {
                foreach (UnbasedScan current in unbased)
                {
                    result.Add(current.MatchedPosition);
                }
            }
            else
            {
                /*
                 * If there's an available base address we'll want to add it to each relative address
                 * here.
                 */
                foreach (UnbasedScan current in unbased)
                {
                    result.Add(current.MatchedPosition + @base);
                }
            }

            /*
             * Now that all of the addresses are based, we can return the offsets. We have cached the
             * relative versions already.
             */
            return result;
        }
    }

    /// <summary>
    /// Scans the full address range for sequences of bytes matching the provided pattern.
    /// </summary>
    /// <param name="pattern">
    /// The pattern, which may be composed of any of the following:
    /// - Dereferences:          [01 02 03 04]
    /// - Exact Matches:          01 02 03 04
    /// - Partial/Full Wildcards: ?1 ?? ?? 0? 
    /// </param>
    /// <param name="cache">
    /// Whether or not the cache should be used in this scan. If true, the cache is first checked for
    /// existing results. If no results are found, a scan will be performed and its results will be
    /// cached.
    /// </param>
    /// <returns>
    /// The addresses of matching sequences of bytes if found. Otherwise, an empty vector and nothing
    /// will be cached.
    /// </returns>
    public List<nint> ScanAll(string pattern, bool cache = true) =>
        ScanAll(pattern, 0, (nint)(m_stream.Length), cache);

    /// <summary>
    /// Scans the specified address range for a sequence of bytes matching the provided pattern.
    /// </summary>
    /// <param name="pattern">
    /// The pattern, which may be composed of any of the following:
    /// - Dereferences:          [01 02 03 04]
    /// - Exact Matches:          01 02 03 04
    /// - Partial/Full Wildcards: ?1 ?? ?? 0?
    /// </param>
    /// <param name="start">The beginning of the address range to scan.</param>
    /// <param name="end">The end of the address range to scan.</param>
    /// <param name="cache">
    /// Whether or not the cache should be used in this scan. If true, the cache is first checked for
    /// existing results. If no results are found, a scan will be performed and its results will be
    /// cached.
    /// </param>
    /// <returns>
    /// The addresses of matching sequences of bytes if found. Otherwise, an empty vector and nothing
    /// will be cached.
    /// </returns>
    public nint ScanOne(string pattern, nint start, nint end, bool cache = true)
    {
        /* 
         * We might receive calls from different threads simultaneously (i.e. calls coming from hooks
         * and the editor), so we'll use synchronization.
         */
        lock (s_cache)
        {
            nint result = 0;

            if (pattern is "" || start >= end)
            {
                return result;
            }

            /* 
             * If specified, we'll want to attempt to retrieve a past result from the cache. If there
             * isn't one, begin a new scan and cache it.
             */
            if (cache && GetCachedScanOneResult(pattern, start, end, out result))
            {
                return result;
            }

            Context context = new();
            context.End = end;
            context.Start = start;

            if (!ScanInternal(ref context, pattern))
            {
                return result;
            }

            if (cache)
            {
                SetCachedScanOneResult(ref context, pattern);
            }

            result = context.MatchedPosition;
            if (GetBaseAddressFromScanBase(out nint @base))
            {
                result += @base;
            }

            /*
             * The SetCachedScan functions automatically determine if the address is fit for caching,
             * so we can return.
             */
            return result;
        }
    }

    /// <summary>
    /// Scans the full address range for a sequence of bytes matching the provided pattern.
    /// </summary>
    /// <param name="pattern">
    /// The pattern, which may be composed of any of the following:
    /// - Dereferences:          [01 02 03 04]
    /// - Exact Matches:          01 02 03 04
    /// - Partial/Full Wildcards: ?1 ?? ?? 0?
    /// </param>
    /// <param name="cache">
    /// Whether or not the cache should be used in this scan. If true, the cache is first checked for
    /// existing results. If no results are found, a scan will be performed and its results will be
    /// cached.
    /// </param>
    /// <returns>
    /// The addresses of matching sequences of bytes if found. Otherwise, an empty vector and nothing
    /// will be cached.
    /// </returns>
    public nint ScanOne(
        string pattern, bool cache = true) => ScanOne(pattern, 0, (nint)(m_stream.Length), cache);
}
