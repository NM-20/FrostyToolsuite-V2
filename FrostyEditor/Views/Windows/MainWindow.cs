using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace FrostyEditor.Views.Windows;

internal class MainWindow : Window
{
    protected override Type StyleKeyOverride => typeof(MainWindow);

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        /*
         * We're not using Avalonia's templates at the moment, so we will
         * need to do this manually.
         */
        #if DEBUG
        this.AttachDevTools();
        #endif
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
    }
}
