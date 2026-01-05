using Avalonia.Controls;
using Frosty.Sdk.Utils;
using Frosty.Ui.Exceptions;
using System.Reflection;
using System.Text;
using System.Xml;

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
        /// <returns>The formatted period-delimitted path.</returns>
        public readonly string FormatResourcePath(string filename) =>
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

    internal readonly Stream? GetInternalStream(string filename)
    {
        try
        {
            return Internal.Assembly.GetManifestResourceStream(Internal.FormatResourcePath(filename));
        }
        catch (FileNotFoundException)
        {
            /*
             * `GetManifestResourceStream` can throw this exception if we request a resource that could not
             * be found while there are other resources in the assembly, so we'll want to make sure we are
             * handling this in the event that a language does not have a resource dictionary defined here.
             */
            return null;
        }
    }

    internal readonly Stream? GetStream(string language)
    {
        /* 
         * `External` can be specified as `null` or an empty string to default to always using the internal
         * stream.
         */
        string filename = $"{language}.xml";

        if (string.IsNullOrEmpty(External))
        {
            return GetInternalStream(filename);
        }

        string external = Path.Combine(
            Utils.BaseDirectory,
            External, $"{filename}");

        if (File.Exists(external))
        {
            return new FileStream(external,
                FileMode.Open);
        }
        else
        {
            return GetInternalStream(filename);
        }
    }
}

/// <summary>
/// Provides the arguments for a <see cref="LocaleChangedEventHandler"/>, such as the strings that've been
/// newly loaded.
/// </summary>
/// <param name="current">The current, or newly set locale.</param>
/// <param name="previous">The previous locale.</param>
/// <param name="strings">
/// An <see cref="IEnumerable{T}"/> containing the strings associated with the <see cref="current"/>
/// locale.
/// </param>
public class LocaleChangedEventArgs(
    string? previous, string? current, IEnumerable<KeyValuePair<string, string>> strings) : EventArgs
{
    /// <summary>
    /// The current, or newly set locale.
    /// </summary>
    public string? Current => current;

    /// <summary>
    /// The previous locale.
    /// </summary>
    public string? Previous => previous;

    /// <summary>
    /// An <see cref="IEnumerable{T}"/> containing the strings associated with the <see cref="Current"/>
    /// locale.
    /// </summary>
    public IEnumerable<KeyValuePair<string, string>> Strings => strings;
}

/// <summary>
/// An <see cref="EventHandler"/> triggered whenever <see cref="LocalizationManager"/> has switched locales.
/// </summary>
/// <param name="sender">The sender.</param>
/// <param name="e">The event arguments.</param>
public delegate void LocaleChangedEventHandler(object? sender, LocaleChangedEventArgs e);

/// <summary>
/// Provides access to language-dependent strings that are indexed through a particular string identifier.
/// </summary>
public class LocalizationManager
{
    /* 
     * We must keep track of the `ResourceDictionary` we add, so that we can remove it when it is necessary.
     * This will always get assigned in the constructor.
     */
    private Dictionary<string, string> m_dictionary = new();
    private List<LocalizationSource> m_sources = new();

    /// <summary>
    /// The singleton instance of the <see cref="LocalizationManager"/>. This is accessible early in the
    /// bootflow, albeit a language will not be loaded by default.
    /// </summary>
    public static LocalizationManager Instance
    {
        get;
    } = new LocalizationManager();

    /// <summary>
    /// The <see cref="LocalizationManager"/>'s current locale. This will be initially null until a call
    /// to <see cref="SwitchLocale(string)"/> is made.
    /// </summary>
    public string? CurrentLocale
    {
        get;
        private set;
    }

    /// <summary>
    /// An event called whenever a call to <see cref="SwitchLocale(string)"/> is made.
    /// </summary>
    public event LocaleChangedEventHandler? LocaleChanged;

    private LocalizationManager()
    {}

