using Frosty.Sdk;
using Frosty.Ui.ViewModels;

namespace FrostyEditor.ViewModels;

internal class SplashWindowViewModel : ViewModelBase
{
    /// <summary>
    /// The <see cref="ProfilesLibrary.DisplayName"/> for the game that the editor is currently injected into.
    /// </summary>
    public string Game => ProfilesLibrary.DisplayName;
}
