using Avalonia.Data.Converters;
using System.Globalization;

namespace Frosty.Ui.Converters;

internal class EnumToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ((value is Enum @enum && parameter is string @string) ?
        @enum.ToString().Equals(@string, StringComparison.OrdinalIgnoreCase) : false);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
