using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Frosty.Ui.Managers;
using FrostyEditor.ViewModels;
using FrostyEditor.Views;
using System.Globalization;

namespace FrostyEditor;

public partial class App : Application
{
    /// <summary>
    /// The singleton instance of the <see cref="App"/> class. This should be preferred over <see cref="Application.Current"/>.
    /// </summary>
    public static App? Instance => (App?)(Current);

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        /*
         * Before we add any sources, we'll need to ensure that we initialize the `LocalizationManager`. This will create the
         * `List` necessary for us to add.
         */
        LocalizationManager.Initialize(Instance!);

        LocalizationSource source = new("Languages",
            new LocalizationSource.InternalSource(typeof(App).Assembly, "FrostyEditor.Languages"));

        LocalizationManager.Sources!.Add(source);

        /* Once we've added a source, we can then switch the locale to the current UI culture. This isn't guaranteed to work,
         * but `LocalizationManager` will automatically handle a fallback for us.
         */
        LocalizationManager.SwitchLocale(CultureInfo.CurrentUICulture.Name);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
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
