using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using System7.Avalonia.Controls;
using System7.Avalonia.Rendering;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;

namespace System7.Avalonia;

public sealed partial class System7Theme : Styles
{
    [GeneratedAttachedProperty]
    public static partial bool GetUseNativeButtonTracking(Button button);

    [GeneratedAttachedProperty(DefaultValue = true)]
    public static partial bool GetIsButtonPointerWithin(Button button);

    [GeneratedAttachedProperty(DefaultValue = System7ColorDepth.Monochrome, Inherits = true)]
    public static partial System7ColorDepth GetColorDepth(Control control);

    public static readonly AttachedProperty<IBrush> WindowBackgroundProperty =
        AvaloniaProperty.RegisterAttached<System7Theme, Control, IBrush>("WindowBackground", Brushes.White, inherits: true);
    public static IBrush GetWindowBackground(Control control) => control.GetValue(WindowBackgroundProperty);
    public static void SetWindowBackground(Control control, IBrush value) => control.SetValue(WindowBackgroundProperty, value);

    public static readonly AttachedProperty<IBrush> SelectionBrushProperty =
        AvaloniaProperty.RegisterAttached<System7Theme, Control, IBrush>("SelectionBrush", Brushes.Black, inherits: true);
    public static IBrush GetSelectionBrush(Control control) => control.GetValue(SelectionBrushProperty);
    public static void SetSelectionBrush(Control control, IBrush value) => control.SetValue(SelectionBrushProperty, value);

    [GeneratedAttachedProperty]
    public static partial System7IconFamily? GetListIcon(ListBoxItem item);

    [GeneratedAttachedProperty(DefaultValue = true)]
    public static partial bool GetShowListLabel(ListBoxItem item);

    [GeneratedAttachedProperty]
    public static partial System7Icon? GetSmallListIcon(ListBoxItem item);

    [GeneratedAttachedProperty]
    public static partial bool GetSmallListIconOnRight(ListBoxItem item);

    [GeneratedAttachedProperty]
    public static partial bool GetSmallListDisabled(ListBoxItem item);

    [GeneratedAttachedProperty]
    public static partial IBrush? GetSmallListIconBrush(ListBoxItem item);

    [GeneratedAttachedProperty(Inherits = true)]
    public static partial System7TextStyle GetTextStyle(Control control);

    [GeneratedAttachedProperty]
    public static partial char GetMenuMark(MenuItem item);

    [GeneratedAttachedProperty]
    public static partial System7MenuIconKind GetMenuIconKind(MenuItem item);

    [GeneratedAttachedProperty]
    public static partial object? GetMonochromeIcon(MenuItem item);

    [GeneratedAttachedProperty(Inherits = true)]
    public static partial bool GetUseAliasedText(Visual visual);

    /// <summary>Whether a pop-up's chosen item blinks before it is selected, while the pop-up otherwise keeps Avalonia's behavior. Inherited.</summary>
    [GeneratedAttachedProperty(Inherits = true)]
    public static partial bool GetUsePopupFlash(Control control);

    /// <summary>Whether a menu's chosen item blinks before its command runs, while the menu otherwise keeps Avalonia's behavior. Inherited.</summary>
    [GeneratedAttachedProperty(Inherits = true)]
    public static partial bool GetUseMenuFlash(Control control);

    [GeneratedAttachedProperty(Inherits = true)]
    public static partial bool GetUseNativeMenuTracking(Control control);

    [GeneratedAttachedProperty]
    public static partial bool GetUseNativeTextEditing(TextBox box);

    [GeneratedAttachedProperty]
    public static partial bool GetUseNativeDialogKeys(Control control);

    [GeneratedAttachedProperty]
    public static partial bool GetIsDialogButtonPressed(Button button);

    [GeneratedAttachedProperty]
    public static partial bool GetUseNativeListTracking(ListBox list);

    [GeneratedAttachedProperty(Inherits = true)]
    public static partial bool GetUseNativePopupTracking(Control control);

    [GeneratedAttachedProperty(DefaultValue = "")]
    public static partial string GetPopupTitle(ComboBox box);

    [GeneratedAttachedProperty]
    public static partial double GetPopupTitleWidth(ComboBox box);

    [GeneratedAttachedProperty]
    public static partial bool GetIsPopupItemHighlighted(ComboBoxItem item);

