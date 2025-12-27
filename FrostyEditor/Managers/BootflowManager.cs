using Frosty.Sdk;
using FrostyEditor.Bootflow;
using FrostyEditor.Native;
using Reloaded.Hooks.Definitions;

namespace FrostyEditor.Managers;

internal static class BootflowManager
{
    /*
     * This is the entrypoint that is referred to within the optional header; we hook this to execute bootflows
     * as early as possible and to allow for the editor to be opened without a launcher open.
     */
    private static IHook<BootflowBase.NativeEntrypointFunction>? s_detour;

    /// <summary>
    /// The bootflow associated with the active game. Access this when a launch of the active game is required.
    /// </summary>
    public static BootflowBase? Bootflow { get; private set; }

    private static void DetourNativeEntrypoint()
    {
        /*
         * If we're at this point, `ProfilesLibrary` should be initialized. We'll need to retrieve the bootflow
         * specified within the effective profile first, then fetch its associated type.
         */
        Type? bootflow = Type.GetType($"FrostyEditor.Bootflow.{ProfilesLibrary.Bootflow}");
        if (bootflow is null)
        {
            return;
        }

        var instance = (BootflowBase)(Activator.CreateInstance(bootflow)!);

        Bootflow = instance;

        /*
         * Propagate the remaining initialization to the bootflow instance for any game-specific initialization.
         */
        Bootflow.Initialize(s_detour!);
    }

    public unsafe static void Initialize()
    {
        /*
         * Hooking Activation by itself wouldn't really allow the editor to be booted without an open launcher,
         * so we'll be hooking the entrypoint that calls Activation's ordinal 100 to allow for this.
         * This also allows for some cool customization later down the line, such as the user having an ability
         * to select whether or not they'd like to use V2's injection workflow or V1's external workflow.
         */
        nint address = GetModuleHandleW(null);
        var dos = (IMAGE_DOS_HEADER *)(address);

        var nt = (IMAGE_NT_HEADERS *)(address +
            dos->e_lfanew);
        var entrypoint = (nint)(nt->OptionalHeader.ImageBase + nt->OptionalHeader.AddressOfEntryPoint);

        s_detour =
            Mod.Instance!.Hooks!.CreateHook<BootflowBase.NativeEntrypointFunction>(DetourNativeEntrypoint, entrypoint).Activate();
    }
}
