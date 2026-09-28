using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace System7.Avalonia.Rendering;

/// <summary>
/// A QuickDraw round rectangle the size of the bound bounds. The parameter lists options separated by semicolons:
/// a number or "Half" (half the height) for the oval diameter, "Pen=n" to frame instead of fill, "Outline" for the pixels
/// that touch the outside, and "SquareTop" or "SquareBottom" to square eight rows as WDEF 1 does. As a multi-value converter,
/// the second value gives the diameter.
/// </summary>
public sealed class System7RoundRectConverter : IValueConverter, IMultiValueConverter
{
    public static System7RoundRectConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Rect bounds ? Build(bounds, null, parameter as string) : null;

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
        values.Count > 0 && values[0] is Rect bounds
            ? Build(bounds, values.Count > 1 ? values[1] switch { int i => i, double d => d, Rect r => (int)Math.Round(r.Height) / 2, _ => null } : null,
                parameter as string) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();

    private static Geometry Build(Rect bounds, double? diameter, string? options)
    {
        var width = (int)Math.Round(bounds.Width);
        var height = (int)Math.Round(bounds.Height);
        int pen = 0;
        bool outline = false, squareTop = false, squareBottom = false;
        foreach (var option in (options ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (option == "Half") diameter ??= height / 2;
            else if (option == "Outline") outline = true;
            else if (option == "SquareTop") squareTop = true;
            else if (option == "SquareBottom") squareBottom = true;
            else if (option.StartsWith("Pen=", StringComparison.Ordinal)) pen = int.Parse(option[4..], CultureInfo.InvariantCulture);
            else diameter ??= double.Parse(option, CultureInfo.InvariantCulture);
        }
        var d = diameter ?? 0;
        if (outline) return System7Geometry.OutlineRoundRect(0, 0, width, height, d, squareTop, squareBottom);
        if (squareTop || squareBottom) return System7Geometry.RoundRect(0, 0, width, height, d, squareTop, squareBottom);
        return pen > 0 ? System7Geometry.FrameRoundRect(0, 0, width, height, d, pen) : System7Geometry.RoundRect(0, 0, width, height, d);
    }
}