    private static void FillExternalLanguages(string external, List<string> languages)
    {
        /*
         * We have already checked if the given source exists in `GetAvailableLanguages`, so we can safely
         * begin an iteration directly.
         */
        foreach (string current in Directory.EnumerateFiles(external))
        {
            string name = Path.GetFileNameWithoutExtension(current);

            /*
             * Before we add the language to our strings, we'll want to check if the collection already is
             * holding it.
             */
            if (!languages.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                languages.Add(name);
            }
        }
    }

    private static void FillInternalLanguages(LocalizationSource.InternalSource @internal, List<string> languages)
    {
        foreach (string current in @internal.Assembly.GetManifestResourceNames())
        {
            if (!current.StartsWith(@internal.ResourcePath) ||
                !current.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            /*
             * We have narrowed the possible resources to only our source, so we can now begin to retrieve
             * their associated languages.
             */
            ReadOnlySpan<char> view = current;
            view = view.Slice(0, view.LastIndexOf('.'));

            /*
             * Once we've removed the extension, we can use `LastIndexof` to locate the final period, then
             * slice the string starting after it.
             */
            string name =
                view.Slice(view.LastIndexOf('.') + ".".Length).ToString();

            /*
             * Before we add the language to our strings, we'll want to check if the collection already is
             * holding it.
             */
            if (!languages.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                /*
                 * This should account for differences in casing, so we should not receive duplicates from
                 * this function.
                 */
                languages.Add(name);
            }
        }
    }

    private void AddMultiLineString(string identifier, XmlNodeList children)
    {
        StringBuilder builder = new();

        /*
         * As for our format validation, we'll only need to check whether or not each element is named as a
         * `Line`.
         */
        foreach (XmlNode current in children)
        {
            if (current.Name is not "Line")
            {
                continue;
            }

            if (current.ChildNodes.Count is 0)
            {
                builder.AppendLine();
                continue;
            }

            if (current.ChildNodes.Count is 1 && current.FirstChild is XmlText text)
            {
                builder.AppendLine(text.Value);
            }
        }

        m_dictionary.Add(identifier, builder.ToString());
    }

    private void ReadStream(Stream? stream)
    {
        if (stream is null)
        {
            return;
        }

        /* 
         * Otherwise, we should be able to proceed with loading the external XAML (maybe with potential syntax
         * errors).
         */
        try
        {
            ReadStrings(stream);
        }
        catch (XmlException exception)
        {
            m_dictionary.Clear();
            throw new LocalizationManagerLoadException(exception);
        }
    }

    private void ReadStrings(Stream stream)
    {
        /*
         * Since we're catching exceptions in the calling function, we'll exclude the majority of the error
         * handling here.
         */
        XmlDocument document = new XmlDocument();
        document.Load(stream);

        /* 
         * Once we have got the document read, we'll need to do some validation to ensure that it's in the
         * correct format.
         */
        if (document.DocumentElement is null || document.DocumentElement.Name is not "LanguageDefinition" ||
            document.DocumentElement.ChildNodes.Count is 0)
        {
            return;
        }

        foreach (XmlNode current in document.DocumentElement.ChildNodes)
        {
            /*
             * There could be incorrectly named elements or strings without identifiers, so we will need to
             * check for this.
             */
            if (current.Name is not "String" || current.Attributes is null)
            {
                continue;
            }

            XmlNode? identifier = current.Attributes.GetNamedItem("Identifier");
            if (identifier is null || identifier.Value is null)
            {
                continue;
            }

            if (current.ChildNodes.Count is > 1)
            {
                AddMultiLineString(identifier.Value, current.ChildNodes);
                continue;
            }

            /*
             * Once we've validated that the data is in the correct format, we can safely add the string in
             * our database.
             */
            if (current.ChildNodes.Count is 1 && current.FirstChild is XmlText text && text.Value is not null)
            {
                m_dictionary.Add(identifier.Value, text.Value);
            }
        }
    }

    /// <summary>
    /// Adds the provided <paramref name="source"/> if a matching <see cref="LocalizationSource"/> does not
    /// exist.
    /// </summary>
    /// <param name="source">The <see cref="LocalizationSource"/>.</param>
    public bool AddSource(LocalizationSource source)
    {
        if (m_sources.Contains(source))
        {
            return false;
        }
        else
        {
            m_sources.Add(source);

            /*
             * We've mainly done this to prevent external assemblies (i.e. plugins) from adding more than a
             * single instance of the same source while also informing them of when this operation fails.
             */
            return true;
        }
    }

    /// <summary>
    /// Clears the <see cref="LocalizationManager"/>'s sources, but does not wipe its database of strings.
    /// To do this, see <see cref="SwitchLocale(string?)"/> with a null parameter.
    /// </summary>
    public void ClearSources() => m_sources.Clear();

    /// <summary>
    /// Enumerates over the available languages within the provided <see cref="m_sources"/>.
    /// </summary>
    /// <returns>A <see cref="List{string}"/> containing all available languages.</returns>
    public List<string> GetAvailableLanguages()
    {
        List<string> result = new();

        foreach (LocalizationSource current in m_sources)
        {
            /*
             * We allow external sources to be `null` or empty in the event that developers localize at an
             * internal level exclusively.
             */
            if (string.IsNullOrEmpty(current.External))
            {
                FillInternalLanguages(current.Internal, result);
                continue;
            }

            /*
             * `Path.Combine`'s behavior is a bit peculiar in that it starts from the last absolute path in
             * the provided arguments and ignores everything before it,
             * but this functionality is nice in our case since it saves us an explicit absolute path check.
             */
            string external = Path.Combine(Utils.BaseDirectory,
                current.External);

            if (Directory.Exists(external))
            {
                FillExternalLanguages(external, result);
            }
            else
            {
                FillInternalLanguages(current.Internal, result);
            }
        }

        /*
         * This shouldn't contain any duplicate languages, so it should work for i.e. display in a combobox
         * or within our configuration.
         */
        return result;
    }

    /// <summary>
    /// Gets a localized string with a particular <paramref name="identifier"/>.
    /// </summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>
    /// If the string was found, its localization. Otherwise, a generic failure
    /// message.
    /// </returns>
    public string GetString(string identifier) => (m_dictionary.ContainsKey(identifier) ?
        m_dictionary[identifier] : $"\"{identifier}\": String could not be located!");

    /// <summary>
    /// Performs a clean retrieval of strings for the <see cref="CurrentLocale"/> from <see cref="m_sources"/>.
    /// </summary>
    public void Refresh() => SwitchLocale(CurrentLocale);

    /// <summary>
    /// Removes a particular <paramref name="source"> from the <see cref="LocalizationManager"/> if it's found.
    /// </summary>
    /// <param name="source">The <see cref="LocalizationSource"/></param>
    /// <returns>True if the <see cref="LocalizationSource"/> was found and removed, otherwise false.</returns>
    public bool RemoveSource(LocalizationSource source) => m_sources.Remove(source);

    /// <summary>
    /// Switches the <see cref="LocalizationManager"/>'s current string database to the specified locale.
    /// </summary>
    /// <exception cref="LocalizationManagerLoadException"/>
    /// <param name="language">The name of the locale, specified as i.e. en-US for English, United States.</param>
    public void SwitchLocale(string? language)
    {
        /* 
         * We only need to do this here, as this is the only function that we will be directly calling when we
         * need to load/reload the locale.
         */
        string? previous = CurrentLocale;
        m_dictionary.Clear();
        CurrentLocale = language;

        if (!string.IsNullOrEmpty(language))
        {
            foreach (LocalizationSource current in m_sources)
            {
                /*
                 * For the dictionaries we've failed to find, attempt to use English, United States as a fallback.
                 */
                ReadStream(current.GetStream(language) ?? current.GetStream("en-US"));
            }
        }

        LocaleChanged?.Invoke(this, new LocaleChangedEventArgs(previous, language, m_dictionary));
    }
}
