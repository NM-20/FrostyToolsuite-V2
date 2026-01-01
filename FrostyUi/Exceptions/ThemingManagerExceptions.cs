using Avalonia.Markup.Xaml;

namespace Frosty.Ui.Exceptions;

/// <summary>
/// An <see cref="Exception"/> thrown when any theming source couldn't be loaded. To address a specific
/// theming source failure, see
/// <see cref="ThemingManagerExternalLoadException"/>/<see cref="ThemingManagerInternalLoadException"/>.
/// </summary>
/// <param name="asset">The particular AXAML asset in which the inner exception occurred.</param>
/// <param name="inner">
/// The inner exception; that is, the <see cref="XamlLoadException"/> that caused this exception to be
/// thrown.
/// </param>
public class ThemingManagerLoadException(string asset, Exception inner) :
    Exception(null, inner)
{
    /// <summary>
    /// The particular AXAML asset in which the inner exception occurred.
    /// </summary>
    public string Asset => asset;
}

/// <summary>
/// An <see cref="Exception"/> thrown when an external theming source couldn't be loaded.
/// </summary>
/// <param name="asset">The particular AXAML asset in which the inner exception occurred.</param>
/// <param name="inner">
/// The inner exception; that is, the <see cref="XamlLoadException"/> that caused this exception to be
/// thrown.
/// </param>
public class ThemingManagerExternalLoadException(string asset, Exception inner) :
    ThemingManagerLoadException(asset, inner)
{
}

/// <summary>
/// An <see cref="Exception"/> thrown when an external theming source couldn't be loaded.
/// </summary>
/// <param name="asset">The particular AXAML asset in which the inner exception occurred.</param>
/// <param name="inner">
/// The inner exception; that is, the <see cref="XamlLoadException"/> that caused this exception to be
/// thrown.
/// </param>
public class ThemingManagerInternalLoadException(string asset, Exception inner) :
    ThemingManagerLoadException(asset, inner)
{
}
