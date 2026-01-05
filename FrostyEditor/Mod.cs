using Frosty.Sdk;
using Frosty.Sdk.Sdk;
using Frosty.Sdk.Utils;
using Frosty.Ui.Managers;
using FrostyEditor.Managers;
using FrostyEditor.Template;
using FrostyEditor.Utilities;
using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;
using System.Globalization;
using FrostyEditor.Extensions;

#if DEBUG
using System.Diagnostics;
#endif

namespace FrostyEditor;

/// <summary>
/// Your mod logic goes here.
/// </summary>
public class Mod : ModBase // <= Do not Remove.
{
    /// <summary>
    /// The primary <see cref="Mod"/> instance that represents the injected Frosty Editor. Access this for
    /// hooking, etc.
    /// </summary>
    public static Mod? Instance { get; private set; }

    /// <summary>
    /// Provides access to the mod loader API.
    /// </summary>
    public IModLoader ModLoader { get; private set; }

    /// <summary>
    /// Provides access to the Reloaded.Hooks API.
    /// </summary>
    /// <remarks>This is null if you remove dependency on Reloaded.SharedLib.Hooks in your mod.</remarks>
    public IReloadedHooks? Hooks { get; private set; }

    /// <summary>
    /// Provides access to the Reloaded logger.
    /// </summary>
    public ILogger Logger { get; private set; }

    /// <summary>
    /// Entry point into the mod, instance that created this class.
    /// </summary>
    public IMod Owner { get; private set; }

    /// <summary>
    /// The configuration of the currently executing mod.
    /// </summary>
    public IModConfig ModConfig { get; private set; }

    /// <summary>
    /// The primary <see cref="Frosty.Sdk.Sdk.PatternScanner"/>. Use this when a scan within the editor
    /// process is required.
    /// </summary>
    public PatternScanner PatternScanner { get; private set; }

    public Mod(ModContext context)
    {
        /*
         * This will essentially be our entrypoint for the editor. Any core initialization must go here,
         * as it provides the earliest initialization before the game boots.
         */

        ModLoader = context.ModLoader;
        Hooks = context.Hooks;
        Logger = context.Logger;
        Owner = context.Owner;
        ModConfig = context.ModConfig;

        Instance = this;

#if DEBUG
        // Attaches debugger in debug mode; ignored in release.
        Debugger.Launch();
#endif

        // For more information about this template, please see
        // https://reloaded-project.github.io/Reloaded-II/ModTemplate/

        // If you want to implement e.g. unload support in your mod,
        // and some other neat features, override the methods in ModBase.

        /*
         * Setting this is essentially a prerequisite to the entire bootflow, so we will set it as early
         * as we can.
         */
        Utils.BaseDirectory = ModLoader.GetDirectoryForModId(ModConfig.ModId);

        Config.Load(Path.Combine(Utils.BaseDirectory, "editor_config.json"));

        /*
         * We're aiming to have localization as early as possible, so we will initialize it in our ctor.
         */
        LocalizationSource source = new("Resources/Editor/Languages",
            new LocalizationSource.InternalSource(
            typeof(App).Assembly, "FrostyEditor.Resources.Editor.Languages"));

        LocalizationManager.Instance.AddSource(source);

        /*
         * TODO: Leave the responsibility for registering localization up to the assemblies themselves.
         */
        source = new("Resources/Ui/Languages", new LocalizationSource.InternalSource(
            typeof(LocalizationManager).Assembly, "Frosty.Ui.Resources.Ui.Languages"));

        LocalizationManager.Instance.AddSource(source);

        /* 
         * Once we've got out sources, we can then switch over to the user's selected theme. By default,
         * we use the current UI culture to detect a default language.
         */
        LocalizationManager.Instance.EditorSwitchLocale(
            Config.Get("SelectedLanguage", CultureInfo.CurrentUICulture.Name, ConfigScope.Global));

        PatternScanner.Initialize();
        PatternScanner = new PatternScanner();

        /*
         * Game selection is a bit different from V1: it's done through Reloaded. We'll need to grab the
         * executable from its API to initialize `ProfilesLibrary`.
         */
        IApplicationConfigV1 configuration = ModLoader.GetAppConfig();

        if (!ProfilesLibrary.Initialize(Path.GetFileNameWithoutExtension(
            configuration.AppId)))
        {
            MessageBoxW(0, LocalizationManager.Instance.GetString("Str_Editor_Mod_ProfileNotFound"),
                LocalizationManager.Instance.GetString("Str_Editor_ProgramTitle"), (MB_ICONERROR | MB_OK));

            const int ERROR_FILE_NOT_FOUND = 2;
            Environment.Exit(ERROR_FILE_NOT_FOUND);
        }

        /* 
         * For the more game-specific procedures, these will be handled within their own unique bootflow
         * implementation. See these for further initialization.
         */
        BootflowManager.Initialize();
    }

    #region For Exports, Serialization etc.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public Mod() { }
#pragma warning restore CS8618
    #endregion
}