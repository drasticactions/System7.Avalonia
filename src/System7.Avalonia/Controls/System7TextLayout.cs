using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace System7.Avalonia.Controls;

/// <summary>
/// Lays out a TextBox's lines, selection, and caret on whole pixels as TextEdit draws them, from the invisible TextPresenter
/// that edits the text, and publishes them for the theme to draw.
/// </summary>
internal sealed class System7TextLayout : IDisposable
{
    private static readonly System7Attachments<TextBox, System7TextLayout> Instances = new(owner => new System7TextLayout(owner));
    private readonly TextBox owner;
    private readonly DispatcherTimer caretTimer = new();
    private TextPresenter? presenter;
    private bool caretVisible;

    public static void SetEnabled(TextBox box, bool enabled) => Instances.Set(box, enabled);

    private System7TextLayout(TextBox owner)
    {
        this.owner = owner;
        caretTimer.Tick += (_, _) =>
        {
            caretVisible = !caretVisible;
            Update();
        };
        owner.TemplateApplied += TemplateApplied;
        owner.PropertyChanged += OwnerChanged;
        owner.DetachedFromVisualTree += Detached;
        Attach(owner.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault(p => p.Name == "PART_TextPresenter"));
    }

    public void Dispose()
    {
        owner.TemplateApplied -= TemplateApplied;
        owner.PropertyChanged -= OwnerChanged;
        owner.DetachedFromVisualTree -= Detached;
        Attach(null);
        caretTimer.Stop();
    }

    private void TemplateApplied(object? sender, TemplateAppliedEventArgs e) => Attach(e.NameScope.Find<TextPresenter>("PART_TextPresenter"));

    private void Attach(TextPresenter? next)
    {
        if (presenter != null)
        {
            presenter.PropertyChanged -= PresenterChanged;
            presenter.LayoutUpdated -= PresenterLaidOut;
        }
        presenter = next;
        if (presenter != null)
        {
            presenter.PropertyChanged += PresenterChanged;
            presenter.LayoutUpdated += PresenterLaidOut;
        }
        ResetCaret();
    }

    private void Detached(object? sender, VisualTreeAttachmentEventArgs e) => caretTimer.Stop();

    private void PresenterLaidOut(object? sender, EventArgs e) => Update();

    private void PresenterChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextPresenter.PreeditTextProperty || e.Property == TextPresenter.PreeditTextCursorPositionProperty)
            ResetCaret();
        else if (e.Property == Visual.BoundsProperty) Update();
    }

    private void OwnerChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty || e.Property == TextBox.CaretIndexProperty ||
            e.Property == TextBox.SelectionStartProperty || e.Property == TextBox.SelectionEndProperty ||
            e.Property == TextBox.CaretBlinkIntervalProperty || e.Property == InputElement.IsFocusedProperty ||
            e.Property == InputElement.IsEffectivelyEnabledProperty)
            ResetCaret();
        else if (e.Property == TextBox.PasswordCharProperty || e.Property == TextBox.RevealPasswordProperty) Update();
    }

    private void ResetCaret()
    {
        caretTimer.Stop();
        caretVisible = owner is { IsFocused: true, IsEffectivelyEnabled: true };
        if (caretVisible && owner.SelectionStart == owner.SelectionEnd && owner.CaretBlinkInterval > TimeSpan.Zero)
        {
            caretTimer.Interval = owner.CaretBlinkInterval;
            caretTimer.Start();
        }
        Update();
    }

    private void Update()
    {
        var result = new List<System7TextLine>();
        var showCaret = false;
        System7TextLine? underCaret = null;
        var caretMargin = default(Thickness);
        var caretHeight = 0d;
        var textInCaret = default(Thickness);
        if (presenter != null)
        {
            var width = (int)Math.Ceiling(presenter.Bounds.Width);
            var height = (int)Math.Ceiling(presenter.Bounds.Height);
            var content = owner.Text ?? "";
            if (!string.IsNullOrEmpty(presenter.PreeditText))
                content = content.Insert(Math.Clamp(owner.CaretIndex, 0, content.Length), presenter.PreeditText);
            if (owner.PasswordChar != '\0' && !owner.RevealPassword) content = new string(owner.PasswordChar, content.Length);
            var layout = presenter.TextLayout;
            var top = 0;
            var selectionStart = Math.Min(owner.SelectionStart, owner.SelectionEnd);
            var selectionEnd = Math.Max(owner.SelectionStart, owner.SelectionEnd);
            var caretTop = 0;
            var caretX = 0;
            var caretBottom = 0;
            if (caretVisible && selectionStart == selectionEnd && width > 0)
            {
                var compositionOffset = presenter.PreeditText == null ? 0 : presenter.PreeditTextCursorPosition ?? presenter.PreeditText.Length;
                var position = layout.HitTestTextPosition(owner.CaretIndex + compositionOffset);
                caretX = Math.Clamp((int)Math.Round(position.X), 0, width - 1);
                caretTop = Math.Max(0, (int)Math.Round(position.Y));
                caretBottom = Math.Min(height, (int)Math.Round(position.Bottom));
                showCaret = caretBottom > caretTop;
            }
            foreach (var line in layout.TextLines)
            {
                var start = line.FirstTextSourceIndex;
                var length = Math.Min(line.Length - line.NewLineLength, content.Length - start);
                var lineHeight = (int)Math.Round(line.Height);
                var text = length >= 0 ? content.Substring(start, length) : "";
                var textMargin = new Thickness((int)Math.Round(line.Start) + 1, top + (int)Math.Round(line.Baseline) - 12, 0, 0);
                var selected = presenter.ShowSelectionHighlight && selectionStart < selectionEnd && selectionEnd >= start && selectionStart < start + line.Length;
                var left = 0;
                var right = 0;
                if (selected)
                {
                    left = selectionStart <= start ? 0 : (int)Math.Round(layout.HitTestTextPosition(selectionStart).X);
                    right = selectionEnd >= start + line.Length ? width : (int)Math.Round(layout.HitTestTextPosition(selectionEnd).X) + 1;
                }
                var entry = new System7TextLine(text, textMargin, selected && right > left, new Thickness(left, top, 0, 0), Math.Max(0, right - left), lineHeight);
                result.Add(entry);
                if (showCaret && caretTop < top + lineHeight && caretBottom > top) underCaret ??= entry;
                top += lineHeight;
            }
            caretMargin = new Thickness(caretX, caretTop, 0, 0);
            caretHeight = Math.Max(0, caretBottom - caretTop);
            if (underCaret != null)
                textInCaret = new Thickness(underCaret.TextMargin.Left - caretX, underCaret.TextMargin.Top - caretTop, 0, 0);
        }
        if (!System7Theme.GetTextLines(owner).SequenceEqual(result)) owner.SetValue(System7Theme.TextLinesProperty, result);
        owner.SetValue(System7Theme.ShowTextCaretProperty, showCaret);
        owner.SetValue(System7Theme.TextCaretMarginProperty, caretMargin);
        owner.SetValue(System7Theme.TextCaretHeightProperty, caretHeight);
        owner.SetValue(System7Theme.TextCaretLineProperty, underCaret?.Text ?? "");
        owner.SetValue(System7Theme.TextInCaretMarginProperty, textInCaret);
    }
}
