using Avalonia.Media;
using Avalonia.Platform;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Frosty.Ui.Extensions;

internal static class IFontManagerImplExtensions
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = nameof(TryCreateGlyphTypeface))]
    private static extern bool InternalTryCreateGlyphTypeface(IFontManagerImpl reserved, Stream stream,
        FontSimulations fontSimulations, [NotNullWhen(returnValue: true)] out IGlyphTypeface? glyphTypeface);

    public static bool TryCreateGlyphTypeface(this IFontManagerImpl extended, Stream stream,
        FontSimulations fontSimulations, [NotNullWhen(returnValue: true)] out IGlyphTypeface? glyphTypeface)
    {
        return InternalTryCreateGlyphTypeface(extended, stream, fontSimulations, out glyphTypeface);
    }
}
