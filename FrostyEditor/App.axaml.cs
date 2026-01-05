using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Frosty.Ui.Managers;
using FrostyEditor.Extensions;
using FrostyEditor.Utilities;
using FrostyEditor.ViewModels;
using FrostyEditor.Views.Windows;

namespace FrostyEditor;

public partial class App : Application
{
    private ResourceDictionary m_strings = new();
    private ResourceDictionary m_theming = new();

    /// <summary>
    /// The singleton instance of the <see cref="App"/> class. This should be preferred over <see cref="Application.Current"/>.
    /// </summary>
    public static App? Instance => (App?)(Current);

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        Resources.MergedDictionaries.Add(m_strings);
        Resources.MergedDictionaries.Add(m_theming);

        LocalizationManager.Instance.LocaleChanged += LocalizationManager_LocaleChanged;
        LocalizationManager.Instance.EditorRefresh();

        /*
         * Next, we can initialize the `ThemingManager`, then populate its sources and switch to the user's configured theme.
         * Note that the order of these matters; ideally it should match the build order.
         */
        ThemingManager.Instance.AddSource(new ThemingSource("Resources/Ui/Theming", new
            Uri("avares://FrostyUi/Resources/Ui/Theming")));

        /*
         * TODO: As with `LocalizationManager`, try to isolate doing this to the responsibility of the assemblies themselves.
         */
        ThemingManager.Instance.AddSource(new ThemingSource("Resources/Editor/Theming",
            new Uri("avares://FrostyEditor/Resources/Editor/Theming")));


        ThemingManager.Instance.ThemeChanged += ThemingManager_ThemeChanged;

        /*
         * Finally, we can switch to the theme that the user has selected. Given that it's user input, this may not work due
         * to i.e. a theme being deleted manually.
         */
        ThemingManager.Instance.EditorSwitchTheme(Config.Get("SelectedTheme", "Default", ConfigScope.Game));
    }

    private void LocalizationManager_LocaleChanged(object? sender, LocaleChangedEventArgs e)
    {
        m_strings.Clear();
        foreach (KeyValuePair<string, string> current in e.Strings)
        {
            m_strings.Add(current.Key, current.Value);
        }
    }

    private void ThemingManager_ThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        m_theming.MergedDictionaries.Clear();

        /*
         * Compared to strings, we're given a collection of `IResourceProvider`s, meaning we'll need to merge these into our
         * own dictionary.
         */
        foreach (IResourceProvider current in e.Resources)
        {
            m_theming.MergedDictionaries.Add(current);
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            /*
             * Our `ViewLocator` will automatically assign a `DataContext` for the inner view of our `MainWindow`, so we can
             * omit the assignment here.
             */
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
