using Frosty.Ui.Exceptions;
using Frosty.Ui.Managers;

namespace FrostyEditor.Extensions;

internal static class ThemingManagerExtensions
{
    /// <summary>
    /// Performs editor-specific configuration and handling for switching the <see cref="ThemingManager"/>'s current theme.
    /// </summary>
    /// <param name="extended">Reserved.</param>
    /// <param name="theme">The new theme.</param>
    public static void EditorSwitchTheme(this ThemingManager extended, string? theme)
    {
        /*
         * This is essentially meant to be a wrapper that handles exceptions for us and provides error messages to the user.
         */
        try
        {
            extended.SwitchTheme(theme);
        }
        catch (ThemingManagerExternalLoadException external)
        {
            string message =
                string.Format(LocalizationManager.Instance.GetString("Str_Editor_ThemingManagerExternalException"),
                external.Asset, external.InnerException);

            /*
             * TODO: Introduce a `MessageBoxManager` that has a theme-agnostic style option so that we can use it instead of
             * `MessageBoxW`.
             */
            MessageBoxW(0, message, LocalizationManager.Instance.GetString("Str_Global_ProgramTitle"), (MB_ICONERROR | MB_OK));
        }
        catch (ThemingManagerInternalLoadException @internal)
        {
            string message =
                string.Format(LocalizationManager.Instance.GetString("Str_Editor_ThemingManagerInternalException"),
                @internal.Asset, @internal.InnerException);

            /*
             * TODO: Introduce a `MessageBoxManager` that has a theme-agnostic style option so that we can use it instead of
             * `MessageBoxW`.
             */
            MessageBoxW(0, message, LocalizationManager.Instance.GetString("Str_Global_ProgramTitle"), (MB_ICONERROR | MB_OK));
        }
    }

    /// <summary>
    /// Performs editor-specific configuration and handling for refreshing the <see cref="ThemingManager"/>'s current theme.
    /// </summary>
    /// <param name="extended">Reserved.</param>
    public static void EditorRefresh(this ThemingManager extended) => extended.EditorSwitchTheme(extended.CurrentTheme);
}
