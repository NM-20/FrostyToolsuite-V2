using Frosty.Sdk.Sdk;
using FrostyEditor.Utilities;
using Reloaded.Hooks.Definitions;

namespace FrostyEditor.Bootflow;

internal unsafe sealed class Nfs16Bootflow : BootflowBase
{
    private delegate bool AwcFixupExeFunction(void *unknown1, void *unknown2, void *unknown3, void *unknown4,
        void *unknown5, void *unknown6, void *unknown7);

    private delegate bool UnknownFixupExeFunction(void *unknown1, void *unknown2, uint unknown3,
        void *unknown4);

    private IHook<AwcFixupExeFunction>? m_awcFixupExeDetour;
    private PatternScanner? m_scanner;
    private IHook<UnknownFixupExeFunction>? m_unknownFixupExeDetour;
    private IHook<WinMainFunction>? m_winMainDetour;

    private WinMainContext m_context;

    private bool DetourAwcFixupExe(void *unknown1, void *unknown2, void *unknown3, void *unknown4,
        void *unknown5, void *unknown6, void *unknown7)
    {
        bool result = m_awcFixupExeDetour!.OriginalFunction(unknown1, unknown2, unknown3, unknown4, unknown5,
            unknown6, unknown7);

        const string pattern =
            "4C 89 4C 24 20 44 89 44 24 18 48 89 54 24 10 48 89 4C 24 08 48 83 EC 48 48 8B 44 24 58";

        /*
         * Compared to other AwcFixupExe hooks, we don't actually have an unpacked executable yet. Instead,
         * the executable is only partially unpacked, and there's another function we need to hook.
         *
         * TODO: I'm not sure if this second step is part of AWC or not, so try to figure out if AwcFixupExe
         * is involved in any way. If it is, see if we can somehow hook this second step without a signature.
         */
        m_unknownFixupExeDetour = Mod.Instance!.Hooks!.CreateHook<UnknownFixupExeFunction>(
            DetourUnknownFixupExe,
            Mod.Instance!.PatternScanner.ScanOne(pattern)).Activate();

        return result;
    }

    private bool DetourUnknownFixupExe(void *unknown1, void *unknown2, uint unknown3, void *unknown4)
    {
        bool result = m_unknownFixupExeDetour!.OriginalFunction(unknown1, unknown2, unknown3, unknown4);

        const string pattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 41 56 48 83 EC 20 44 89 CB";

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
        m_context = new(hInstance, hPrevInstance, lpCmdLine, nShowCmd);
        StartEditor();
        return Environment.ExitCode;
    }

    public override void Initialize(IHook<NativeEntrypointFunction> hook)
    {
        base.Initialize(hook);

        /*
         * Reloaded injects before the executable entrypoint is called, so we'll need to load `Activation64`
         * manually.
         */
        LoadActivation();
        m_scanner = new PatternScanner(ActivationPath);

        /*
         * Nfs2016 only offers EA's DRM across all platforms, so we only need to hook Activation's FixupExe.
         * Before that, we'll need to configure the environment to ensure that the executable gets unpacked.
         */
        Environment.SetEnvironmentVariable("EARtPLaunchCode", RequiredToPlay.GenerateCode());
        Environment.SetEnvironmentVariable("ContentId", "1024486");

        const string pattern = "4C 89 4C 24 20 4C 89 44 24 18 48 89 54 24 10 55";

        /*
         * Next, we'll need to hook `AwcFixupExe`, which we'll locate through our pattern scanner. This will
         * not be called until we execute the original native entrypoint.
         */
        m_awcFixupExeDetour = Mod.Instance!.Hooks!.CreateHook<AwcFixupExeFunction>(
            DetourAwcFixupExe,
            m_scanner.ScanOne(pattern)).Activate();

        /*
         * This will call Activation's ordinal 100, which will check for `ContentId` and `EARtPLaunchCode`
         * in the environment before unpacking the executable.
         */
        hook.OriginalFunction();
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
