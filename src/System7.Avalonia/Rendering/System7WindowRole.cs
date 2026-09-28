namespace System7.Avalonia.Rendering;

/// <summary>
/// WDEF 0's color table entries, named by where the frame uses them. Entries 16 to 36 are blends of the window colors,
/// so several parts share one entry; entries 40 to 55 and 56 to 71 ramp between two window colors for the box pictures.
/// </summary>
public enum System7WindowRole
{
    Content = 0,
    Frame = 1,
    TitleText = 2,
    InactiveTitleText = 17,
    Stripe = 18,
    InactiveFrame = 19,
    InactiveBevel = 21,
    TitleBar = 22,
    OuterFrame = 23,
    OuterBevelLight = 24,
    OuterBevelDark = 26,
    InnerFrame = 28,
    InnerBevelDark = 30,
    InnerBevelLight = 32,
    Highlight = 34,
    Shadow = 35,
    BoxFrame = 36,
    BoxFace = 50,
    PressedBoxDark = 56,
    PressedBoxMiddle = 61,
    PressedBoxLight = 71,
}
