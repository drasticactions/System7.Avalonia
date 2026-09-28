using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Platform;
using Avalonia.Threading;
using System7.Avalonia.Controls;
using System7.Avalonia;
using System7.Avalonia.Rendering;

namespace System7.Demo;

public sealed class DemoView : UserControl
{
    private readonly TextBlock status = new() { Text = "Ready.", TextWrapping = TextWrapping.Wrap };
    private readonly DemoColumns sections = new() { Margin = new Thickness(10) };
    private readonly LayoutTransformControl magnifier;
    private readonly System7WindowFrame memoryWindow;
    private int pixelScale = 2;

    /// <summary>Whether the windows stay in one column instead of filling the view in several.</summary>
    public bool SingleColumn
    {
        get => sections.MaxColumns == 1;
        set
        {
            sections.MaxColumns = value ? 1 : int.MaxValue;
            memoryWindow.Margin = value ? new Thickness(-10, 0, -10, 0) : default;
        }
    }

    public DemoView()
    {
        Focusable = true;
        this[!BackgroundProperty] = new DynamicResourceExtension("System7DesktopBackgroundBrush");
        var menu = new Menu();
        var apple = new MenuItem { Header = "\u0014" };
        var file = new MenuItem { Header = "File" };
        var about = new MenuItem { Header = "About System 7..." };
        about.Click += (_, _) => status.Text = "System 7.0.1 controls in Avalonia.";
        apple.Items.Add(about);
        var icons = new MenuItem { Header = "Icons" };
        var largeIcon = System7Icon.FromMonochrome(ReadAsset("MenuLarge"));
        var smallIcon = System7Icon.FromMonochrome(ReadAsset("MenuSmall"), 16);
        var colorIcon = System7Icon.FromColorIcon(ReadAsset("MenuColor"));
        var iconLabels = new[] { "Large", "Reduced", "Small", "Color", "Color reduced", "Color small", "Disabled", "Styled" };
        for (var i = 0; i < iconLabels.Length; i++)
        {
            var item = MenuCommand(iconLabels[i]);
            item.Icon = i == 2 ? smallIcon : i is >= 3 and <= 6 ? colorIcon : largeIcon;
            System7Theme.SetMenuIconKind(item, i is 1 or 4 ? System7MenuIconKind.Reduced
                : i is 2 or 5 ? System7MenuIconKind.Small : System7MenuIconKind.Normal);
            if (i is >= 3 and <= 6) System7Theme.SetMonochromeIcon(item, i == 5 ? smallIcon : largeIcon);
            if (i == 6) item.IsEnabled = false;
            if (i == 7)
            {
                System7Theme.SetTextStyle(item, (System7TextStyle)127);
                System7Theme.SetMenuMark(item, '\u0012');
            }
            icons.Items.Add(item);
        }
        apple.Items.Add(icons);

        foreach (var (label, key) in new[] { ("New", Key.N), ("Open...", Key.O), ("-", Key.None), ("Save", Key.S) })
        {
            var item = new MenuItem { Header = label };
            if (key != Key.None) item.InputGesture = item.HotKey = new KeyGesture(key, KeyModifiers.Meta);
            item.Click += (_, _) => status.Text = $"{label} selected.";
            file.Items.Add(item);
        }
        file.Items.Add(new MenuItem { Header = "Disabled", IsEnabled = false });
        file.Items.Add(new MenuItem { Header = "Checked", IsChecked = true, ToggleType = MenuItemToggleType.CheckBox });
        var edit = new MenuItem { Header = "Edit" };
        edit.Items.Add(new MenuItem { Header = "Undo", IsEnabled = false });
        edit.Items.Add(new MenuItem { Header = "-" });
        foreach (var (label, key) in new[] { ("Cut", Key.X), ("Copy", Key.C), ("Paste", Key.V) })
        {
            var item = new MenuItem { Header = label, InputGesture = new KeyGesture(key, KeyModifiers.Meta) };
            item.Click += (_, _) => status.Text = $"{label} selected.";
            edit.Items.Add(item);
        }
        var submenus = new MenuItem { Header = "Submenus" };
        submenus.Items.Add(MenuCommand("Leaf"));
        var nested = new MenuItem { Header = "Submenu" };
        nested.Items.Add(MenuCommand("Child"));
        var deep = new MenuItem { Header = "Deep" };
        deep.Items.Add(MenuCommand("First"));
        deep.Items.Add(MenuCommand("Checked", true));
        deep.Items.Add(MenuCommand("Last"));
        nested.Items.Add(deep);
        nested.Items.Add(new MenuItem { Header = "Disabled", IsEnabled = false });
        nested.Items.Add(MenuCommand("Last"));
        submenus.Items.Add(nested);
        var disabledSubmenu = new MenuItem { Header = "Disabled", IsEnabled = false };
        disabledSubmenu.Items.Add(MenuCommand("Unavailable"));
        submenus.Items.Add(disabledSubmenu);
        submenus.Items.Add(MenuCommand("Last"));
        var scrolling = new MenuItem { Header = "Scroll menu" };
        for (var i = 1; i <= 40; i++) scrolling.Items.Add(MenuCommand($"Item {i:00}"));
        edit.Items.Add(new MenuItem { Header = "-" });
        edit.Items.Add(submenus);
        edit.Items.Add(scrolling);
        var view = new MenuItem { Header = "View" };
        foreach (var scale in new[] { 1, 2, 3 })
        {
            var item = new MenuItem { Header = $"{scale}x pixels" };
            item.Click += (_, _) => SetScale(scale);
            view.Items.Add(item);
        }
        view.Items.Add(new MenuItem { Header = "-" });
        foreach (var style in new[] { System7TextStyle.Plain, System7TextStyle.Bold, System7TextStyle.Italic,
            System7TextStyle.Underline, System7TextStyle.Outline, System7TextStyle.Shadow,
            System7TextStyle.Condensed, System7TextStyle.Extended, (System7TextStyle)127 })
        {
            var label = style == (System7TextStyle)127 ? "All styles" : style.ToString();
            var item = new MenuItem { Header = label };
            System7Theme.SetTextStyle(item, style);
            item.Click += (_, _) => status.Text = $"{label} selected.";
            view.Items.Add(item);
        }
        menu.Items.Add(apple);
        menu.Items.Add(file);
        menu.Items.Add(edit);
        menu.Items.Add(view);

        MenuItem MenuCommand(string label, bool check = false)
        {
            var item = new MenuItem { Header = label, IsChecked = check };
            item.Click += (_, _) => status.Text = $"{label} selected.";
            return item;
        }
        sections.Children.Add(Window("System 7", new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = "The Macintosh,\none pixel at a time.", TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = "Try the controls below.", TextWrapping = TextWrapping.Wrap }
            }
        }));
        sections.Children.Add(Window("Menus", MenuColors(menu)));
        var button = new Button { Content = "Button", Width = 100, Height = 20, HorizontalAlignment = HorizontalAlignment.Left };
        var clicks = 0;
        button.Click += (_, _) => status.Text = $"Button clicked {++clicks} times.";
        var checks = new StackPanel { Spacing = 6 };
        checks.Children.Add(button);
        checks.Children.Add(new Button { Content = "Disabled", Width = 100, Height = 20, IsEnabled = false });
        foreach (var (text, selected, enabled) in new[] { ("Check box", false, true), ("Checked", true, true), ("Disabled", true, false) })
        {
            var check = new CheckBox { Content = text, IsChecked = selected, IsEnabled = enabled };
            check.IsCheckedChanged += (_, _) => status.Text = $"{text}: {(check.IsChecked == true ? "on" : "off")}.";
            checks.Children.Add(check);
        }
        checks.Children.Add(new RadioButton { Content = "Radio button", GroupName = "sample", IsChecked = true });
        checks.Children.Add(new RadioButton { Content = "Another choice", GroupName = "sample" });
        checks.Children.Add(new RadioButton { Content = "Disabled", GroupName = "sample", IsEnabled = false });
        sections.Children.Add(Window("Controls", checks));
        sections.Children.Add(Window("Color controls", ColorControls()));
        sections.Children.Add(Window("Color scrollbars", ColorScrollBars()));
        sections.Children.Add(Window("Windows", WindowPlayground()));
        sections.Children.Add(Window("Rounded windows", RoundedWindows()));
        sections.Children.Add(Window("Text", new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = "Name:" },
                new TextBox { Text = "Macintosh", MinWidth = 100 },
                new TextBox { Text = "Select and edit this text.", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 54 },
            }
        }));
        var list = new ListBox
        {
            ItemsSource = new[] { "System Folder", "Applications", "Documents", "Utilities", "Fonts", "Preferences", "Control Panels", "Extensions", "Desk Accessories", "Scrapbook", "TeachText", "Trash" },
            SelectedIndex = 0, Height = 82, SelectionMode = SelectionMode.Multiple
        };
        list.SelectionChanged += (_, _) => status.Text = list.SelectedItems is { Count: > 0 } selected
            ? string.Join(", ", selected.Cast<string>()) : "No list selection.";
        var fonts = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var label in new[] { "Chicago", "Geneva", "Monaco", "Disabled", "-", "A very long font name" })
            fonts.Items.Add(new ComboBoxItem { Content = label, IsEnabled = label != "Disabled" });
        fonts.SelectedIndex = 0;
        System7Theme.SetPopupTitle(fonts, "Font:");
        System7Theme.SetPopupTitleWidth(fonts, 50);
        fonts.SelectionChanged += (_, _) => status.Text = $"Pop-up choice: {(fonts.SelectedItem as ComboBoxItem)?.Content}.";
        sections.Children.Add(Window("Pop-up menus", fonts));
        sections.Children.Add(Window("Lists", ListControls(list)));
        sections.Children.Add(Window("Icon lists", IconListControls()));
        sections.Children.Add(Window("Small icon lists", SmallIconListControls()));
        sections.Children.Add(Window("Compact controls", CompactControls()));
        memoryWindow = Window("Memory slider", MemorySliderControls());
        memoryWindow.MinWidth = 160;
        ((Control)memoryWindow.Content!).Margin = new Thickness(6);
        sections.Children.Add(memoryWindow);
        sections.Children.Add(Window("Sound slider", SoundSliderControls()));
        sections.Children.Add(Window("Speech speaker", SpeechSpeakerControls()));
        sections.Children.Add(Window("Speech options", SpeechOptionControls()));
        sections.Children.Add(Window("Speech rate", SpeechSliderControls()));
        sections.Children.Add(Window("Network slider", NetworkSliderControls()));
        sections.Children.Add(Window("Dialog frames", DialogFrames()));
        sections.Children.Add(Window("Picture buttons", PictureButtons()));
        sections.Children.Add(Window("Media controls", MediaControls()));
        var progress = new ProgressBar { Value = 35 };
        var verticalProgress = new ProgressBar { Value = 35, Orientation = Orientation.Vertical, Height = 80,
            HorizontalAlignment = HorizontalAlignment.Left };
        var progressDisabled = new CheckBox { Content = "Disabled" };
        progressDisabled.IsCheckedChanged += (_, _) => progress.IsEnabled = verticalProgress.IsEnabled = progressDisabled.IsChecked != true;
        var advanceProgress = new Button { Content = "Advance", Width = 100, Height = 20, HorizontalAlignment = HorizontalAlignment.Left };
        advanceProgress.Click += (_, _) =>
        {
            progress.Value = verticalProgress.Value = progress.Value >= 100 ? 0 : Math.Min(100, progress.Value + 10);
            status.Text = $"Progress: {progress.Value:0}%.";
        };
        sections.Children.Add(Window("Progress", new StackPanel { Spacing = 6,
            Children = { progress, verticalProgress, progressDisabled, advanceProgress } }));
        sections.Children.Add(Window("Status", status));
        sections.Children.Add(Window("Menu tracking", TrackingControls()));
        var page = new DockPanel();
        page[!Panel.BackgroundProperty] = new DynamicResourceExtension("System7DesktopBackgroundBrush");
        DockPanel.SetDock(menu, Dock.Top);
        page.Children.Add(menu);
        var scroll = new ScrollViewer
        {
            Content = sections,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        page.Children.Add(scroll);
        scroll.PropertyChanged += (_, e) =>
        {
            if (e.Property == BoundsProperty) sections.ViewportHeight = scroll.Bounds.Height - sections.Margin.Top - sections.Margin.Bottom;
        };
        Loaded += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                Focus();
                scroll.Offset = default;
            }, DispatcherPriority.Background);
        };
        magnifier = new LayoutTransformControl { Child = page, LayoutTransform = new ScaleTransform(2, 2) };
        Content = magnifier;
    }

    private static Control MenuColors(Menu menu)
    {
        var depth = new ComboBox { ItemsSource = new[] { "Monochrome", "4 grays", "16 colors", "256 colors", "Thousands", "Millions" }, SelectedIndex = 0 };
        AutomationProperties.SetAutomationId(depth, "menu-color-depth");
        var custom = new CheckBox { Content = "Custom colors", IsEnabled = false };
        AutomationProperties.SetAutomationId(custom, "menu-custom-colors");
        var depths = new[] { System7ColorDepth.Monochrome, System7ColorDepth.Indexed2, System7ColorDepth.Indexed4,
            System7ColorDepth.Indexed8, System7ColorDepth.Rgb555, System7ColorDepth.Rgb888 };
        depth.SelectionChanged += (_, _) =>
        {
            System7Theme.SetColorDepth(menu, depths[depth.SelectedIndex]);
            custom.IsEnabled = depth.SelectedIndex != 0;
            if (!custom.IsEnabled) custom.IsChecked = false;
        };
        custom.IsCheckedChanged += (_, _) =>
        {
            foreach (var (key, rgb) in new[] { ("System7MenuTitleForegroundBrush", 0x332266), ("System7MenuBarBackgroundBrush", 0xeeddff),
                ("System7MenuBackgroundBrush", 0xffeedd), ("System7MenuItemForegroundBrush", 0x003366),
                ("System7MenuMarkBrush", 0x003366), ("System7MenuShortcutBrush", 0x003366) })
            {
                if (custom.IsChecked == true) menu.Resources[key] = new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
                else menu.Resources.Remove(key);
            }
        };
        return new StackPanel { Spacing = 6, Children = { depth, custom, new TextBlock { Text = "Try the menus above.", TextWrapping = TextWrapping.Wrap } } };
    }

    private static Control ListControls(ListBox list)
    {
        AutomationProperties.SetAutomationId(list, "color-list");
        var depth = new ComboBox { ItemsSource = new[] { "Monochrome", "4 grays", "16 colors", "256 colors", "Thousands", "Millions" }, SelectedIndex = 0 };
        var highlight = new ComboBox { ItemsSource = new[] { "Black", "Blue", "Green", "Pink", "White", "Gray" }, SelectedIndex = 0, IsEnabled = false };
        var palette = new ComboBox { ItemsSource = new[] { "Standard", "Cream", "Dark" }, SelectedIndex = 0, IsEnabled = false };
        var inactive = new CheckBox { Content = "Inactive" };
        AutomationProperties.SetAutomationId(depth, "list-color-depth");
        AutomationProperties.SetAutomationId(highlight, "list-highlight");
        AutomationProperties.SetAutomationId(palette, "list-palette");
        AutomationProperties.SetAutomationId(inactive, "list-inactive");
        System7Theme.SetPopupTitle(highlight, "Select:");
        System7Theme.SetPopupTitleWidth(highlight, 40);
        System7Theme.SetPopupTitle(palette, "Paper:");
        System7Theme.SetPopupTitleWidth(palette, 40);
        var modes = new[] { System7ColorDepth.Monochrome, System7ColorDepth.Indexed2, System7ColorDepth.Indexed4,
            System7ColorDepth.Indexed8, System7ColorDepth.Rgb555, System7ColorDepth.Rgb888 };
        uint[] colors = [0x000000, 0xccccff, 0xccffcc, 0xffcccc, 0xffffff, 0x888888];
        (uint Foreground, uint Background)[] papers = [(0x000000, 0xffffff), (0x112244, 0xffeecc), (0xcc1122, 0x112244)];
        depth.SelectionChanged += (_, _) =>
        {
            if (depth.SelectedIndex < 0) return;
            System7Theme.SetColorDepth(list, modes[depth.SelectedIndex]);
            highlight.IsEnabled = palette.IsEnabled = depth.SelectedIndex > 0;
            if (depth.SelectedIndex == 0) highlight.SelectedIndex = palette.SelectedIndex = 0;
        };
        highlight.SelectionChanged += (_, _) =>
        {
            if (highlight.SelectedIndex >= 0) System7Theme.SetSelectionBrush(list, Brush(colors[highlight.SelectedIndex]));
        };
        palette.SelectionChanged += (_, _) =>
        {
            if (palette.SelectedIndex < 0) return;
            var pair = papers[palette.SelectedIndex];
            list.Foreground = list.BorderBrush = Brush(pair.Foreground);
            list.Background = Brush(pair.Background);
        };
        inactive.IsCheckedChanged += (_, _) => System7Theme.SetIsActive(list, inactive.IsChecked != true);
        return new StackPanel { Spacing = 6, Children = { depth, highlight, palette, inactive, list,
            new ScrollBar { Orientation = Orientation.Horizontal, Minimum = 0, Maximum = 100, Value = 35, ViewportSize = 20 } } };

        static IBrush Brush(uint rgb) => new SolidColorBrush(Color.FromUInt32(0xff000000 | rgb));
    }

    private Control IconListControls()
    {
        var monochrome = ReadAsset("ListIcon");
        var color4 = ReadAsset("ListIcon4");
        var color8 = ReadAsset("ListIcon8");
        var list = new ListBox { Height = 130, SelectionMode = SelectionMode.Multiple };
        list[!ThemeProperty] = new DynamicResourceExtension("System7IconListBoxTheme");
        list.Resources["System7IconListItemWidth"] = 48d;
        string[] labels = ["Phone", "Colors"];
        for (var index = 0; index < labels.Length; index++)
        {
            var item = new ListBoxItem { Content = labels[index] };
            System7Theme.SetListIcon(item, System7IconFamily.FromResources(monochrome, index > 0 ? color4 : default, index > 0 ? color8 : default));
            list.Items.Add(item);
        }
        list.SelectedIndex = 0;
        list.SelectionChanged += (_, _) => status.Text = list.SelectedItem is ListBoxItem item ? $"Icon: {item.Content}." : "No icon selection.";
        var depth = new ComboBox { ItemsSource = new[] { "Monochrome", "4 grays", "16 colors", "256 colors", "Thousands", "Millions" }, SelectedIndex = 0 };
        var inactive = new CheckBox { Content = "Inactive" };
        var modes = new[] { System7ColorDepth.Monochrome, System7ColorDepth.Indexed2, System7ColorDepth.Indexed4,
            System7ColorDepth.Indexed8, System7ColorDepth.Rgb555, System7ColorDepth.Rgb888 };
        depth.SelectionChanged += (_, _) => { if (depth.SelectedIndex >= 0) System7Theme.SetColorDepth(list, modes[depth.SelectedIndex]); };
        inactive.IsCheckedChanged += (_, _) => System7Theme.SetIsActive(list, inactive.IsChecked != true);
        AutomationProperties.SetAutomationId(list, "icon-list");
        AutomationProperties.SetAutomationId(depth, "icon-list-depth");
        AutomationProperties.SetAutomationId(inactive, "icon-list-inactive");
        return new StackPanel { Spacing = 6, Children = { depth, inactive, list } };
    }

    private Control SmallIconListControls()
    {
        var icons = ReadAsset("SmallListIcons");
        var labels = new[] { "Applications", "Utilities", "Network", "Controls", "Disabled", "Styled", "System Folder", "Color icons" };
        var list = new ListBox { Height = 182, SelectionMode = SelectionMode.Multiple };
        list[!ThemeProperty] = new DynamicResourceExtension("System7SmallIconListBoxTheme");
        for (var index = 0; index < labels.Length; index++)
        {
            var item = new ListBoxItem { Content = labels[index] };
            System7Theme.SetSmallListIcon(item, System7Icon.FromMonochrome(icons.AsSpan(index * 32, 32), 16));
            System7Theme.SetSmallListIconOnRight(item, (index & 1) != 0);
            if (index == 4) System7Theme.SetSmallListDisabled(item, true);
            if (index == 5) System7Theme.SetTextStyle(item, System7TextStyle.Bold);
            if (index == 2) System7Theme.SetSmallListIconBrush(item, new SolidColorBrush(Color.FromRgb(204, 204, 255)));
            if (index == 3) System7Theme.SetSmallListIconBrush(item, new SolidColorBrush(Color.FromRgb(255, 34, 34)));
            list.Items.Add(item);
        }
        list.SelectedIndex = 1;
        var depth = new ComboBox { ItemsSource = new[] { "Monochrome", "4 grays", "16 colors", "256 colors", "Thousands", "Millions" }, SelectedIndex = 0 };
        var modes = new[] { System7ColorDepth.Monochrome, System7ColorDepth.Indexed2, System7ColorDepth.Indexed4,
            System7ColorDepth.Indexed8, System7ColorDepth.Rgb555, System7ColorDepth.Rgb888 };
        depth.SelectionChanged += (_, _) => { if (depth.SelectedIndex >= 0) System7Theme.SetColorDepth(list, modes[depth.SelectedIndex]); };
        AutomationProperties.SetAutomationId(list, "small-icon-list");
        AutomationProperties.SetAutomationId(depth, "small-icon-list-depth");
        return new StackPanel { Spacing = 6, Children = { depth, list } };
    }

    private Control CompactControls()
    {
        var first = new RadioButton { Content = "12hr.", GroupName = "compact-hours" };
        var second = new RadioButton { Content = "24hr.", GroupName = "compact-hours", IsChecked = true };
        var sound = new CheckBox { Content = "Use sound", IsChecked = true };
        first[!ThemeProperty] = new DynamicResourceExtension("System7CompactRadioButtonTheme");
        second[!ThemeProperty] = new DynamicResourceExtension("System7CompactRadioButtonTheme");
        sound[!ThemeProperty] = new DynamicResourceExtension("System7CompactCheckBoxTheme");
        first.IsCheckedChanged += (_, _) => { if (first.IsChecked == true) status.Text = "12-hour clock selected."; };
        second.IsCheckedChanged += (_, _) => { if (second.IsChecked == true) status.Text = "24-hour clock selected."; };
        sound.IsCheckedChanged += (_, _) => status.Text = sound.IsChecked == true ? "Sound enabled." : "Sound disabled.";
        AutomationProperties.SetAutomationId(first, "compact-radio-12");
        AutomationProperties.SetAutomationId(second, "compact-radio-24");
        AutomationProperties.SetAutomationId(sound, "compact-checkbox-sound");
        return new StackPanel { Spacing = 5, Children = { first, second, sound } };
    }

    private Control MemorySliderControls()
    {
        return SliderReadout(new System7MemorySlider { Value = 64, Margin = new Thickness(1, 0, 0, 0) }, "memory-slider",
            value => $"{value:0} MB", value => $"Memory: {value:0} MB.");
    }

    private Control SoundSliderControls()
    {
        var readout = SliderReadout(new System7SoundSlider(), "sound-slider", value => $"Level {value:0}", value => $"Sound level: {value:0}.");
        readout.Orientation = Orientation.Horizontal;
        readout.Spacing = 8;
        readout.Children[1].VerticalAlignment = VerticalAlignment.Center;
        return readout;
    }

    private Control NetworkSliderControls()
    {
        return SliderReadout(new System7NetworkSlider(), "network-slider", value => $"Value {value:0}", value => $"Network setting: {value:0}.");
    }

    private Control SpeechSliderControls()
    {
        return SliderReadout(new System7SpeechSlider { Value = 148 }, "speech-rate-slider", value => $"Rate {value:0}", value => $"Speech rate: {value:0}.");
    }

    private Control ColorControls()
    {
        var custom = new CheckBox { Content = "Custom" };
        var disabled = new CheckBox { Content = "Disabled" };
        var button = new Button { Content = "Button", Width = 100, Height = 20, HorizontalAlignment = HorizontalAlignment.Left };
        var check = new CheckBox { Content = "Check box", IsChecked = true };
        var first = new RadioButton { Content = "First", GroupName = "colors", IsChecked = true };
        var second = new RadioButton { Content = "Second", GroupName = "colors" };
        var examples = new StackPanel { Spacing = 6, Children = { button, check, first, second } };
        var depth = ColorDepthChooser(examples, custom);
        custom.IsCheckedChanged += (_, _) =>
        {
            foreach (var control in examples.Children.OfType<Button>())
            {
                if (custom.IsChecked == true)
                {
                    control.Foreground = new SolidColorBrush(Color.FromRgb(153, 0, 0));
                    control.Background = new SolidColorBrush(Color.FromRgb(255, 204, 153));
                    control.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 0, 153));
                }
                else
                {
                    control.ClearValue(TemplatedControl.ForegroundProperty);
                    control.ClearValue(TemplatedControl.BackgroundProperty);
                    control.ClearValue(TemplatedControl.BorderBrushProperty);
                }
            }
        };
        disabled.IsCheckedChanged += (_, _) => examples.IsEnabled = disabled.IsChecked != true;
        button.Click += (_, _) => status.Text = "Color button clicked.";
        return new StackPanel { Spacing = 6, Children = { depth, custom, disabled, examples } };
    }

    private Control TrackingControls()
    {
        var modes = new ComboBox { ItemsSource = new[] { "Avalonia", "Avalonia with flash", "System 7" }, HorizontalAlignment = HorizontalAlignment.Stretch };
        System7Theme.SetPopupTitle(modes, "Menus:");
        System7Theme.SetPopupTitleWidth(modes, 56);
        void Apply()
        {
            var mode = modes.SelectedIndex;
            System7Theme.SetUseNativeMenuTracking(this, mode == 2);
            System7Theme.SetUseNativePopupTracking(this, mode == 2);
            System7Theme.SetUseMenuFlash(this, mode == 1);
            System7Theme.SetUsePopupFlash(this, mode == 1);
        }
        // Switch after the choice has closed the pop-up that made it.
        modes.SelectionChanged += (_, _) => Dispatcher.UIThread.Post(Apply);
        modes.SelectedIndex = 1;
        Apply();
        return new StackPanel { Spacing = 6, Children =
        {
            modes,
            new TextBlock { Text = "Avalonia menus and pop-ups, with the chosen item's flash, or System 7 tracking.", TextWrapping = TextWrapping.Wrap },
        } };
    }

    private Control ColorScrollBars()
    {
        var horizontal = new ScrollBar { Orientation = Orientation.Horizontal, Minimum = 0, Maximum = 100, Value = 50 };
        var vertical = new ScrollBar { Orientation = Orientation.Vertical, Minimum = 0, Maximum = 100, Value = 50 };
        var value = new TextBlock { Text = "50, 50", Margin = new Thickness(6), VerticalAlignment = VerticalAlignment.Center };
        var examples = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16"), RowDefinitions = new RowDefinitions("80,16") };
        examples.Children.Add(value);
        Grid.SetColumn(vertical, 1);
        Grid.SetRow(horizontal, 1);
        examples.Children.Add(vertical);
        examples.Children.Add(horizontal);
        var bars = new[] { horizontal, vertical };
        foreach (var bar in bars)
            bar.ValueChanged += (_, _) => value.Text = $"{horizontal.Value:0}, {vertical.Value:0}";
        var custom = new CheckBox { Content = "Custom" };
        var disabled = new CheckBox { Content = "Disabled" };
        var depth = ColorDepthChooser(examples, custom);
        custom.IsCheckedChanged += (_, _) =>
        {
            foreach (var bar in bars)
            {
                if (custom.IsChecked == true)
                {
                    bar.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 0, 153));
                    bar.Background = new SolidColorBrush(Color.FromRgb(255, 204, 153));
                }
                else
                {
                    bar.ClearValue(TemplatedControl.BorderBrushProperty);
                    bar.ClearValue(TemplatedControl.BackgroundProperty);
                }
            }
        };
        disabled.IsCheckedChanged += (_, _) => examples.IsEnabled = disabled.IsChecked != true;
        return new StackPanel { Spacing = 6, Children = { depth, custom, disabled, examples } };
    }

    private Control PictureButtons()
    {
        using var source = AssetLoader.Open(new Uri("avares://System7.Demo/Assets/NewFolder.png"));
        var picture = new Bitmap(source);
        var rounded = new Button { Content = new Image { Source = picture }, Width = 80, Height = 20,
            HorizontalAlignment = HorizontalAlignment.Left };
        rounded[!ThemeProperty] = new DynamicResourceExtension("System7PictureButtonTheme");
        AutomationProperties.SetName(rounded, "New folder, rounded button");
        var square = new Button { Content = new Image { Source = picture }, Width = 80, Height = 20,
            HorizontalAlignment = HorizontalAlignment.Left };
        square[!ThemeProperty] = new DynamicResourceExtension("System7SquarePictureButtonTheme");
        AutomationProperties.SetName(square, "New folder, square button");
        rounded.Click += (_, _) => status.Text = "Rounded picture button clicked.";
        square.Click += (_, _) => status.Text = "Square picture button clicked.";
        var disabled = new CheckBox { Content = "Disabled" };
        disabled.IsCheckedChanged += (_, _) => rounded.IsEnabled = square.IsEnabled = disabled.IsChecked != true;
        return new StackPanel { Spacing = 6, Children = { rounded, square, disabled } };
    }

    private Control MediaControls()
    {
        var buttons = new WrapPanel();
        foreach (var name in new[] { "Play", "Pause", "Stop", "Record" })
        {
            var button = new Button { Margin = new Thickness(0, 0, 4, 4) };
            button[!ThemeProperty] = new DynamicResourceExtension($"System7Media{name}ButtonTheme");
            AutomationProperties.SetName(button, name);
            button.Click += (_, _) => status.Text = $"{name} clicked.";
            buttons.Children.Add(button);
        }
        var speaker = new ProgressBar { Value = 50 };
        speaker[!ThemeProperty] = new DynamicResourceExtension("System7SpeakerIndicatorTheme");
        AutomationProperties.SetName(speaker, "Speaker level");
        var lower = new Button { Content = "-", Width = 29, Height = 20 };
        var higher = new Button { Content = "+", Width = 29, Height = 20 };
        AutomationProperties.SetName(lower, "Lower speaker level");
        AutomationProperties.SetName(higher, "Raise speaker level");
        lower.Click += (_, _) => speaker.Value = Math.Max(0, speaker.Value - 17);
        higher.Click += (_, _) => speaker.Value = Math.Min(99, speaker.Value + 17);
        var level = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { lower, speaker, higher } };
        var disabled = new CheckBox { Content = "Disabled" };
        disabled.IsCheckedChanged += (_, _) => buttons.IsEnabled = speaker.IsEnabled = disabled.IsChecked != true;
        return new StackPanel { Spacing = 6, Children = { buttons, level, disabled } };
    }

    private Control SpeechSpeakerControls()
    {
        var first = new Button();
        first[!ThemeProperty] = new DynamicResourceExtension("System7SpeechSpeakerButtonTheme");
        AutomationProperties.SetName(first, "Speak first voice");
        first.Click += (_, _) => status.Text = "First speech voice selected.";
        var second = new Button();
        second[!ThemeProperty] = new DynamicResourceExtension("System7SpeechSpeakerButtonTheme");
        AutomationProperties.SetName(second, "Speak second voice");
        second.Click += (_, _) => status.Text = "Second speech voice selected.";
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { first, second } };
        var disabled = new CheckBox { Content = "Disabled" };
        disabled.IsCheckedChanged += (_, _) => buttons.IsEnabled = disabled.IsChecked != true;
        return new StackPanel { Spacing = 6, Children = { buttons, disabled } };
    }

    private Control SpeechOptionControls()
    {
        var feedback = new CheckBox { Content = "Speak text", Width = 120 };
        feedback[!ThemeProperty] = new DynamicResourceExtension("System7SpeechCheckBoxTheme");
        AutomationProperties.SetName(feedback, "Speak text feedback");
        var listening = new RadioButton { Content = "Toggle listening", Width = 140 };
        listening[!ThemeProperty] = new DynamicResourceExtension("System7SpeechRadioButtonTheme");
        AutomationProperties.SetName(listening, "Key toggles listening");
        var options = new StackPanel { Spacing = 4, Children = { feedback, listening } };
        var disabled = new CheckBox { Content = "Disabled" };
        disabled.IsCheckedChanged += (_, _) => options.IsEnabled = disabled.IsChecked != true;
        return new StackPanel { Spacing = 6, Children = { options, disabled } };
    }

    private Control DialogFrames()
    {
        var desktop = new System7Desktop { Height = 132, MinWidth = 100 };
        var other = new System7WindowFrame { Kind = System7WindowKind.PlainDialog, Width = 84, Height = 46,
            Content = new TextBlock { Text = "Other", Margin = new Thickness(6) } };
        Canvas.SetLeft(other, 16);
        Canvas.SetTop(other, 66);
        var dialog = new System7WindowFrame { Title = "Dialog", Kind = System7WindowKind.Dialog, Width = 100, Height = 86,
            Content = new TextBlock { Text = "Click to\nactivate.", Margin = new Thickness(4) } };
        Canvas.SetTop(dialog, 4);
        desktop.Children.Add(other);
        desktop.Children.Add(dialog);
        var kinds = new[] { System7WindowKind.Dialog, System7WindowKind.PlainDialog, System7WindowKind.ShadowDialog, System7WindowKind.MovableDialog };
        var choices = new ComboBox { ItemsSource = new[] { "Dialog", "Plain", "Shadow", "Movable" }, SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        var zoom = new CheckBox { Content = "Zoom box", IsEnabled = false };
        zoom.IsCheckedChanged += (_, _) => dialog.CanZoom = zoom.IsChecked == true;
        choices.SelectionChanged += (_, _) =>
        {
            if (choices.SelectedIndex < 0) return;
            dialog.Kind = kinds[choices.SelectedIndex];
            zoom.IsEnabled = dialog.Kind == System7WindowKind.MovableDialog;
            Canvas.SetLeft(dialog, 0);
            Canvas.SetTop(dialog, 4);
            dialog.Width = 100;
            dialog.Height = 86;
            desktop.Activate(dialog);
            status.Text = $"Frame: {choices.SelectedItem}.";
        };
        return new StackPanel { Spacing = 6, Children = { choices, zoom, WindowColorSwitch(desktop), desktop } };
    }

    private static Control WindowPlayground()
    {
        var desktop = new System7Desktop { Height = 166, MinWidth = 80 };
        var other = new System7WindowFrame
        {
            Title = "Other", Width = 84, Height = 90, CanClose = true, CanResize = true,
            Content = new TextBlock { Text = "Click to activate.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6) }
        };
        var document = new System7WindowFrame
        {
            Title = "Window", Width = 96, Height = 100, CanClose = true, CanZoom = true, CanResize = true,
            Content = new TextBlock { Text = "Drag.\nZoom.\nResize.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6) }
        };
        Canvas.SetLeft(other, 14);
        Canvas.SetTop(other, 48);
        Canvas.SetLeft(document, 4);
        Canvas.SetTop(document, 8);
        System7Desktop.SetResizeMinimum(other, new Size(72, 40));
        System7Desktop.SetResizeMinimum(document, new Size(72, 40));
        desktop.Children.Add(other);
        desktop.Children.Add(document);
        var reset = new Button { Content = "Reset", Width = 80, Height = 20, HorizontalAlignment = HorizontalAlignment.Left };
        reset.Click += (_, _) =>
        {
            other.IsVisible = document.IsVisible = true;
            Canvas.SetLeft(other, 14);
            Canvas.SetTop(other, 48);
            Canvas.SetLeft(document, 4);
            Canvas.SetTop(document, 8);
            other.Width = 84;
            other.Height = 90;
            document.Width = 96;
            document.Height = 100;
            desktop.Activate(document);
        };
        return new StackPanel { Spacing = 6, Children = { WindowColorSwitch(desktop), desktop, reset } };
    }

    private static byte[] ReadAsset(string name)
    {
        using var stream = AssetLoader.Open(new Uri($"avares://System7.Demo/Assets/{name}.bin"));
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private StackPanel SliderReadout(Slider slider, string automationId, Func<double, string> readout, Func<double, string> report)
    {
        var value = new TextBlock { Text = readout(slider.Value) };
        slider.PropertyChanged += (_, change) =>
        {
            if (change.Property != RangeBase.ValueProperty) return;
            value.Text = readout(slider.Value);
            status.Text = report(slider.Value);
        };
        AutomationProperties.SetAutomationId(slider, automationId);
        return new StackPanel { Spacing = 6, Children = { slider, value } };
    }

    private static ComboBox ColorDepthChooser(Control examples, CheckBox custom)
    {
        var modes = new[] { System7ColorDepth.Indexed8, System7ColorDepth.Rgb555, System7ColorDepth.Rgb888,
            System7ColorDepth.Indexed2, System7ColorDepth.Indexed4, System7ColorDepth.Monochrome };
        var depth = new ComboBox { ItemsSource = new[] { "256 colors", "Thousands", "Millions", "4 grays", "16 colors", "Monochrome" },
            SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        System7Theme.SetColorDepth(examples, modes[0]);
        depth.SelectionChanged += (_, _) =>
        {
            if (depth.SelectedIndex < 0) return;
            System7Theme.SetColorDepth(examples, modes[depth.SelectedIndex]);
            custom.IsEnabled = modes[depth.SelectedIndex] != System7ColorDepth.Monochrome;
            if (!custom.IsEnabled) custom.IsChecked = false;
        };
        return depth;
    }

    private static CheckBox WindowColorSwitch(System7Desktop desktop)
    {
        var color = new CheckBox { Content = "Color" };
        desktop[!Panel.BackgroundProperty] = new DynamicResourceExtension("System7DesktopBackgroundBrush");
        color.IsCheckedChanged += (_, _) =>
        {
            System7Theme.SetColorDepth(desktop, color.IsChecked == true ? System7ColorDepth.Indexed8 : System7ColorDepth.Monochrome);
            desktop[!Panel.BackgroundProperty] = new DynamicResourceExtension(color.IsChecked == true
                ? "System7ColorDesktopBackgroundBrush" : "System7DesktopBackgroundBrush");
        };
        return color;
    }

    private Control RoundedWindows()
    {
        var desktop = new System7Desktop { Height = 132, MinWidth = 100 };
        var other = new System7WindowFrame { Kind = System7WindowKind.PlainDialog, Width = 80, Height = 46,
            Content = new TextBlock { Text = "Other", Margin = new Thickness(6) } };
        Canvas.SetLeft(other, 18);
        Canvas.SetTop(other, 70);
        var rounded = new System7WindowFrame { Title = "Rounded", Width = 96, Height = 80, CanClose = true,
            Content = new TextBlock { Text = "Drag this\nwindow.", Margin = new Thickness(6) } };
        rounded[!ThemeProperty] = new DynamicResourceExtension("System7RoundedWindowTheme");
        Canvas.SetLeft(rounded, 4);
        Canvas.SetTop(rounded, 8);
        desktop.Children.Add(other);
        desktop.Children.Add(rounded);
        var choices = new ComboBox { ItemsSource = new[] { "16 px", "4 px", "6 px", "8 px", "10 px", "12 px", "20 px", "24 px" },
            SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(choices, "Window corner diameter");
        var diameters = new[] { 16, 4, 6, 8, 10, 12, 20, 24 };
        choices.SelectionChanged += (_, _) =>
        {
            if (choices.SelectedIndex >= 0) rounded.CornerDiameter = diameters[choices.SelectedIndex];
        };
        var color = WindowColorSwitch(desktop);
        var reset = new Button { Content = "Reset", Width = 80, Height = 20, HorizontalAlignment = HorizontalAlignment.Left };
        reset.Click += (_, _) =>
        {
            rounded.IsVisible = true;
            Canvas.SetLeft(rounded, 4);
            Canvas.SetTop(rounded, 8);
            desktop.Activate(rounded);
        };
        rounded.CloseRequested += (_, _) => status.Text = "Rounded window closed.";
        return new StackPanel { Spacing = 6, Children = { choices, color, desktop, reset } };
    }

    protected override Size MeasureOverride(Size availableSize) => base.MeasureOverride(NativePixelSize(availableSize));

    protected override Size ArrangeOverride(Size finalSize)
    {
        base.ArrangeOverride(NativePixelSize(finalSize));
        return finalSize;
    }

    private Size NativePixelSize(Size size) => new(Math.Floor(size.Width / pixelScale) * pixelScale,
        Math.Floor(size.Height / pixelScale) * pixelScale);

    private void SetScale(int scale)
    {
        pixelScale = scale;
        magnifier.LayoutTransform = new ScaleTransform(scale, scale);
        InvalidateMeasure();
    }

    private static System7WindowFrame Window(string title, Control content)
    {
        content.Margin = new Thickness(10);
        return new System7WindowFrame { Title = title, Content = content, HorizontalContentAlignment = HorizontalAlignment.Stretch };
    }
}
