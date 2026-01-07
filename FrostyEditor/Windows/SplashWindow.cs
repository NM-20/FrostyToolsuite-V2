using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace FrostyEditor.Windows;

internal class SplashWindow : Window
{
    protected override Type StyleKeyOverride => typeof(SplashWindow);

    /// <summary>
    /// Initializes a new instance of the <see cref="SplashWindow"/> class.
    /// </summary>
    public SplashWindow()
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
