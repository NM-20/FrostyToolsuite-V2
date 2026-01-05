using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Metadata;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Svg.Skia;
using Frosty.Sdk.Utils;
using Frosty.Ui.Managers;

namespace Frosty.Ui.Media;

/// <summary>
/// Represents an image source for <see cref="RuntimeImageInclude"/> instances within XAML to pull from.
/// </summary>
public abstract class RuntimeImageSource
{
    /// <summary>
    /// A unique identifier for this source that will be used for creating `DynamicResources` within
    /// <see cref="RuntimeImageInclude"/>.
    /// </summary>
    public string Key
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// The location at which the image exists. This can be a relative <see cref="Uri"/>, albeit it will
    /// only allow for external sources.
    /// </summary>
    [Content]
    public Uri? Source
    {
        get;
        set;
    }

    private object? GetExternal()
    {
        /*
         * We assume that `Source` is not null here since our `Get` method checks if `Source` is `null`.
         */
        string external = Path.Join(Utils.BaseDirectory, Source!.ToString());

        if (!File.Exists(external))
        {
            return null;
        }

        return GetFromStream(new FileStream(external, FileMode.Open, FileAccess.Read));
    }

    private object? GetInternal() =>
        (AssetLoader.Exists(Source!) ? GetFromStream(AssetLoader.Open(Source!)) : null);

    protected abstract object? GetFromStream(Stream stream);

    protected void ShowLoadException(Exception exception)
    {
        string message = string.Format(LocalizationManager.Instance.GetString(
            "Str_Ui_RuntimeImageSource_LoadException"), Key, exception);
        MessageBoxW(0, message,
            LocalizationManager.Instance.GetString("Str_Global_ProgramTitle"), (MB_ICONWARNING | MB_OK));
    }

    /// <summary>
    /// Gets the 
    /// </summary>
    /// <returns></returns>
    public object? Get() => (Source is not null ? (GetExternal() ?? GetInternal()) : default);
}

public sealed class RuntimeBitmapSource : RuntimeImageSource
{
    /* 
     * TODO: Try to figure out what exceptions are thrown within the `Bitmap` constructor.
     */
    protected override object? GetFromStream(Stream stream) => new Bitmap(stream);
}

public sealed class RuntimeIcoSource : RuntimeImageSource
{
    /* 
     * TODO: Try to figure out what exceptions are thrown within the `WindowIcon` constructor.
     */
    protected override object? GetFromStream(Stream stream) => new WindowIcon(stream);
}

public sealed class RuntimeSvgSource : RuntimeImageSource
{
    protected override object? GetFromStream(Stream stream)
    {
        try
        {
            /*
             * We avoid creating an `SvgImage` automatically since this would prevent us from assigning
             * `Css` as we can when constructing `SvgImage` manually in XAML.
             */
            return SvgSource.LoadFromStream(stream);
        }
        catch (Svg.SvgException svg)
        {
            ShowLoadException(svg);

            /*
             * Returning `null` should be fine, since resource providers typically support null values.
             */
            return null;
        }
    }
}

/// <summary>
/// Provides a mechanism to include images not only from an internal source, but from the disk as well.
/// </summary>
public class RuntimeImageInclude : ResourceProvider
{
    /*
     * Internally we manage a `ResourceDictionary` containing the resolved sources as they're discovered.
     */
    private ResourceDictionary? m_dictionary;

    public override bool HasResources => (m_dictionary?.HasResources ?? false);

    /// <summary>
    /// The sources that the <see cref="RuntimeImageInclude"/> should pull from for images. Each instance
    /// can be a relative <see cref="Uri"/>, albeit it will only allow for external sources.
    /// </summary>
    [Content]
    public AvaloniaList<RuntimeImageSource> Sources { get; } = new();

    private ResourceDictionary BuildDictionary()
    {
        /*
         * `RuntimeImageInclude` instances become immutable once they've been accessed once, so we will
         * want to block rebuilding.
         */
        if (m_dictionary is not null)
        {
            return m_dictionary;
        }

        m_dictionary = new ResourceDictionary();

        foreach (RuntimeImageSource current in Sources)
        {
            m_dictionary.Add(current.Key, current.Get());
        }

        /*
         * Finally, return the `ResourceDictionary` we built. Once this method has executed once, it'll
         * always return this.
         */
        return m_dictionary;
    }

    public override bool TryGetResource(object key,
        ThemeVariant? theme, out object? value) => BuildDictionary().TryGetResource(key, null, out value);
}