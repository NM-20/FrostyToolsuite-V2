using Frosty.Ui.Exceptions;
using Frosty.Ui.Managers;

namespace FrostyEditor.Extensions;

internal static class LocalizationManagerExtensions
{
    /// <summary>
    /// Performs editor-specific configuration and handling for switching the <see cref="LocalizationManager"/>'s current locale.
    /// </summary>
    /// <param name="extended">Reserved.</param>
    /// <param name="language">The new language.</param>
    public static void EditorSwitchLocale(this LocalizationManager extended, string? language)
    {
        /*
         * This is essentially meant to be a wrapper that handles exceptions for us and provides exception messages to the user.
         */
        try
        {
            extended.SwitchLocale(language);
        }
        catch (LocalizationManagerLoadException exception)
        {
            string message = string.Format(extended.GetString("Str_Editor_LocalizationManager_LoadException"),
                language, exception.InnerException);
            MessageBoxW(0, message, extended.GetString("Str_Global_ProgramTitle"), (MB_ICONERROR | MB_OK));
        }
    }

    /// <summary>
    /// Performs editor-specific confiugration and handling for refreshing the <see cref="LocalizationManager"/>'s current locale.
    /// </summary>
    /// <param name="extended">Reserved.</param>
    public static void EditorRefresh(this LocalizationManager extended) => extended.EditorSwitchLocale(extended.CurrentLocale);
}
