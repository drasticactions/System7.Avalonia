using Avalonia;

namespace System7.Avalonia.Controls;

/// <summary>One laid-out line of a text field, placed on whole pixels.</summary>
public sealed record System7TextLine(string Text, Thickness TextMargin, bool IsSelected, Thickness SelectionMargin, double SelectionWidth, double Height)
{
    /// <summary>The text's position inside the selection highlight, which clips the reversed copy of the line.</summary>
    public Thickness TextInSelectionMargin => new(TextMargin.Left - SelectionMargin.Left, TextMargin.Top - SelectionMargin.Top, 0, 0);
}
