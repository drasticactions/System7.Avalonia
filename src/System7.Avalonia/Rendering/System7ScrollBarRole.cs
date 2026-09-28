namespace System7.Avalonia.Rendering;

/// <summary>CDEF 1's color table entries, named by where the scroll bar uses them. Several parts share one blended entry.</summary>
public enum System7ScrollBarRole
{
    Frame = 0,
    Background = 1,
    ThumbFace = 18,
    Track = 22,
    /// <summary>The track's ltGray pattern, the arrows' lower and right edges, and the outline of an idle arrow.</summary>
    TrackPattern = 28,
    ThumbTop = 29,
    /// <summary>The whole bar while its window is active, and an idle arrow's face.</summary>
    Surface = 32,
    ArrowFace = 33,
    ThumbLight = 34,
    /// <summary>The arrow's fill and the grip's top row.</summary>
    ArrowFill = 35,
    Grip = 36,
    /// <summary>The thumb's lower and right edges and the arrow's outline.</summary>
    ThumbDark = 37,
}
