using Avalonia.Markup.Xaml;
using Avalonia.Metadata;
using Avalonia.Styling;
using Frosty.Sdk.Utils;
using Frosty.Ui.Managers;

namespace Frosty.Ui.Media;

/// <summary>
/// Provides a mechanism to include styles from not only an internal source, but through the disk as well.
/// </summary>
public sealed class RuntimeStyleInclude : StyleBase
{
    private Uri?    m_source;
    private IStyle? m_style;

    /// <summary>
    /// The <see cref="RuntimeStyleInclude"/>'s resolved <see cref="IStyle"/>, which varies depending on
    /// its <see cref="Source"/>.
    /// </summary>
    public IStyle? Loaded => GetStyle();

    /// <summary>
    /// The location at which the <see cref="IStyle"/> is found. For both internal sources and sources on
    /// the disk, this should be in the format of an `avares` URI.
    /// </summary>
    [Content]
    public Uri? Source
    {
        get
        {
            return m_source;
        }
        set
        {
            /*
             * TODO: Once `IStyle` becomes client implementable, move back to a traditional auto property.
             */
            if (m_source == value)
            {
                return;
            }

            m_source = value;

            /*
             * Ensure that we call `GetStyle` to allow `m_style` to be assigned if it hasn't been already.
             */
            GetStyle();
        }
    }

    private void InvalidateSource() => m_source = null;

    private void OnException(string source, Exception exception)
    {
        string message = string.Format(LocalizationManager.Instance.GetString("Str_Ui_RuntimeStyleInclude_LoadException"),
            source, exception);
        MessageBoxW(0, message, LocalizationManager.Instance.GetString("Str_Global_ProgramTitle"), (MB_ICONWARNING | MB_OK));
    }

    private IStyle? LoadExternal()
    {
        /*
         * `AvaloniaRuntimeXamlLoader` can throw exceptions, so we'll want to ensure we are catching these
         * to make debugging easier for the user.
         */
        string external = Path.Join(Utils.BaseDirectory, (Source!.IsAbsoluteUri ? Source!.AbsolutePath : Source!.ToString()));

        if (!File.Exists(external))
        {
            return null;
        }

        object loaded;
        try
        {
            loaded = AvaloniaRuntimeXamlLoader.Load(new FileStream(external, FileMode.Open));
        }
        catch (XamlLoadException exception)
        {
            OnException(external, exception);
            return null;
        }

        if (loaded is not IStyle style)
        {
            return null;
        }

        m_style = style;
        Children.Add(m_style);

        /* 
         * Since we've stored the dictionary, this should only be called once, at least for this instance.
         */
        return m_style;
    }

    private IStyle? LoadInternal()
    {
        /*
         * While internal exceptions aren't as likely, we'll ensure that we handle them just in case, i.e.
         * in the event that we're dealing with inclusions from within a plugin.
         */
        if (!Source!.IsAbsoluteUri)
        {
            /*
             * We exclusively invalidate sources here, since invalidating them within `LoadExternal` would
             * break `LoadInternal`.
             */
            InvalidateSource();
            return null;
        }

        object loaded;
        try
        {
            loaded = AvaloniaXamlLoader.Load(Source!);
        }
        catch (XamlLoadException exception)
        {
            OnException(Source!.ToString(), exception);
            InvalidateSource();
            return null;
        }

        if (loaded is not IStyle style)
        {
            InvalidateSource();
            return null;
        }

        m_style = style;
        Children.Add(m_style);

        /* 
         * Since we've stored the style, this should only be called once, (at least within this instance).
         */
        return m_style;
    }

    private IStyle? GetStyle()
    {
        if (m_style is not null)
        {
            return m_style;
        }

        if (Source is null)
        {
            /* 
             * This is a user-facing API (users can define new instances of `IStyle`-derived classes using
             * external XAML), so we won't be throwing exceptions.
             */
            return null;
        }

        /*
         * Otherwise, we'll want to determine where the `IStyle` is located. If it's placed in an assembly
         * we can use `AvaloniaXamlLoader`. Otherwise, `AvaloniaRuntimeXamlLoader`.
         */
        return (LoadExternal() ?? LoadInternal());
    }
}
