using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Frosty.Sdk.Utils;
using Frosty.Ui.Exceptions;
using System.Reflection;

namespace Frosty.Ui.Managers;

/// <summary>
/// Describes a theme source that the <see cref="ThemingManager"/> can pull from for resources, i.e. icons,
/// resources, etc.
/// </summary>
/// <param name="External">
/// The external source to be pulled from. If this is `null` or empty, <see cref="ThemingManager"/> will
/// always use <see cref="Internal"/>.
/// </param>
/// <param name="Internal">
/// The internal source to be pulled from (a source within a particular <see cref="Assembly"/>, denoted
/// in the format of an `avares` URI).
/// </param>
public record struct ThemingSource(string? External, Uri Internal)
{
    internal readonly string GetAvailableSource(string theme)
    {
        /*
         * Before anything else, we will need to format our internal URI, as we'll return it when `External`
         * is empty.
         */
        string filename = $"{theme}/Theme.axaml";
        string @internal = Path.Combine(Internal.AbsoluteUri, filename);

        if (string.IsNullOrEmpty(External))
        {
            return @internal;
        }

        string external = Path.Combine(Utils.BaseDirectory, External,
            filename);

        /*
         * `External` is not in the clear yet, as we need to determine whether or not it exists on the disk.
         */
        if (File.Exists(external))
        {
            return external;
        }
        else
        {
            /*
             * We do not do any checks here for the existence of the internal source; we handle those checks
             * later.
             */
            return @internal;
        }
    }
}

/// <summary>
/// Encapsulates the <see cref="EventArgs"/> of a <see cref="ThemeChangedEventHandler"/>, i.e. the previous
/// theme, the new theme, and its resources.
/// </summary>
/// <param name="previous">The previous theme.</param>
/// <param name="current">The current theme, that is, the new theme.</param>
/// <param name="resources">
/// An <see cref="IEnumerable{IResourceProvider}"/> that contains all resources corresponding with the new
/// (<see cref="current"/>) theme.
/// </param>
public class ThemeChangedEventArgs(string? previous, string? current, IEnumerable<IResourceProvider> resources) :
    EventArgs
{
    /// <summary>
    /// The current theme, that is, the new theme.
    /// </summary>
    public string? Current => current;

    /// <summary>
    /// The previous theme.
    /// </summary>
    public string? Previous => previous;

    /// <summary>
    /// An <see cref="IEnumerable{IResourceProvider}"/> that contains all resources corresponding with the new
    /// (<see cref="current"/>) theme.
    /// </summary>
    public IEnumerable<IResourceProvider> Resources => resources;
}

/// <summary>
/// An <see cref="EventHandler"/> triggered whenever <see cref="ThemingManager"/> has switched locales.
/// </summary>
/// <param name="sender">The sender.</param>
/// <param name="e">The event arguments.</param>
public delegate void ThemeChangedEventHandler(object? sender, ThemeChangedEventArgs e);

/// <summary>
/// Provides theming functionality for applications. Allows for conditional loading of assets, such as icons
/// and resources, based on a particular theme identifier.
/// </summary>
public class ThemingManager
{
    private List<ThemingSource> m_sources = new();

    /// <summary>
    /// The singleton instance of the <see cref="ThemingManager"/>.
    /// </summary>
    public static ThemingManager Instance
    {
        get;
    } = new ThemingManager();

    /// <summary>
    /// The <see cref="ThemingManager"/>'s current theme denoted by its name. The theme is `null` until some
    /// call to <see cref="SwitchTheme(string)"/> is made.
    /// </summary>
    public string? CurrentTheme
    {
        get;
        private set;
    }

    /// <summary>
    /// An event that is triggered whenever the <see cref="SwitchTheme(string)"/> function has succeeded.
    /// </summary>
    public event ThemeChangedEventHandler? ThemeChanged;

    private ThemingManager()
    {}