    [GeneratedAttachedProperty(DefaultValue = true, Inherits = true)]
    public static partial bool GetIsActive(Control control);

    [GeneratedAttachedProperty(DefaultValue = 3)]
    public static partial int GetMenuFlashCount(Control control);

    [GeneratedAttachedProperty(DefaultValue = 50)]
    public static partial int GetMenuFlashInterval(Control control);

    public static readonly AttachedProperty<TimeSpan> SubmenuDelayProperty =
        AvaloniaProperty.RegisterAttached<System7Theme, Menu, TimeSpan>("SubmenuDelay", TimeSpan.FromSeconds(8d / 60));
    public static TimeSpan GetSubmenuDelay(Menu menu) => menu.GetValue(SubmenuDelayProperty);
    public static void SetSubmenuDelay(Menu menu, TimeSpan value) => menu.SetValue(SubmenuDelayProperty, value);

    public static readonly AttachedProperty<TimeSpan> SubmenuAimDelayProperty =
        AvaloniaProperty.RegisterAttached<System7Theme, Menu, TimeSpan>("SubmenuAimDelay", TimeSpan.FromSeconds(1));
    public static TimeSpan GetSubmenuAimDelay(Menu menu) => menu.GetValue(SubmenuAimDelayProperty);
    public static void SetSubmenuAimDelay(Menu menu, TimeSpan value) => menu.SetValue(SubmenuAimDelayProperty, value);

    public static readonly AttachedProperty<IBrush> MenuMarkBrushProperty =
        AvaloniaProperty.RegisterAttached<System7Theme, Control, IBrush>("MenuMarkBrush", Brushes.Black);
    /// <summary>The color of a menu item's mark.</summary>
    public static IBrush GetMenuMarkBrush(Control control) => control.GetValue(MenuMarkBrushProperty);
    public static void SetMenuMarkBrush(Control control, IBrush value) => control.SetValue(MenuMarkBrushProperty, value);

    public static readonly AttachedProperty<IBrush> MenuShortcutBrushProperty =
        AvaloniaProperty.RegisterAttached<System7Theme, Control, IBrush>("MenuShortcutBrush", Brushes.Black);
    /// <summary>The color of a menu item's command key and submenu arrow.</summary>
    public static IBrush GetMenuShortcutBrush(Control control) => control.GetValue(MenuShortcutBrushProperty);
    public static void SetMenuShortcutBrush(Control control, IBrush value) => control.SetValue(MenuShortcutBrushProperty, value);

    /// <summary>Whether a menu item's cicn is drawn from its one-bit member, which keeps its bits when the item is selected.</summary>
    [GeneratedAttachedProperty]
    public static partial bool GetIsMenuIconMember(MenuItem item);

    /// <summary>Whether a pop-up menu item is a separator line.</summary>
    [GeneratedAttachedProperty]
    public static partial bool GetIsMenuSeparator(Control control);

    /// <summary>Whether a pop-up's current item is enabled; otherwise CDEF 63 grays the item's text and the triangle.</summary>
    [GeneratedAttachedProperty(DefaultValue = true)]
    public static partial bool GetIsPopupSelectionEnabled(ComboBox box);

    /// <summary>Whether a menu title is the Apple menu's, which a color screen shows as the color Apple icon.</summary>
    [GeneratedAttachedProperty]
    public static partial bool GetIsAppleMenu(MenuItem item);

    public static readonly AttachedProperty<IReadOnlyList<System7TextLine>> TextLinesProperty =
        AvaloniaProperty.RegisterAttached<System7Theme, TextBox, IReadOnlyList<System7TextLine>>("TextLines", []);
    /// <summary>The text field's lines as TextEdit places them.</summary>
    public static IReadOnlyList<System7TextLine> GetTextLines(TextBox box) => box.GetValue(TextLinesProperty);

    /// <summary>Whether TextEdit's blinking caret is showing.</summary>
    [GeneratedAttachedProperty]
    public static partial bool GetShowTextCaret(TextBox box);

    [GeneratedAttachedProperty]
    public static partial Thickness GetTextCaretMargin(TextBox box);

    [GeneratedAttachedProperty]
    public static partial double GetTextCaretHeight(TextBox box);

