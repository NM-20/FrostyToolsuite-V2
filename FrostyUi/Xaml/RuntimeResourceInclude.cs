using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Metadata;
using Avalonia.Styling;
using Frosty.Sdk.Utils;
using Frosty.Ui.Managers;

namespace Frosty.Ui.Xaml;

/// <summary>
/// Provides a mechanism to include resources from not only an internal source, but from the disk as well.
/// </summary>
public class RuntimeResourceInclude : ResourceProvider
{
    private ResourceProvider? m_provider;

    public override bool HasResources =>
        (Loaded?.HasResources ?? false);

    /// <summary>
    /// The <see cref="RuntimeResourceInclude"/>'s resolved <see cref="ResourceDictionary"/>, which varies
    /// based on its <see cref="Source"/>.
    /// </summary>
    public ResourceProvider? Loaded => GetResourceProvider();

    /// <summary>
    /// The location at which the <see cref="ResourceDictionary"/> is found. For both internal sources and
    /// sources on the disk, this should be in the format of an `avares` URI.
    /// </summary>
    [Content]
    public Uri? Source
    {
        get;
        set;
    }

    private void InvalidateSource() => Source = null;

    private ResourceProvider? LoadExternal(string path)
    {
        /*
         * `AvaloniaRuntimeXamlLoader` can throw exceptions, so we'll want to ensure we are catching these
         * to make debugging easier for the user.
         */
        object loaded;
        try
        {
            loaded = AvaloniaRuntimeXamlLoader.Load(new FileStream(path, FileMode.Open));
        }
        catch (XamlLoadException exception)
        {
            string message = string.Format(LocalizationManager.Instance.GetString("Str_Ui_RuntimeResourceIncludeExternalException"),
                path, exception);
            MessageBoxW(0, message, LocalizationManager.Instance.GetString("Str_Global_ProgramTitle"), (MB_ICONWARNING | MB_OK));

            InvalidateSource();

            return null;
        }

        if (loaded is not ResourceProvider provider)
        {
            InvalidateSource();
            return null;
        }

        m_provider = provider;

        /* 
         * Since we've stored the dictionary, this should only be called once, at least for this instance.
         */
        return m_provider;
    }

    private ResourceProvider? LoadInternal(Uri source)
    {
        /*
         * While internal exceptions aren't as likely, we'll ensure that we handle them just in case, i.e.
         * in the event that we're dealing with inclusions from within a plugin.
         */
        if (!source.IsAbsoluteUri)
        {
            InvalidateSource();
            return null;
        }

        object loaded;
        try
        {
            loaded = AvaloniaXamlLoader.Load(source);
        }
        catch (XamlLoadException exception)
        {
            string message = string.Format(LocalizationManager.Instance.GetString("Str_Ui_RuntimeResourceIncludeInternalException"),
                source, exception);
            MessageBoxW(0, message, LocalizationManager.Instance.GetString("Str_Global_ProgramTitle"), (MB_ICONWARNING | MB_OK));

            InvalidateSource();

            return null;
        }

        if (loaded is not ResourceProvider provider)
        {
            InvalidateSource();
            return null;
        }

        m_provider = provider;

        /* 
         * Since we've stored the dictionary, this should only be called once, at least for this instance.
         */
        return m_provider;
    }

    private ResourceProvider? GetResourceProvider()
    {
        if (m_provider is not null)
        {
            return m_provider;
        }

        if (Source is null)
        {
            /* 
             * This is a user-facing API (users can define new `ResourceDictionary` instances via external
             * XAML), so we won't be throwing exceptions.
             */
            return null;
        }

        /*
         * In the event that a provided URI is not absolute, `OriginalString` will net us a relative path
         */
        string path = Path.Join(Utils.BaseDirectory, (Source.IsAbsoluteUri ? Source.AbsolutePath :
            Source.OriginalString));

        /*
         * Otherwise, we'll want to determine where the `ResourceDictionary` is located. If it's placed in
         * an assembly, we can use `AvaloniaXamlLoader`. Otherwise, `AvaloniaRuntimeXamlLoader`.
         */
        if (File.Exists(path))
        {
            return LoadExternal(path);
        }
        else
        {
            /*
             * TODO: Is there a way to check if compiled AXAML exists within a particular assembly? Right
             * now we're relying on exceptions to tell us whether or not it exists.
             */
            return LoadInternal(Source);
        }
    }

    public override bool TryGetResource(object key, ThemeVariant? theme, out object? value)
    {
        if (Loaded is not null)
        {
            return Loaded.TryGetResource(key, null, out value);
        }

        /* 
         * Otherwise, we will have to output null and return false to indicate the resource doesn't exist.
         */
        value = null;
        return false;
    }
}
