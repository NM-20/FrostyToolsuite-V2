using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Frosty.Ui.Managers;
using Frosty.Ui.ViewModels;

namespace Frosty.Ui;

public class ViewLocator : IDataTemplate
{
    private Control? CreateView(ViewModelBase model, Type type)
    {
        /*
         * Just to be safe, we'll ensure that the `View` we found is a `Control`. For our assemblies,
         * this should always be the case, but it could happen in plugins.
         */
        if (!type.IsSubclassOf(typeof(Control)))
        {
            return null;
        }

        var view = (Control?)(Activator.CreateInstance(type));
        if (view is null)
        {
            return null;
        }

        /*
         * Knowing that `param` is a `ViewModelBase` and that the type we create is the corresponding
         * `View`, we'll be assigning `param` as the `DataContext` in the `View`.
         */
        view.DataContext = model;

        /*
         * Finally, we can return the `View`. Since we create a new `ViewModelBase` each time in XAML,
         * these should all have unique `DataContext`s.
         */
        return view;
    }

    public Control? Build(object? param)
    {
        /*
         * The `is` check in `Match` automatically disqualifies `null` values, so we can safely access
         * the `param` as if it's assigned.
         */
        var model = (ViewModelBase)(param!);

        var name = param!.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type is not null)
        {
            return CreateView(model, type);
        }

        /*
         * `LocalizationManager` has sources added before Avalonia initializes, so we can safely be
         * using it here.
         */
        return new TextBlock
        { Text = string.Format(LocalizationManager.Instance.GetString("Str_Ui_ViewLocator_NotFound"), name) };
    }

    public bool Match(object? data) => (data is ViewModelBase);
}