    /// <summary>The line under the caret, drawn again inside the caret in the background color as TextEdit's XOR caret shows it.</summary>
    [GeneratedAttachedProperty(DefaultValue = "")]
    public static partial string GetTextCaretLine(TextBox box);

    [GeneratedAttachedProperty]
    public static partial Thickness GetTextInCaretMargin(TextBox box);

    [GeneratedAttachedProperty]
    public static partial bool GetUseNativeScrollTracking(ScrollBar bar);

    /// <summary>The colors CDEF 1 draws the bar in, from its brushes and the theme's shades.</summary>
    [GeneratedAttachedProperty]
    public static partial System7ScrollBarScheme? GetScrollBarScheme(ScrollBar bar);

    /// <summary>Whether the bar is enabled and has a range, so it shows its pattern and thumb.</summary>
    [GeneratedAttachedProperty]
    public static partial bool GetIsScrollLive(ScrollBar bar);

    /// <summary>The part held down with the pointer still inside it.</summary>
    [GeneratedAttachedProperty]
    public static partial System7ScrollPart GetScrollHighlight(ScrollBar bar);

    /// <summary>Where CDEF 1 draws the thumb.</summary>
    [GeneratedAttachedProperty]
    public static partial Thickness GetScrollThumbMargin(ScrollBar bar);

    /// <summary>The ring of the thumb's drag outline, or null while no outline shows.</summary>
    [GeneratedAttachedProperty]
    public static partial Geometry? GetScrollDragOutline(ScrollBar bar);

    [GeneratedAttachedProperty]
    public static partial System7ProgressRetention GetProgressRetention(ProgressBar bar);

    /// <summary>The filled fraction of the value CDEF 62 last drew.</summary>
    [GeneratedAttachedProperty]
    public static partial double GetProgressFraction(ProgressBar bar);

    /// <summary>Whether the bar's pixels are on the screen: false after they are erased while CDEF 62 cannot redraw them.</summary>
    [GeneratedAttachedProperty]
    public static partial bool GetIsProgressDrawn(ProgressBar bar);

    /// <summary>The speaker ICON shown, from 0 to 5, or -1 for none.</summary>
    [GeneratedAttachedProperty(DefaultValue = -1)]
    public static partial int GetSpeakerFrame(ProgressBar bar);

    static System7Theme()
    {
        UseNativeTextEditingProperty.Changed.AddClassHandler<TextBox>((box, change) => System7TextLayout.SetEnabled(box, change.GetNewValue<bool>()));
        UseNativeScrollTrackingProperty.Changed.AddClassHandler<ScrollBar>((bar, change) => System7ScrollBarTracking.SetEnabled(bar, change.GetNewValue<bool>()));
        ProgressRetentionProperty.Changed.AddClassHandler<ProgressBar>((bar, change) =>
            System7ProgressRetentionState.SetKind(bar, change.GetNewValue<System7ProgressRetention>()));
        UseNativeButtonTrackingProperty.Changed.AddClassHandler<Button>((button, change) => System7ButtonTracking.SetEnabled(button, change.GetNewValue<bool>()));
        System7TextEditing.Initialize();
        System7DialogKeys.Initialize();
        System7ListTracking.Initialize();
        UseAliasedTextProperty.Changed.AddClassHandler<Visual>((visual, change) =>
            TextOptions.SetTextRenderingMode(visual, change.GetNewValue<bool>() ? TextRenderingMode.Alias : TextRenderingMode.Unspecified));
        UsePopupFlashProperty.Changed.AddClassHandler<ComboBox>((box, change) => System7PopupFlash.SetEnabled(box, change.GetNewValue<bool>()));
        UseMenuFlashProperty.Changed.AddClassHandler<Menu>((menu, change) => System7MenuFlash.SetEnabled(menu, change.GetNewValue<bool>()));
        UseNativeMenuTrackingProperty.Changed.AddClassHandler<Menu>((menu, change) => System7MenuTracking.SetEnabled(menu, change.GetNewValue<bool>()));
        UseNativePopupTrackingProperty.Changed.AddClassHandler<ComboBox>((control, change) => System7PopupTracking.SetEnabled(control, change.GetNewValue<bool>()));
    }

    public System7Theme(IServiceProvider? provider = null) => AvaloniaXamlLoader.Load(provider, this);
}
