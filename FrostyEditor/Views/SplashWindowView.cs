using Avalonia.Controls.Primitives;

namespace FrostyEditor.Views;

internal class SplashWindowView : TemplatedControl
{
    protected override Type StyleKeyOverride => typeof(SplashWindowView);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
    }
}
