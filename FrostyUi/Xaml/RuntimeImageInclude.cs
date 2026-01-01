using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Metadata;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Svg.Skia;
using Frosty.Sdk.Utils;
using Frosty.Ui.Managers;
using System.Collections.Specialized;

namespace Frosty.Ui.Xaml;

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
        string external = Path.Join(Utils.BaseDirectory,
            (Source!.IsAbsoluteUri ? Source!.AbsolutePath : Source!.OriginalString));

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
            "Str_Ui_RuntimeImageSourceLoadException"), Key, exception);
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
        SvgSource loaded;
        try
        {
            loaded = SvgSource.LoadFromStream(stream);
        }
        catch (Svg.SvgException svg)
        {
            ShowLoadException(svg);
            return null;
        }
        return new SvgImage { Source = loaded };
    }
}

/// <summary>
/// Provides a mechanism to include images not only from an internal source, but from the disk as well.
/// </summary>
public class RuntimeImageInclude : ResourceProvider
{
    /*
     * Internally, we build a `ResourceDictionary` containing the resolved sources as they are added.
     */
    private ResourceDictionary m_dictionary = new();

    public override bool HasResources => m_dictionary.HasResources;

    /// <summary>
    /// The sources that the <see cref="RuntimeImageInclude"/> should pull from for images. Each instance
    /// can be a relative <see cref="Uri"/>, albeit it will only allow for external sources.
    /// </summary>
    [Content]
    public AvaloniaList<RuntimeImageSource> Sources { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeImageInclude"/> class.
    /// </summary>
    public RuntimeImageInclude() => Sources.CollectionChanged += Sources_CollectionChanged;

    private void BuildDictionary()
    {
        /*
         * This may be called multiple times if the user makes changes to the dictionary after it has
         * been loaded by AXAML, so we'll clear it to be safe.
         */
        m_dictionary.Clear();

        foreach (RuntimeImageSource current in Sources)
        {
            m_dictionary.Add(current.Key, current.Get());
        }
    }

    private void Sources_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        BuildDictionary();

    public override bool TryGetResource(object key,
        ThemeVariant? theme, out object? value) => m_dictionary.TryGetResource(key, null, out value);
}