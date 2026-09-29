using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System7.Avalonia;
using System7.Avalonia.Controls;
using System7.Demo;
using System7.Avalonia.Rendering;
using Avalonia.Layout;
using Avalonia.Media;

AppBuilder.Configure<OracleApp>().UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();

if (args is ["capture", _]) return Oracle.Capture(args[1], null);
if (args is ["capture", _, _]) return Oracle.Capture(args[1], args[2]);
if (args is ["compare", _, _, _]) return Oracle.Compare(args[1], args[2], args[3]);
Console.Error.WriteLine("usage: capture <dir> [filter] | compare <golden> <actual> <diffdir>");
return 2;

internal static class Oracle
{
    private static readonly string[] DepthLabels = ["Monochrome", "4 grays", "16 colors", "256 colors", "Thousands", "Millions"];
    private static string output = "";
    private static string? filter;
    private static int count;

    public static int Capture(string directory, string? only)
    {
        output = directory;
        filter = only;
        Directory.CreateDirectory(output);
        foreach (var depth in DepthLabels) DemoStates(depth);
        Mobile();
        ShortMenus();
        Sheet();
        Additions();
        Console.WriteLine($"{count} images");
        return 0;
    }

    private static bool Wanted(string name) => filter == null || name.Contains(filter, StringComparison.Ordinal);

    private static (Window window, DemoView view) OpenDemo(double width)
    {
        var view = new DemoView { SingleColumn = true };
        // The golden images were captured with System 7 menu and pop-up tracking, which the demo leaves off.
        foreach (var bar in view.GetLogicalDescendants().OfType<Menu>()) System7Theme.SetUseNativeMenuTracking(bar, true);
        foreach (var popup in view.GetLogicalDescendants().OfType<ComboBox>()) System7Theme.SetUseNativePopupTracking(popup, true);
        var window = new Window { Width = width, Height = 800, Content = view };
        window.Show();
        typeof(DemoView).GetMethod("SetScale", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(view, [1]);
        Settle(window);
        var scroll = view.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.Content is DemoColumns);
        window.Height = Math.Ceiling(scroll.Extent.Height + 40);
        Settle(window);
        scroll.Offset = default;
        Settle(window);
        return (window, view);
    }

