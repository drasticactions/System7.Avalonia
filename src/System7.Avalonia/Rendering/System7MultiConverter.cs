using System.Globalization;
using Avalonia.Data.Converters;

namespace System7.Avalonia.Rendering;

/// <summary>A multi-value converter from a function of the values and the converter parameter.</summary>
internal sealed class System7MultiConverter(Func<IList<object?>, object?, object?> convert) : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) => convert(values, parameter);
}
