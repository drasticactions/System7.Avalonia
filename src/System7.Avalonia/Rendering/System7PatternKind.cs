namespace System7.Avalonia.Rendering;

/// <summary>The QuickDraw patterns a shape or text can paint through.</summary>
public enum System7PatternKind
{
    Solid,
    /// <summary>QuickDraw gray: pixels where screen x + y is even.</summary>
    Gray,
    /// <summary>QuickDraw ltGray: pixels where screen x + 2y is a multiple of four.</summary>
    LightGray,
}