    private static void Settle(Window window)
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using (window.CaptureRenderedFrame()) { }
        }
    }

    private static void Snap(Window window, string name, Visual? crop = null, double inflate = 6)
    {
        if (!Wanted(name)) return;
        Settle(window);
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame.");
        var full = new PixelRect(0, 0, frame.PixelSize.Width, frame.PixelSize.Height);
        var rect = full;
        if (crop != null)
        {
            var origin = crop.TranslatePoint(default, window) ?? default;
            var bounds = new Rect(origin, crop.Bounds.Size).Inflate(inflate);
            rect = new PixelRect((int)Math.Floor(bounds.X), (int)Math.Floor(bounds.Y), (int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height));
            rect = rect.Intersect(full);
        }
        if (rect.Width <= 0 || rect.Height <= 0) return;
        var stride = rect.Width * 4;
        var buffer = Marshal.AllocHGlobal(stride * rect.Height);
        try
        {
            frame.CopyPixels(rect, buffer, stride * rect.Height, stride);
            using var bitmap = new Bitmap(frame.Format ?? PixelFormat.Bgra8888, AlphaFormat.Premul, buffer, rect.Size, new Vector(96, 96), stride);
            bitmap.Save(Path.Combine(output, name + ".png"), new PngBitmapEncoderOptions());
        }
        finally { Marshal.FreeHGlobal(buffer); }
        count++;
    }

    private static T Named<T>(Visual root, string content) where T : ContentControl =>
        root.GetVisualDescendants().OfType<T>().First(c => Equals(c.Content, content));

    private static IEnumerable<ComboBox> DepthCombos(Visual root) => root.GetVisualDescendants().OfType<ComboBox>()
        .Where(c => c.Items.OfType<string>().Contains("Monochrome"));

    private static void SetDepth(Visual root, string label)
    {
        foreach (var combo in DepthCombos(root)) combo.SelectedIndex = combo.Items.OfType<string>().ToList().IndexOf(label);
        foreach (var check in root.GetVisualDescendants().OfType<CheckBox>().Where(c => Equals(c.Content, "Color")))
            check.IsChecked = label != "Monochrome";
    }

    private static void SetToggles(Visual root, bool on)
    {
        foreach (var check in root.GetVisualDescendants().OfType<CheckBox>()
                     .Where(c => c.Content is "Disabled" or "Inactive" or "Custom" or "Custom colors").ToList())
            if (check.IsEffectivelyEnabled || !on) check.IsChecked = on;
    }

    private static string Slug(string text) => new(text.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray());

    private static void DemoStates(string depth)
    {
        var tag = Slug(depth);
        var (window, view) = OpenDemo(1280);
        SetDepth(view, depth);
        Snap(window, $"demo-{tag}");
        SetToggles(view, true);
        Snap(window, $"demo-{tag}-toggled");
        SetToggles(view, false);
        Settle(window);

        var highlight = view.GetVisualDescendants().OfType<ComboBox>().FirstOrDefault(c => System7Theme.GetPopupTitle(c) == "Select:");
        var paper = view.GetVisualDescendants().OfType<ComboBox>().FirstOrDefault(c => System7Theme.GetPopupTitle(c) == "Paper:");
        if (highlight != null && paper != null && depth != "Monochrome")
        {
            var list = view.GetVisualDescendants().OfType<ListBox>().First(l => global::Avalonia.Automation.AutomationProperties.GetAutomationId(l) == "color-list");
            for (var i = 0; i < 6; i++)
            {
                highlight.SelectedIndex = i;
                paper.SelectedIndex = i % 3;
                Snap(window, $"list-{tag}-highlight{i}", list.Parent as Visual);
            }
            highlight.SelectedIndex = paper.SelectedIndex = 0;
        }

        var kinds = view.GetVisualDescendants().OfType<ComboBox>().FirstOrDefault(c => c.Items.OfType<string>().Contains("Movable"));
        if (kinds != null)
            for (var i = 0; i < kinds.ItemCount; i++)
            {
                kinds.SelectedIndex = i;
                Snap(window, $"dialogs-{tag}-{i}", kinds.Parent as Visual);
            }
        var diameters = view.GetVisualDescendants().OfType<ComboBox>().FirstOrDefault(c => c.Items.OfType<string>().Contains("24 px"));
        if (diameters != null)
            for (var i = 0; i < diameters.ItemCount; i++)
            {
                diameters.SelectedIndex = i;
                Snap(window, $"rounded-{tag}-{i}", diameters.Parent as Visual);
            }

        Menus(window, view, tag);
        foreach (var custom in view.GetVisualDescendants().OfType<CheckBox>().Where(c => Equals(c.Content, "Custom colors") && c.IsEffectivelyEnabled))
        {
            custom.IsChecked = true;
            Menus(window, view, tag + "-custom");
            custom.IsChecked = false;
        }
        Popups(window, view, tag);
        Presses(window, view, tag);
        ScrollBars(window, view, tag);
        Windows(window, view, tag);
        Sliders(window, view, tag);
        Text(window, view, tag);
        window.Close();
    }

    private static void Menus(Window window, DemoView view, string tag)
    {
        var menu = view.GetVisualDescendants().OfType<Menu>().First();
        var titles = menu.Items.OfType<MenuItem>().ToList();
        foreach (var (title, index) in titles.Select((t, i) => (t, i)))
        {
            title.IsSubMenuOpen = true;
            Snap(window, $"menu-{tag}-{index}");
            var items = title.Items.OfType<MenuItem>().ToList();
            foreach (var (item, itemIndex) in items.Select((t, i) => (t, i)))
            {
                if (item.ItemCount == 0 || !item.IsEffectivelyEnabled) continue;
                item.IsSubMenuOpen = true;
                Snap(window, $"menu-{tag}-{index}-{itemIndex}");
                foreach (var (child, childIndex) in item.Items.OfType<MenuItem>().Select((t, i) => (t, i)))
                {
                    if (child.ItemCount == 0 || !child.IsEffectivelyEnabled) continue;
                    child.IsSubMenuOpen = true;
                    Snap(window, $"menu-{tag}-{index}-{itemIndex}-{childIndex}");
                    foreach (var (leaf, leafIndex) in child.Items.OfType<MenuItem>().Select((t, i) => (t, i)))
                    {
                        if (leaf.ItemCount == 0) continue;
                        leaf.IsSubMenuOpen = true;
                        Snap(window, $"menu-{tag}-{index}-{itemIndex}-{childIndex}-{leafIndex}");
                        leaf.IsSubMenuOpen = false;
                    }
                    child.IsSubMenuOpen = false;
                }
                item.IsSubMenuOpen = false;
            }
            title.IsSubMenuOpen = false;
            Settle(window);
        }

        // Native tracking: press a title, drag over each item, and release outside every menu.
        foreach (var (title, index) in titles.Select((t, i) => (t, i)))
        {
            var start = title.TranslatePoint(new Point(title.Bounds.Width / 2, 10), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            Settle(window);
            var rows = title.Items.OfType<MenuItem>().ToList();
            for (var i = 0; i < rows.Count && i < 12; i++)
            {
                if (!rows[i].IsVisible || rows[i].Bounds.Height <= 0) continue;
                var point = rows[i].TranslatePoint(new Point(20, rows[i].Bounds.Height / 2), window);
                if (point == null) continue;
                window.MouseMove(point.Value, RawInputModifiers.LeftMouseButton);
                Snap(window, $"track-{tag}-{index}-{i}");
            }
            window.MouseMove(new Point(window.Width - 2, window.Height - 2), RawInputModifiers.LeftMouseButton);
            window.MouseUp(new Point(window.Width - 2, window.Height - 2), MouseButton.Left);
            Settle(window);
            for (var i = 0; i < 40; i++) Pump();
        }
    }

    private static void Pump()
    {
        using var cancel = new CancellationTokenSource(5);
        Dispatcher.UIThread.MainLoop(cancel.Token);
    }

    private static void Popups(Window window, DemoView view, string tag)
    {
        foreach (var (combo, index) in view.GetVisualDescendants().OfType<ComboBox>().Where(c => c.IsEffectivelyEnabled).ToList().Select((c, i) => (c, i)))
        {
            combo.IsDropDownOpen = true;
            Snap(window, $"popup-{tag}-{index}", combo.Parent as Visual, 120);
            combo.IsDropDownOpen = false;
            Settle(window);
        }
    }

    private static void Presses(Window window, DemoView view, string tag)
    {
        var controls = view.GetVisualDescendants().OfType<Button>().Cast<Control>()
            .Concat(view.GetVisualDescendants().OfType<ToggleButton>())
            .Where(c => c is { IsEffectivelyVisible: true, Bounds.Width: > 0 } && c.FindAncestorOfType<Menu>() == null
                                                                               && c.FindAncestorOfType<ComboBox>() == null && c.FindAncestorOfType<ScrollBar>() == null)
            .Distinct().ToList();
        for (var i = 0; i < controls.Count; i++)
        {
            var control = controls[i];
            var center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window);
            if (center == null) continue;
            window.MouseDown(center.Value, MouseButton.Left);
            Snap(window, $"press-{tag}-{i}", control, 8);
            var outside = control.TranslatePoint(new Point(-30, -30), window)!.Value;
            window.MouseMove(outside, RawInputModifiers.LeftMouseButton);
            Snap(window, $"press-{tag}-{i}-out", control, 8);
            window.MouseUp(outside, MouseButton.Left);
            Settle(window);
        }
    }

    private static void ScrollBars(Window window, DemoView view, string tag)
    {
        var bars = view.GetVisualDescendants().OfType<ScrollBar>().Where(b => b is { IsEffectivelyVisible: true, Bounds.Width: > 0 }).ToList();
        for (var i = 0; i < bars.Count; i++)
        {
            var bar = bars[i];
            var horizontal = bar.Orientation == Avalonia.Layout.Orientation.Horizontal;
            var length = horizontal ? bar.Bounds.Width : bar.Bounds.Height;
            var old = bar.Value;
            foreach (var at in new[] { 8.0, 24, length / 2, length - 24, length - 8 })
            {
                var local = horizontal ? new Point(at, 8) : new Point(8, at);
                var point = bar.TranslatePoint(local, window)!.Value;
                window.MouseDown(point, MouseButton.Left);
                Snap(window, $"scroll-{tag}-{i}-{(int)at}", bar, 4);
                window.MouseUp(point, MouseButton.Left);
                Settle(window);
                bar.Value = old;
            }
            bar.Value = (bar.Minimum + bar.Maximum) / 2;
            Settle(window);
            var grab = bar.TranslatePoint(horizontal ? new Point(length / 2, 8) : new Point(8, length / 2), window)!.Value;
            var moved = grab + (horizontal ? new Point(20, 0) : new Point(0, 20));
            window.MouseDown(grab, MouseButton.Left);
            window.MouseMove(moved, RawInputModifiers.LeftMouseButton);
            Snap(window, $"scroll-{tag}-{i}-thumbdrag", bar, 4);
            window.MouseUp(moved, MouseButton.Left);
            Settle(window);
            bar.Value = old;
            foreach (var value in new[] { bar.Minimum, (bar.Minimum + bar.Maximum) / 2, bar.Maximum })
            {
                bar.Value = value;
                Snap(window, $"scroll-{tag}-{i}-value{(int)value}", bar, 4);
            }
            bar.Value = old;
        }
    }

    private static void Windows(Window window, DemoView view, string tag)
    {
        var frames = view.GetVisualDescendants().OfType<System7WindowFrame>().Where(f => f.Parent is System7Desktop).ToList();
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            var desktop = (System7Desktop)frame.Parent!;
            if (frame.HasTitleBar)
            {
                var title = frame.TranslatePoint(new Point(frame.Bounds.Width / 2, 9), window)!.Value;
                window.MouseDown(title, MouseButton.Left);
                window.MouseMove(title + new Vector(7, 5), RawInputModifiers.LeftMouseButton);
                Snap(window, $"drag-{tag}-{i}", desktop, 2);
                window.MouseMove(title, RawInputModifiers.LeftMouseButton);
                window.MouseUp(title, MouseButton.Left);
                Snap(window, $"drag-{tag}-{i}-done", desktop, 2);
            }
            if (frame.HasCloseButton)
            {
                var close = frame.TranslatePoint(new Point(14, 10), window)!.Value;
                window.MouseDown(close, MouseButton.Left);
                Snap(window, $"close-{tag}-{i}", frame, 2);
                window.MouseMove(close + new Vector(40, 0), RawInputModifiers.LeftMouseButton);
                window.MouseUp(close + new Vector(40, 0), MouseButton.Left);
                Settle(window);
            }
            if (frame.HasZoomButton)
            {
                var zoom = frame.TranslatePoint(new Point(frame.Bounds.Width - 16, 10), window)!.Value;
                window.MouseDown(zoom, MouseButton.Left);
                Snap(window, $"zoom-{tag}-{i}", frame, 2);
                window.MouseMove(zoom - new Vector(40, 0), RawInputModifiers.LeftMouseButton);
                window.MouseUp(zoom - new Vector(40, 0), MouseButton.Left);
                Settle(window);
            }
            if (frame.HasResizeGrip)
            {
                var grip = frame.TranslatePoint(new Point(frame.Bounds.Width - 8, frame.Bounds.Height - 8), window)!.Value;
                window.MouseDown(grip, MouseButton.Left);
                window.MouseMove(grip + new Vector(9, 6), RawInputModifiers.LeftMouseButton);
                Snap(window, $"grow-{tag}-{i}", desktop, 2);
                window.MouseMove(grip, RawInputModifiers.LeftMouseButton);
                window.MouseUp(grip, MouseButton.Left);
                Settle(window);
            }
        }
    }

    private static void Sliders(Window window, DemoView view, string tag)
    {
        foreach (var (slider, index) in view.GetVisualDescendants().OfType<Slider>().ToList().Select((s, i) => (s, i)))
        {
            var old = slider.Value;
            for (var step = 0; step <= 4; step++)
            {
                slider.Value = slider.Minimum + (slider.Maximum - slider.Minimum) * step / 4;
                Snap(window, $"slider-{tag}-{index}-{step}", slider, 4);
            }
            slider.Value = old;
        }
        foreach (var (bar, index) in view.GetVisualDescendants().OfType<ProgressBar>().ToList().Select((s, i) => (s, i)))
        {
            var old = bar.Value;
            foreach (var value in new[] { 0.0, 1, 50, 99, 100 })
            {
                bar.Value = value;
                Snap(window, $"progress-{tag}-{index}-{(int)value}", bar, 4);
            }
            bar.Value = old;
        }
    }

    private static void Text(Window window, DemoView view, string tag)
    {
        foreach (var (box, index) in view.GetVisualDescendants().OfType<TextBox>().Where(t => t.IsEffectivelyVisible).ToList().Select((t, i) => (t, i)))
        {
            box.Focus();
            box.SelectionStart = 2;
            box.SelectionEnd = Math.Min(9, box.Text?.Length ?? 0);
            Snap(window, $"text-{tag}-{index}-selected", box, 4);
            box.SelectionStart = box.SelectionEnd = 3;
            box.CaretIndex = 3;
            Snap(window, $"text-{tag}-{index}-caret", box, 4);
            box.SelectionStart = box.SelectionEnd = 0;
        }
        view.Focus();
        foreach (var (list, index) in view.GetVisualDescendants().OfType<ListBox>().ToList().Select((l, i) => (l, i)))
        {
            var old = list.SelectedIndex;
            for (var i = 0; i < Math.Min(3, list.ItemCount); i++)
            {
                list.SelectedIndex = i;
                Snap(window, $"listsel-{tag}-{index}-{i}", list, 4);
            }
            list.ScrollIntoView(list.ItemCount - 1);
            Snap(window, $"listsel-{tag}-{index}-end", list, 4);
            list.ScrollIntoView(0);
            list.SelectedIndex = old;
        }
    }

    private static void ShortMenus()
    {
        var view = new DemoView { SingleColumn = true };
        // The golden images were captured with System 7 menu and pop-up tracking, which the demo leaves off.
        foreach (var bar in view.GetLogicalDescendants().OfType<Menu>()) System7Theme.SetUseNativeMenuTracking(bar, true);
        foreach (var popup in view.GetLogicalDescendants().OfType<ComboBox>()) System7Theme.SetUseNativePopupTracking(popup, true);
        var window = new Window { Width = 640, Height = 360, Content = view };
        window.Show();
        typeof(DemoView).GetMethod("SetScale", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(view, [1]);
        Settle(window);
        var menu = view.GetVisualDescendants().OfType<Menu>().First();
        var edit = menu.Items.OfType<MenuItem>().First(item => Equals(item.Header, "Edit"));
        var scrolling = edit.Items.OfType<MenuItem>().First(item => Equals(item.Header, "Scroll menu"));
        edit.IsSubMenuOpen = true;
        scrolling.IsSubMenuOpen = true;
        Snap(window, "short-scroll-menu");
        var surface = scrolling.GetVisualDescendants().OfType<Visual>().Concat(TopLevel.GetTopLevel(window)!.GetVisualDescendants())
            .FirstOrDefault(v => v.GetType().Name == "System7MenuSurface" && v.GetVisualDescendants().OfType<MenuItem>().Any(i => Equals(i.Header, "Item 01")));
        if (surface != null)
            foreach (var offset in new[] { 16.0, 48, 400 })
            {
                surface.GetType().GetProperty("ScrollOffset")!.SetValue(surface, offset);
                Snap(window, $"short-scroll-menu-{(int)offset}");
            }
        scrolling.IsSubMenuOpen = false;
        edit.IsSubMenuOpen = false;
        window.Close();
    }

    private static void Mobile()
    {
        foreach (var depth in new[] { "Monochrome", "256 colors" })
        {
            var (window, view) = OpenDemo(390);
            SetDepth(view, depth);
            Snap(window, $"mobile-{Slug(depth)}");
            window.Close();
        }
    }

    private static readonly (string Tag, System7ColorDepth Depth)[] AdditionDepths =
        [("monochrome", System7ColorDepth.Monochrome), ("256-colors", System7ColorDepth.Indexed8)];

    // The art the fork adds for a compositor: title strips, the grow box, the right end of the menu bar, icons built from ARGB, cursors and desktop patterns.
    private static void Additions()
    {
        foreach (var (tag, depth) in AdditionDepths)
        {
            var strips = new StackPanel { Spacing = 8, Margin = new Thickness(8) };
            foreach (var active in new[] { true, false })
                strips.Children.Add(new System7WindowFrame
                {
                    IsTitleStrip = true, Title = active ? "Active strip" : "Inactive strip", IsActive = active,
                    CanClose = true, CanZoom = true, CanResize = true, Width = 220,
                });
            strips.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 8,
                Children = { new System7GrowBox(), new Border { BorderBrush = Brushes.Black, BorderThickness = new Thickness(1), Child = new System7GrowBox() } },
            });
            var menu = new Menu { Width = 320 };
            menu.Items.Add(new MenuItem { Header = "File" });
            menu.Items.Add(new MenuItem { Header = "Edit" });
            var clock = new MenuItem { Header = "12:00" };
            System7Theme.SetMenuBarDock(clock, HorizontalAlignment.Right);
            var app = new MenuItem { Header = "App" };
            System7Theme.SetMenuBarDock(app, HorizontalAlignment.Right);
            menu.Items.Add(clock);
            menu.Items.Add(app);
            strips.Children.Add(menu);
            var icons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var family = System7IconFamily.FromArgb(Gradient(32), Gradient(16));
            icons.Children.Add(new System7IconView { Icon = family });
            icons.Children.Add(new System7IconView { Icon = family.Small });
            strips.Children.Add(icons);
            System7Theme.SetColorDepth(strips, depth);
            var window = new Window { Width = 360, Height = 240, Content = strips, Background = Brushes.White };
            window.Show();
            Snap(window, $"additions-{tag}");
            window.Close();
        }

        var cursors = CursorImages(4);
        var sheet = new WrapPanel { Margin = new Thickness(8) };
        foreach (var image in cursors) sheet.Children.Add(new Image { Source = image, Width = 64, Height = 64, Margin = new Thickness(4) });
        var cursorWindow = new Window { Width = 480, Height = 240, Content = sheet, Background = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)) };
        cursorWindow.Show();
        Snap(cursorWindow, "additions-cursors");
        cursorWindow.Close();

        var patterns = new WrapPanel { Margin = new Thickness(4) };
        foreach (var pattern in System7DesktopPattern.All)
            patterns.Children.Add(new Image { Source = Tile(pattern), Width = 48, Height = 48, Margin = new Thickness(2) });
        var patternWindow = new Window { Width = 832, Height = 700, Content = patterns, Background = Brushes.White };
        patternWindow.Show();
        Snap(patternWindow, "additions-patterns");
        patternWindow.Close();
    }

    private static List<WriteableBitmap> CursorImages(int scale)
    {
        var result = new List<WriteableBitmap>();
        foreach (var cursor in new[] { System7Cursor.Arrow, System7Cursor.IBeam, System7Cursor.Crosshair, System7Cursor.Plus, System7Cursor.Watch })
            foreach (var frame in cursor.Frames)
            {
                var size = System7CursorFrame.Size * scale;
                var bitmap = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
                using (var buffer = bitmap.Lock())
                {
                    var row = new int[size];
                    for (var y = 0; y < size; y++)
                    {
                        for (var x = 0; x < size; x++)
                            row[x] = x / scale == frame.HotSpotX && y / scale == frame.HotSpotY ? unchecked((int)0xFFFF0000) : (int)frame.Argb(x / scale, y / scale);
                        Marshal.Copy(row, 0, buffer.Address + y * buffer.RowBytes, size);
                    }
                }
                result.Add(bitmap);
            }
        return result;
    }

    private static uint[] Gradient(int size)
    {
        var pixels = new uint[size * size];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var inside = (x - size / 2.0) * (x - size / 2.0) + (y - size / 2.0) * (y - size / 2.0) < size * size / 4.4;
                pixels[y * size + x] = inside ? 0xFF000000u | (uint)(x * 255 / size) << 16 | (uint)(y * 255 / size) << 8 | 0xC0 : 0;
            }
        return pixels;
    }

    private static WriteableBitmap Tile(System7DesktopPattern pattern)
    {
        var bitmap = new WriteableBitmap(new PixelSize(48, 48), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var buffer = bitmap.Lock();
        var row = new int[48];
        for (var y = 0; y < 48; y++)
        {
            for (var x = 0; x < 48; x++) row[x] = (int)pattern.Argb(x, y);
            Marshal.Copy(row, 0, buffer.Address + y * buffer.RowBytes, 48);
        }
        return bitmap;
    }

    private static void Sheet()
    {
        foreach (var depth in Enum.GetValues<System7.Avalonia.Rendering.System7ColorDepth>())
        {
            var panel = new WrapPanel { Margin = new Thickness(7, 5, 0, 0), Width = 900 };
            System7Theme.SetColorDepth(panel, depth);
            void Add(Control c) { c.Margin = new Thickness(4); panel.Children.Add(c); }
            Add(new Button { Content = "OK", Width = 60, IsDefault = true });
            Add(new Button { Content = "Off", Width = 60, IsDefault = true, IsEnabled = false });
            Add(new Button { Content = "Two\nlines", Width = 70, Height = 36 });
            Add(new Button { Content = "Wide button text", Width = 150, Height = 24 });
            Add(new Button { Content = "Tall", Width = 50, Height = 40 });
            Add(new CheckBox { Content = "Three", IsThreeState = true, IsChecked = null });
            Add(new CheckBox { Content = "Off", IsEnabled = false });
            Add(new RadioButton { Content = "Off", IsEnabled = false, IsChecked = true });
            Add(new CheckBox { Content = "Multi\nline", IsChecked = true, Height = 36 });
            foreach (var theme in new[] { "System7SpeechCheckBoxTheme" })
                foreach (var (on, enabled) in new[] { (false, true), (true, true), (true, false), (false, false) })
                {
                    var c = new CheckBox { Content = "Speech", IsChecked = on, IsEnabled = enabled };
                    c[!StyledElement.ThemeProperty] = new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(theme);
                    Add(c);
                }
            foreach (var (on, enabled) in new[] { (false, true), (true, true), (true, false), (false, false) })
            {
                var r = new RadioButton { Content = "Speech", IsChecked = on, IsEnabled = enabled, GroupName = $"s{on}{enabled}" };
                r[!StyledElement.ThemeProperty] = new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("System7SpeechRadioButtonTheme");
                Add(r);
            }
            foreach (var key in new[] { "System7MediaPlayButtonTheme", "System7MediaStopButtonTheme", "System7MediaPauseButtonTheme", "System7MediaRecordButtonTheme" })
                foreach (var enabled in new[] { true, false })
                {
                    var b = new Button { IsEnabled = enabled };
                    b[!StyledElement.ThemeProperty] = new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(key);
                    Add(b);
                }
            foreach (var enabled in new[] { true, false })
            {
                var b = new Button { IsEnabled = enabled };
                b[!StyledElement.ThemeProperty] = new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("System7SpeechSpeakerButtonTheme");
                Add(b);
            }
            foreach (var value in new[] { 0.0, 3, 7 })
            {
                var p = new ProgressBar { Minimum = 0, Maximum = 7, Value = value };
                p[!StyledElement.ThemeProperty] = new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("System7SpeakerIndicatorTheme");
                Add(p);
            }
            Add(new ProgressBar { Value = 40, Width = 120, ShowProgressText = true });
            Add(new ProgressBar { Value = 70, Width = 120, IsEnabled = false });
            Add(new ProgressBar { Value = 70, Orientation = Avalonia.Layout.Orientation.Vertical, Height = 60, IsEnabled = false });
            foreach (var (value, enabled, size) in new[] { (0.0, true, 120.0), (50, true, 120), (100, false, 120), (50, true, 40), (50, true, 20) })
            {
                Add(new ScrollBar { Orientation = Avalonia.Layout.Orientation.Horizontal, Width = size, Maximum = 100, Value = value, ViewportSize = 10, IsEnabled = enabled });
                Add(new ScrollBar { Orientation = Avalonia.Layout.Orientation.Vertical, Height = size, Maximum = 100, Value = value, ViewportSize = 10, IsEnabled = enabled });
            }
            Add(new ScrollBar { Orientation = Avalonia.Layout.Orientation.Horizontal, Width = 120, Maximum = 0, ViewportSize = 10 });
            var combo = new ComboBox { Width = 140, IsEnabled = false, ItemsSource = new[] { "Disabled" }, SelectedIndex = 0 };
            Add(combo);
            Add(new ComboBox { Width = 140, IsEditable = true, Text = "Editable", ItemsSource = new[] { "One", "Two" } });
            Add(new ComboBox { Width = 140, PlaceholderText = "Empty" });
            Add(new TextBox { Width = 120, Text = "Disabled", IsEnabled = false });
            Add(new TextBox { Width = 120, PlaceholderText = "Placeholder" });
            Add(new TextBox { Width = 120, Text = "Wrapped text that goes on and on", TextWrapping = Avalonia.Media.TextWrapping.Wrap, AcceptsReturn = true, Height = 50 });
            var styles = new StackPanel();
            foreach (System7.Avalonia.Rendering.System7TextStyle style in new[] { 1, 2, 4, 8, 16, 32, 64, 3, 24, 127 })
            {
                var item = new ListBoxItem { Content = $"Style {(int)style}" };
                styles.Children.Add(new TextBlock { Text = $"Plain {(int)style}" });
                System7Theme.SetTextStyle(item, style);
            }
            Add(styles);
            var desktop = new System7Desktop { Width = 600, Height = 260 };
            var x = 4.0;
            foreach (var kind in Enum.GetValues<System7WindowKind>())
                foreach (var active in new[] { true, false })
                {
                    var frame = new System7WindowFrame { Kind = kind, Title = active ? "Active" : "Idle", Width = 70, Height = 90,
                        CanClose = true, CanZoom = true, CanResize = true, IsActive = active, Content = new TextBlock { Text = "Hi" } };
                    Canvas.SetLeft(frame, x);
                    Canvas.SetTop(frame, active ? 6 : 120);
                    desktop.Children.Add(frame);
                    if (!active) x += 76;
                }
            foreach (var frame in desktop.Children.OfType<System7WindowFrame>()) frame.IsActive = frame.Title == "Active";
            Add(desktop);
            var window = new Window { Width = 920, Height = 900, Content = panel };
            window.Show();
            Settle(window);
            foreach (var frame in desktop.Children.OfType<System7WindowFrame>()) frame.IsActive = frame.Title == "Active";
            Snap(window, $"sheet-{(int)depth}");
            window.Close();
        }
    }

    public static int Compare(string golden, string actual, string diffs)
    {
        Directory.CreateDirectory(diffs);
        var failures = 0;
        var files = Directory.GetFiles(golden, "*.png").OrderBy(f => f).ToList();
        foreach (var expectedPath in files)
        {
            var name = Path.GetFileName(expectedPath);
            var actualPath = Path.Combine(actual, name);
            if (!File.Exists(actualPath)) { Console.WriteLine($"MISSING {name}"); failures++; continue; }
            var (ew, eh, e) = Read(expectedPath);
            var (aw, ah, a) = Read(actualPath);
            if (ew != aw || eh != ah) { Console.WriteLine($"SIZE {name} {ew}x{eh} vs {aw}x{ah}"); failures++; continue; }
            var diff = 0;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            var marked = new byte[e.Length];
            for (var i = 0; i < e.Length; i += 4)
            {
                var same = e[i] == a[i] && e[i + 1] == a[i + 1] && e[i + 2] == a[i + 2] && e[i + 3] == a[i + 3];
                var px = i / 4 % ew;
                var py = i / 4 / ew;
                if (same)
                {
                    var gray = (byte)(160 + (e[i] + e[i + 1] + e[i + 2]) / 12);
                    marked[i] = marked[i + 1] = marked[i + 2] = gray;
                }
                else
                {
                    diff++;
                    marked[i] = 0; marked[i + 1] = 0; marked[i + 2] = 255;
                    minX = Math.Min(minX, px); minY = Math.Min(minY, py); maxX = Math.Max(maxX, px); maxY = Math.Max(maxY, py);
                }
                marked[i + 3] = 255;
            }
            if (diff == 0) continue;
            failures++;
            Console.WriteLine($"DIFF {name} {diff}px in ({minX},{minY})-({maxX},{maxY})");
            Write(Path.Combine(diffs, name), ew, eh, marked);
        }
        foreach (var extra in Directory.GetFiles(actual, "*.png").Select(Path.GetFileName).Except(files.Select(Path.GetFileName)))
            Console.WriteLine($"EXTRA {extra}");
        Console.WriteLine($"{files.Count - failures}/{files.Count} identical");
        return failures == 0 ? 0 : 1;
    }

    private static (int, int, byte[]) Read(string path)
    {
        using var bitmap = new Bitmap(path);
        var width = bitmap.PixelSize.Width;
        var height = bitmap.PixelSize.Height;
        using var converted = new WriteableBitmap(bitmap.PixelSize, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        var bytes = new byte[width * height * 4];
        using (var target = converted.Lock())
        {
            bitmap.CopyPixels(new PixelRect(0, 0, width, height), target.Address, bytes.Length, target.RowBytes);
            Marshal.Copy(target.Address, bytes, 0, bytes.Length);
        }
        return (width, height, bytes);
    }

    private static void Write(string path, int width, int height, byte[] pixels)
    {
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            using var bitmap = new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Unpremul, handle.AddrOfPinnedObject(),
                new PixelSize(width, height), new Vector(96, 96), width * 4);
            bitmap.Save(path, new PngBitmapEncoderOptions());
        }
        finally { handle.Free(); }
    }
}
