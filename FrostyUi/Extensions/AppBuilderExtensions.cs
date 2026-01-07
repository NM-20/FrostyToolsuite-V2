using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Frosty.Ui.Media;

namespace Frosty.Ui.Extensions;

/// <summary>
/// Provides extended functionality to the <see cref="AppBuilder"/> class.
/// </summary>
public static class AppBuilderExtensions
{
    /// <summary>
    /// Enables Skia's `SaveLayer` API as a means of handling opacity. This can correct issues with `SvgImage`
    /// not responding to opacity changes.
    /// </summary>
    /// <param name="extended">Reserved.</param>
    /// <returns>The <see cref="AppBuilder"/> to chain function calls.</returns>
    public static AppBuilder WithOpacitySaveLayer(this AppBuilder extended) =>
        extended.With(new SkiaOptions { UseOpacitySaveLayer = true });

    /// <summary>
    /// Registers a <see cref="FontCollectionBase"/> implementation that resolves and caches fonts at runtime.
    /// This pulls fonts not only from internal sources, but also from the disk.
    /// </summary>
    /// <param name="extended">Reserved.</param>
    /// <returns>The <see cref="AppBuilder"/> to chain function calls.</returns>
    public static AppBuilder WithRuntimeFontResolver(this AppBuilder extended) =>
        extended.ConfigureFonts((FontManager manager) => manager.AddFontCollection(new RuntimeFontResolver()));
}
