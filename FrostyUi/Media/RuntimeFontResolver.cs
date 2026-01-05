using Avalonia.Media;
using Avalonia.Media.Fonts;
using Avalonia.Platform;
using Frosty.Sdk.Utils;
using Frosty.Ui.Extensions;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Frosty.Ui.Media;

/// <summary>
/// Represents a <see cref="FontCollectionBase"/> that can fetch from both external and internal locations.
/// </summary>
internal sealed class RuntimeFontResolver : FontCollectionBase
{
    /*
     * This implementation references maxkatz6's `StreamFontCollection` implementation:
     * https://github.com/AvaloniaUI/Avalonia/discussions/19751#discussioncomment-14573546
     */

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = nameof(TryGetNearestMatch))]
    private static extern bool InternalTryGetNearestMatch(FontCollectionBase? reserved,
        ConcurrentDictionary<FontCollectionKey, IGlyphTypeface?> glyphTypefaces, FontCollectionKey key,
        [NotNullWhen(true)] out IGlyphTypeface? glyphTypeface);

    /*
     * When a font needs to be requested via the `RuntimeFontResolver`, the `Uri` should start with this.
     */
    private static readonly Uri s_key = new("fonts:Runtime");

    /*
     * Initially, this will be empty. As fonts are requested, however, this collection will be populated to
     * cache them.
     */
    private readonly List<FontFamily> m_families = new();

    private IFontManagerImpl? m_manager;

    public override int Count => m_families.Count;

    public override Uri Key => s_key;

    private static bool TryGetNearestMatch(
        ConcurrentDictionary<FontCollectionKey, IGlyphTypeface?> glyphTypefaces, FontCollectionKey key,
        [NotNullWhen(true)] out IGlyphTypeface? glyphTypeface)
    {
        return InternalTryGetNearestMatch(null, glyphTypefaces, key, out glyphTypeface);
    }

    public override IEnumerator<FontFamily> GetEnumerator() => m_families.GetEnumerator();

    /*
     * We load fonts as they are requested, so we'll simply want to store the passed `IFontManagerImpl` for
     * later.
     */
    public override void Initialize(IFontManagerImpl fontManager) => m_manager = fontManager;

    public override FontFamily this[int index] => m_families[index];

    private bool FindPartialMatch(string name, FontCollectionKey key, [NotNullWhen(true)] out IGlyphTypeface? typeface)
    {
        /*
         * Like `TryGetGlyphTypeface`, this method also accesses private APIs that will be made public, and
         * so we'll need to remove the `UnsafeAccessor` defined above when appropriate.
         */
        foreach (FontFamily current in m_families)
        {
            if (!current.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            /*
             * If we've found a family that at least starts with the given family name, we might be able to
             * use it as a potential fallback.
             */
            if (_glyphTypefaceCache.TryGetValue(current.Name, out ConcurrentDictionary<FontCollectionKey,
                IGlyphTypeface?>? value) && TryGetNearestMatch(value, key, out typeface))
            {
                return true;
            }
        }

        typeface = null;

        /*
         * Otherwise, we didn't locate a partial match, so we will have to output `null` as our located `typeface`.
         */
        return false;
    }

    private Stream? GetExternalStream(string external) => ((File.Exists(external)) ?
        new FileStream(external, FileMode.Open, FileAccess.Read) : null);

    private Stream? GetInternalStream(Uri uri) => ((uri.IsAbsoluteUri && AssetLoader.Exists(uri)) ?
        AssetLoader.Open(uri) : null);

    public override bool TryGetGlyphTypeface(
        string familyName, FontStyle style,
        FontWeight weight, FontStretch stretch, [NotNullWhen(true)] out IGlyphTypeface? glyphTypeface)
    {
        /*
         * TODO: Right now, we're really accessing `GetImplicitTypeface`, which is a private API equivalent
         * to `Normalize`. Once this API is publicly available, remove `TypefaceExtensions`.
         */
        Typeface typeface = new Typeface(familyName, style, weight, stretch).Normalize(out familyName);

        stretch = typeface.Stretch;
        style   = typeface.Style;
        weight  = typeface.Weight;

        /*
         * Before we attempt to load a new typeface, we'll want to determine if we are in possession of any
         * cached instances.
         * If we aren't, check if the font exists either on the disk or internally. If it doesn't, fallback
         * to a partial match.
         */
        FontCollectionKey key = new(style, weight, stretch);
        if (_glyphTypefaceCache.TryGetValue(
            familyName, out ConcurrentDictionary<FontCollectionKey, IGlyphTypeface?>? glyphTypefaces))
        {
            /*
             * TODO: Both maxkatz6's implementation and the original implementation also do a quick compare
             * with `glyphTypeface` against `null`; try to figure out why.
             */
            if (glyphTypefaces.TryGetValue(key, out glyphTypeface) && glyphTypeface is not null)
            {
                return true;
            }
        }

        /*
         * We weren't able to locate a cached typeface, so we'll want to treat this as a "request" and load
         * the font here.
         * URIs can be either relative or absolute. If the former, the source will always be loaded through
         * an external means.
         */
        if (!Uri.TryCreate(familyName, UriKind.RelativeOrAbsolute, out Uri? uri))
        {
            return FindPartialMatch(familyName, key, out glyphTypeface);
        }

        /*
         * If we've verified that we've received a valid URI from `familyName` (the fragment part), we will
         * want to combine the relative part with our base directory to determine if an external version is
         * on the disk.
         */
        string external = Path.Join(Utils.BaseDirectory, (uri.IsAbsoluteUri ? uri.AbsolutePath : uri.ToString()));

        using Stream? stream = (GetExternalStream(external) ?? GetInternalStream(uri));

        if (stream is null)
        {
            return FindPartialMatch(familyName, key, out glyphTypeface);
        }

        /*
         * `TryCreateGlyphTypeface` should not throw any exceptions and should instead provide us a bool to
         * inform us of a failure, so we shouldn't need a `try` block.
         */
        if (!m_manager!.TryCreateGlyphTypeface(stream, FontSimulations.None, out glyphTypeface))
        {
            return FindPartialMatch(familyName, key, out glyphTypeface);
        }

        /*
         * Before we return the discovered typeface, ensure that we have cached it for the next request we
         * receive.
         */
        ConcurrentDictionary<FontCollectionKey, IGlyphTypeface?> typefaces = _glyphTypefaceCache.GetOrAdd(familyName, (_) =>
        {
            m_families.Add(new FontFamily(s_key, familyName));
            return new ConcurrentDictionary<FontCollectionKey, IGlyphTypeface?>();
        });

        typefaces.TryAdd(key, glyphTypeface);

        /*
         * The next time the same font is requested, we should be instead pulling it via our internal cache
         * and not reloading it.
         */
        return true;
    }
}
