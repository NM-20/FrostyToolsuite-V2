using Avalonia;
using Frosty.Sdk;
using FrostyEditor.Utilities;
using Reloaded.Hooks.Definitions;
using Reloaded.Mod.Interfaces.Internal;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FrostyEditor.Bootflow;

internal unsafe abstract class BootflowBase
{
    /*
     * Our bootflows will always be hooking `WinMain` to defer execution within
     * games. We'll need to store the values from the function for this to work.
     */
    protected record struct WinMainContext(nint HandleInstance,
        nint HandlePrevInstance, nint LongPointerCmdLine, int NumberShowCmd);

    protected delegate int WinMainFunction(
        nint hInstance, nint hPrevInstance, nint lpCmdLine, int nShowCmd);

    /// <summary>
    /// A <see cref="Delegate"/> representing the entrypoint that is defined in
    /// an executable's `OptionalHeader`.
    /// </summary>
    public delegate void NativeEntrypointFunction();

    protected virtual string ActivationPath => "Core\\Activation64.dll";

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().
        UsePlatformDetect().WithInterFont().LogToTrace();

    private static bool UseInjectionWorkflow() =>
        Config.Get("UseInjectedWorkflow", !ProfilesLibrary.HasDenuvoTicket);

    protected static void StartEditor() => BuildAvaloniaApp().
        StartWithClassicDesktopLifetime(Environment.GetCommandLineArgs());

    protected void LoadActivation()
    {
        IApplicationConfigV1 configuration = Mod.Instance!.ModLoader.GetAppConfig();
        NativeLibrary.Load(Path.Join(
            Path.GetDirectoryName(configuration.AppLocation)!, ActivationPath));
    }

    /// <summary>
    /// Calls the original `WinMain` on a separate thread, effectively starting
    /// the game.
    /// If `UseInjectedWorkflow` in the configuration is set to false, the game
    /// will boot externally instead.
    /// </summary>
    public virtual void StartGame()
    {
        /* Through Reloaded's API, we can easily fetch the location of the game
         * executable for use when the injected workflow is disabled.
         */
        if (UseInjectionWorkflow())
        {
            return;
        }

        IApplicationConfigV1 configuration = Mod.Instance!.ModLoader.GetAppConfig();
        Process.Start(configuration.AppLocation);
    }

    /// <summary>
    /// Applies the necessary changes to the game executable for Frosty to boot.
    /// </summary>
    /// <param name="detour">
    /// The hook on the <see cref="NativeEntrypointFunction"/> to callback into.
    /// </param>
    public virtual void Initialize(IHook<NativeEntrypointFunction> detour)
    {
        /*
         * While we encourage the injected workflow for the games supporting it,
         * it might have issues at times.
         * We'll be supporting the traditional external workflow from V1 through
         * redirecting the executable entrypoint to boot up the editor directly
         * without waiting for an unpack.
         */
        if (UseInjectionWorkflow())
        {
            return;
        }

        /* If we've found that the injection workflow is disabled, we'll want to
         * start the editor now, then exit the process to prevent returns to the
         * inheriting bootflow.
         */
        StartEditor();
        Environment.Exit(Environment.ExitCode);
    }
}
