namespace FrostyEditor.Utilities;

/// <summary>
/// Provides Required-to-Play launch codes for use in Frosty's bootflow.
/// </summary>
internal static class RequiredToPlay
{
    /// <summary>
    /// Generates a Required-to-Play code via Universal Coordinated Time
    /// as a basis.
    /// </summary>
    /// <returns>The Required-to-Play launch code.</returns>
    public static string GenerateCode()
    {
        DateTime now = DateTime.UtcNow;
        var code = (uint)(
            (now.Year * 104729) ^ (now.Month * 224737) ^ (now.Day * 350377));

        return (code ^ ((code << 16) ^ (code >> 16))).ToString();
    }
}
