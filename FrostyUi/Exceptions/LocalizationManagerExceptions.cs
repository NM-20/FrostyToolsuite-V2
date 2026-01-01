namespace Frosty.Ui.Exceptions;

/// <summary>
/// An <see cref="Exception"/> thrown whenever an error occurs during loading of a localization resource
/// such as a syntax error.
/// </summary>
/// <param name="inner">The <see cref="Exception"/> that denotes the error, i.e. a syntax error.</param>
public class LocalizationManagerLoadException(Exception inner) : Exception(null, inner)
{
}
