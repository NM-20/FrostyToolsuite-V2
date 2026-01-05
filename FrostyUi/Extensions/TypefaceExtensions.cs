using Avalonia.Media;
using Avalonia.Media.Fonts;
using System.Runtime.CompilerServices;

namespace Frosty.Ui.Extensions;

internal static class TypefaceExtensions
{
    /*
     * This is essentially meant to provide temporary access to the `Normalize` API until it is included within a release. Once it is, simply delete this class
     * and there should be no errors.
     */

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod)]
    private static extern Typeface GetImplicitTypeface(FontCollectionBase? reserved, Typeface typeface, out string normalizedFamilyName);

    public static Typeface Normalize(this Typeface extended, out string normalizedFamilyName) => GetImplicitTypeface(null, extended, out normalizedFamilyName);
}
