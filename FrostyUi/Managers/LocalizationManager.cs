using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Reflection;

namespace Frosty.Ui.Managers;

/// <summary>
/// Represents a source of localized strings to be pulled from within the <see cref="LocalizationManager"/>.
/// </summary>
public struct LocalizationSource
{
    /// <summary>
    /// Describes an internal source to pull localization from.
    /// </summary>
    /// <param name="Assembly">
    /// The <see cref="Assembly"/> containing the localization.
    /// </param>
    /// <param name="ResourcePath">
    /// The period-delimitted path to the localization directory.
    /// This should match the format used in <see cref="Assembly.GetManifestResourceStream(string)"/>.
    /// </param>
    public record struct InternalSource(Assembly Assembly, string ResourcePath)
    {
        /// <summary>
        /// Formats the <see cref="ResourcePath"/> to point to a particular <paramref name="filename"/>.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <returns>The formatted comma-delimitted path.</returns>
        public string FormatResourcePath(string filename) =>
            ResourcePath.EndsWith('.') ? $"{ResourcePath}{filename}" : $"{ResourcePath}.{filename}";
    }

    /// <summary>
    /// The external source to pull localization from. If this path does not exist on the disk, the manager
    /// will use <see cref="Internal"/> as a fallback.
    /// </summary>
    public string? External;

    /// <summary>
    /// The internal source to pull localization from in the event that <see cref="External"/> is missing.
    /// </summary>
    public InternalSource Internal;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizationSource"/> structure with the given parameters.
    /// </summary>
    /// <param name="external">See <see cref="External"/>.</param>
    /// <param name="internal">See <see cref="Internal"/>.</param>
    public LocalizationSource(string? external, InternalSource @internal)
    {
        External = external;
        Internal = @internal;
    }

    private Stream GetInternalStream(string name) => Internal.Assembly.GetManifestResourceStream(Internal.
        FormatResourcePath($"{name}.axaml"))!;

    internal Stream GetResourceStream(string name)
    {
        /* 
         * `External` can be specified as `null` or an empty string to default to always using the internal
         * stream.
         */
        if (string.IsNullOrEmpty(External))
        {
            return GetInternalStream(name);
        }

        string external = Path.Combine(
            External, name);
        if (File.Exists(external))
        {
            return new FileStream(external,
                FileMode.Open);
        }
        else
        {
            return GetInternalStream(name);
        }
    }
}

/// <summary>
/// Provides access to language-dependent strings that are indexed through a particular string identifier.
/// </summary>
public static class LocalizationManager
{
    /* 
     * We must keep track of the `ResourceDictionary` we add, so that we can remove it when it is necessary.
     * This will always get assigned in the constructor.
     */
    private static Application? s_application;
    private static ResourceDictionary s_dictionary = new();

    /// <summary>
    /// A collection of <see cref="LocalizationSource"/> instances that will be pulled from when switching
    /// locales.
    /// </summary>
    public static List<LocalizationSource>? Sources
    {
        get;
        private set;
    }

    private static void LoadResource(Stream stream)
    {
        /* 
         * Otherwise, we should be able to proceed with loading the external XAML (maybe with potential syntax
         * errors).
         */
        object loaded;
        try
        {
            loaded = AvaloniaRuntimeXamlLoader.Load(stream);
        }
        catch (XamlLoadException)
        {
            SwitchLocale("en-US");

            /* 
             * Make sure that we return so to guarantee that `loaded` will always have possession of a value.
             */
            return;
        }
        finally
        {
            stream.Dispose();
        }

        if (loaded is not ResourceDictionary dictionary)
        {
            SwitchLocale("en-US");

            /* 
             * There is a chance the XAML itself is not defined as a `ResourceDictionary`, which is required.
             */
            return;
        }

        /* 
         * Otherwise, we can add the loaded `ResourceDictionary` to our merged dictionaries, which should load
         * it.
         */
        s_dictionary.MergedDictionaries.Add(dictionary);
    }

    /// <summary>
    /// Initializes the <see cref="LocalizationManager"/>.
    /// </summary>
    /// <param name="application">The application that will receive the localization resources.</param>
    public static void Initialize(Application application)
    {
        s_application = application;
        s_application.Resources.MergedDictionaries.Add(s_dictionary);

        Sources = new List<LocalizationSource>();

        /*
         * We have no sources, so we won't be switching to the current UI culture locale here. We will have to
         * do this in the application.
         */
    }

    /// <summary>
    /// Gets a localized string with a particular <paramref name="identifier"/>.
    /// </summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>
    /// If the string was found, its localization. Otherwise, a generic failure
    /// message.
    /// </returns>
    public static string GetString(string identifier)
    {
        s_dictionary.TryGetResource(identifier, null, out object? value);
        return value is string @string ?
            @string : $"\"{identifier}\": String could not be located!";
    }

    /// <summary>
    /// Switches the <see cref="LocalizationManager"/>'s current string database to the specified locale.
    /// </summary>
    /// <param name="name">The name of the locale, specified as i.e. en-US for English, United States.</param>
    public static void SwitchLocale(string name)
    {
        /* 
         * We only need to do this here, as this is the only function that we will be directly calling when we
         * need to load/reload the locale.
         */
        s_dictionary.Clear();

        foreach (LocalizationSource current in Sources!)
        {
            LoadResource(current.GetResourceStream(name));
        }
    }
}
