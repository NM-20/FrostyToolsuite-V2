using Frosty.Sdk.Sdk;
using FrostyEditor.Utilities;
using Reloaded.Hooks.Definitions;

namespace FrostyEditor.Bootflow;

internal unsafe sealed class NfsUnboundBootflow : BootflowBase
{
    private delegate bool AwcFixupExeFunction(void *unknown1, void *unknown2, void *unknown3, void *unknown4,
        void *unknown5, void *unknown6);

    private IHook<AwcFixupExeFunction>? m_awcFixupExeDetour;
    private PatternScanner? m_scanner;
    private IHook<WinMainFunction>? m_winMainDetour;

    private WinMainContext m_context;

    private bool DetourAwcFixupExe(void *unknown1, void *unknown2, void *unknown3, void *unknown4,
        void *unknown5, void *unknown6)
    {
        bool result = m_awcFixupExeDetour!.OriginalFunction(unknown1, unknown2, unknown3, unknown4, unknown5,
            unknown6);

        const string pattern = "4C 8B DC 48 81 EC 98 00 00 00 49 C7";

        /*
         * The executable will be unpacked at this point, so we'll be able to scan within the executable. We
         * will next need to hook `WinMain`, which is where we can call `StartGame`.
         */
        m_winMainDetour = Mod.Instance!.Hooks!.CreateHook<WinMainFunction>(
            DetourWinMain,
            Mod.Instance!.PatternScanner.ScanOne(pattern)).Activate();

        return result;
    }

    private int DetourWinMain(nint hInstance, nint hPrevInstance, nint lpCmdLine, int nShowCmd)
    {
        m_context = new WinMainContext(hInstance, hPrevInstance, lpCmdLine, nShowCmd);
        StartEditor();
        return Environment.ExitCode;
    }

    public override void Initialize(IHook<NativeEntrypointFunction> detour)
    {
        base.Initialize(detour);

        /*
         * Reloaded injects before the executable entrypoint is called, so we'll need to load `Activation64`
         * manually.
         */
        LoadActivation();
        m_scanner = new PatternScanner(ActivationPath);

        /*
         * PvZ2 only offers EA's DRM across all platforms, so we'll only need to hook Activation's FixupExe.
         * Before that, we'll need to configure the environment to ensure that the executable gets unpacked.
         */
        Environment.SetEnvironmentVariable("EARtPLaunchCode", RequiredToPlay.GenerateCode());
        Environment.SetEnvironmentVariable("ContentId", "196787");

        const string pattern =
            "40 55 53 56 57 41 54 41 55 41 56 41 57 48 8D AC 24 68 FF FF FF 48 81 EC 98 01 00 00 48 C7 45 90";

        /*
         * Next, we'll need to hook `AwcFixupExe`, which we'll locate through our pattern scanner. This will
         * not be called until we execute the original native entrypoint.
         */
        m_awcFixupExeDetour =
            Mod.Instance!.Hooks!.CreateHook<AwcFixupExeFunction>(DetourAwcFixupExe, m_scanner.ScanOne(pattern)).
            Activate();

        /*
         * This will call Activation's ordinal 100, which will check for `ContentId` and `EARtPLaunchCode`
         * in the environment before unpacking the executable.
         */
        detour.OriginalFunction();
    }

    public override void StartGame()
    {
        base.StartGame();

        /* 
         * To start the game, we have to call the original implementation of `WinMain` on a new thread. This
         * prevents any blocking of editor execution.
         */
        Task.Factory.StartNew(() => m_winMainDetour!.OriginalFunction(
            m_context.HandleInstance, m_context.HandlePrevInstance, m_context.LongPointerCmdLine, m_context.NumberShowCmd),
            TaskCreationOptions.LongRunning);
    }
}