    private static bool DoesInternalResourceProviderExist(Uri @internal)
    {
        Type? resources = AssetLoader.GetAssembly(@internal)?.GetType("CompiledAvaloniaXaml.!AvaloniaResources");
        if (resources is null)
        {
            return false;
        }

        foreach (MethodInfo current in resources.GetMethods())
        {
            ReadOnlySpan<char> view = current.Name;
            if (!current.Name.StartsWith("Build:"))
            {
                continue;
            }

            view = view.Slice("Build:".Length);

            if (view.Equals(@internal.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        /*
         * Otherwise, the theme doesn't exist; `AvaloniaResources` contains the names of the compiled AXAML
         * files that exist.
         */
        return false;
    }

    private static void FillExternalThemes(string external, List<string> themes)
    {
        foreach (string current in Directory.EnumerateDirectories(external))
        {
            /*
             * Since we have verified that the external source exists, we can begin the iteration without
             * any validation.
             */
            string name = Path.GetFileName(current);

            if (!themes.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                themes.Add(name);
            }
        }
    }

    private static void FillInternalThemes(Uri @internal, List<string> themes)
    {
        /*
         * Since we have compiled AXAML enabled, we can't access these resources via `AssetLoader` and, as a
         * result, we'll have to use reflection.
         * Avalonia stores the compiled results under the `CompiledAvaloniaXaml` namespace in the containing
         * assembly, so we can use this to fetch the theme names.
         */
        Type? resources = AssetLoader.GetAssembly(@internal)?.GetType("CompiledAvaloniaXaml.!AvaloniaResources");
        if (resources is null)
        {
            return;
        }

        /*
         * Through `!AvaloniaResources`, we will want to focus on the `Build` methods, in which there is one
         * for each compiled AXAML.
         */
        foreach (MethodInfo current in resources.GetMethods())
        {
            ReadOnlySpan<char> view = current.Name;
            if (!view.StartsWith("Build:"))
            {
                continue;
            }

            view = view.Slice("Build:".Length);

            /*
             * The `Build` function names use relative URIs, so we'll need to check if they're starting with
             * the relative part of our URI.
             */
            if (!view.StartsWith(@internal.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            view = view.Slice(@internal.AbsolutePath.Length).Trim('/');

            /*
             * Once we've trimmed our relative path from the string and knowing that the only slash character
             * is backslashes, we can substring from zero to the index of the first backslash to retrieve the
             * theme directory.
             */
            string name = view.Slice(0, view.IndexOf('/')).ToString();

            /*
             * We now have the theme directory name, which is equivalent to the theme name that we display to
             * users.
             * Before adding it to our collection, we will need to verify that we haven't already appended it.
             */
            if (!themes.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                themes.Add(name);
            }
        }
    }

    private static IResourceProvider? ReadExternalResourceProvider(string external)
    {
        object loaded;
        try
        {
            loaded = AvaloniaRuntimeXamlLoader.Load(new FileStream(external,
                FileMode.Open, FileAccess.Read));
        }
        catch (XamlLoadException exception)
        {
            throw new ThemingManagerExternalLoadException(external, exception);
        }

        if (loaded is not IResourceProvider provider)
        {
            return null;
        }
        else
        {
            /*
             * We've got a loaded `IResourceProvider` instance, which we can append to a `IResourceProvider`
             * collection as needed.
             */
            return provider;
        }
    }

    private static IResourceProvider? ReadInternalResourceProvider(string @internal)
    {
        /*
         * We will need a URI for both checking if providers exist, as well as loading them in our program.
         */
        Uri uri = new(@internal);

        if (!DoesInternalResourceProviderExist(uri))
        {
            return null;
        }

        object loaded;
        try
        {
            loaded = AvaloniaXamlLoader.Load(uri);
        }
        catch (XamlLoadException exception)
        {
            throw new ThemingManagerInternalLoadException(@internal, exception);
        }

        if (loaded is not IResourceProvider provider)
        {
            return null;
        }
        else
        {
            /*
             * We've got a loaded `IResourceProvider` instance, which we can append to a `IResourceProvider`
             * collection as needed.
             */
            return provider;
        }
    }

    private static IResourceProvider? ReadResourceProvider(string source) =>
        (File.Exists(source) ? ReadExternalResourceProvider(source) : ReadInternalResourceProvider(source));

    /// <summary>
    /// Appends a <see cref="ThemingSource"/> to the <see cref="ThemingManager"/>'s tracked sources if an
    /// existing source that matches could not be found.
    /// </summary>
    /// <param name="source">The <see cref="ThemingSource"/> to add.</param>
    /// <returns>True if the <see cref="ThemingSource"/> was successfully added; otherwise, false.</returns>
    public bool AddSource(ThemingSource source)
    {
        if (m_sources.Contains(source))
        {
            return false;
        }

        if (!source.Internal.IsAbsoluteUri)
        {
            return false;
        }
        else
        {
            m_sources.Add(source);

            /*
             * We have mainly done this to prevent external assemblies (i.e. plugins) from adding more than
             * a single instance of the same source while also informing them of when this operation fails.
             */
            return true;
        }
    }

    /// <summary>
    /// Clears the <see cref="ThemingManager"/>'s sources, but does not clear its database of strings. To
    /// do this, see <see cref="SwitchTheme(string?)"/> with a null parameter.
    /// </summary>
    public void ClearSources() => m_sources.Clear();

    /// <summary>
    /// Retrieves a collection of the available themes within the <see cref="ThemingManager"/>'s sources.
    /// </summary>
    /// <returns>The collection.</returns>
    public List<string> GetAvailableThemes()
    {
        List<string> result = new();

        foreach (ThemingSource current in m_sources)
        {
            /*
             * We allow external sources to be `null` or empty in the event that developers wish for themes
             * to exclusively pull internally.
             */
            if (string.IsNullOrEmpty(current.External))
            {
                FillInternalThemes(current.Internal, result);
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
                FillExternalThemes(external, result);
            }
            else
            {
                FillInternalThemes(current.Internal, result);
            }
        }

        /*
         * Similar to `LocalizationManager`, we use case-insensitive ordinal comparison to prevent duplicate
         * names.
         */
        return result;
    }

    /// <summary>
    /// Refreshes the <see cref="CurrentTheme"/> to account for potential newly added sources.
    /// </summary>
    public void Refresh() => SwitchTheme(CurrentTheme);

    /// <summary>
    /// Removes the specified <see cref="ThemingSource"/> from the <see cref="ThemingManager"/>, but doesn't
    /// refresh the current theme.
    /// </summary>
    /// <param name="source">The <see cref="ThemingSource"/> to be removed.</param>
    public bool RemoveSource(ThemingSource source) => m_sources.Remove(source);

    /// <summary>
    /// Tries to switch the <see cref="CurrentTheme"/> to the theme with the passed <paramref name="theme"/>.
    /// </summary>
    /// <exception cref="ThemingManagerExternalLoadException"/>
    /// <exception cref="ThemingManagerInternalLoadException"/>
    /// <param name="theme">The name of the theme to switch to.</param>
    public void SwitchTheme(string? theme)
    {
        string? previous = CurrentTheme;
        CurrentTheme = theme;
        List<IResourceProvider> resources = new();

        if (theme is not null)
        {
            foreach (ThemingSource current in m_sources)
            {
                /*
                 * Propagate the rest of loading to the individual `ThemingSource` instances. We'll be going
                 * through each `ThemingSource`, as they might contain parts of the whole theme.
                 */
                IResourceProvider? loaded = (ReadResourceProvider(current.GetAvailableSource(theme)) ??
                    ReadResourceProvider(current.GetAvailableSource("Default")));

                /*
                 * `Default` should work for our assemblies, but it might not for plugin assemblies. We will
                 * account for this.
                 */
                if (loaded is null)
                {
                    continue;
                }

                /*
                 * If we have an `IResourceProvider`, add it to our collection. This will work as long as we
                 * have registered `ThemeChanged` handlers.
                 */
                resources.Add(loaded);
            }
        }

        ThemeChanged?.Invoke(null, new ThemeChangedEventArgs(previous, theme, resources));
    }
}
