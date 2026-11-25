using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using RoseTrap.Controls;
using RoseTrap.ViewModels;
using RoseTrap.Views.Pages;

namespace RoseTrap.Views;

public partial class MainWindow : Window
{
    const double Collapsed = 58, Expanded = 190;

    static readonly (string Name, string A, string B)[] Accents =
    [
        ("Rose", "#d99aa9", "#f3adbd"), ("Crimson", "#d9727f", "#f2909b"), ("Lilac", "#b49ad9", "#cdb6f3"),
        ("Peach", "#e0a98a", "#f5c3a6"), ("Mint", "#8fc9b0", "#ace4cb"),
    ];

    static readonly Dictionary<string, string> Titles = new()
    {
        ["Welcome"] = "", ["FFlags"] = "Fast flags", ["FFlags Settings"] = "Flag settings", ["Preset"] = "Game presets",
    };

    readonly Dictionary<string, Func<Control>> _factories = new()
    {
        ["Welcome"] = () => new WelcomePage(),
        ["General"] = () => new GeneralPage(),
        ["Global"] = () => new GlobalPage(),
        ["FFlags"] = () => new FlagsPage(),
        ["FFlags Settings"] = () => new FlagSettingsPage(),
        ["Preset"] = () => new PresetPage(),
        ["Shaders"] = () => new ShadersPage(),
        ["Mods"] = () => new ModsPage(),
        ["Settings"] = () => new SettingsPage(),
        ["Appearance"] = () => new AppearancePage(),
    };
    readonly Dictionary<string, Control> _pages = new();
    readonly List<Button> _nav;
    readonly DispatcherTimer _expandDelay = new() { Interval = TimeSpan.FromMilliseconds(120) };
    readonly PageFade _fade = new();
    string _current = "";

    public MainWindow()
    {
        InitializeComponent();
        _nav = NavTop.Children.Concat(NavBottom.Children).OfType<Button>().ToList();

        foreach (var b in _nav) b.Click += (_, _) => Navigate((string)b.Tag!);
        BrandHome.Click += (_, _) => Navigate("Welcome");
        TitleBar.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.Source == TitleBar) BeginMoveDrag(e);
        };

        // Compact by default; resting on the rail opens it over the page.
        _expandDelay.Tick += (_, _) => { _expandDelay.Stop(); SetSidebar(true); };
        Sidebar.PointerEntered += (_, _) => { if (Vm?.CompactSidebar != false) _expandDelay.Start(); };
        Sidebar.PointerExited += (_, _) => { _expandDelay.Stop(); if (Vm?.CompactSidebar != false) SetSidebar(false); };

        KeyDown += OnKeyDown;
        SetSidebar(false);
        Navigate("Welcome", instant: true);
    }

    MainViewModel? Vm => DataContext as MainViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (Vm is not { } vm) return;
        vm.Toast += ShowToast;
        vm.CloseRequested += Close;
        vm.MinimizeRequested += () => WindowState = WindowState.Minimized;
        vm.AppearanceChanged += ApplyAppearance;
        ApplyAppearance();
    }

    public string CurrentPage => _current;
    internal IEnumerable<Control> LoadedPages => _pages.Values;

    public void Navigate(string name, bool instant = false)
    {
        if (name == _current || !_factories.ContainsKey(name)) return;
        _current = name;

        if (!_pages.TryGetValue(name, out var page))
        {
            _pages[name] = page = _factories[name]();
            page.IsVisible = false;
            PageHost.Children.Add(page);
        }

        // Pages never overlap: the old one goes at once, the new one rises in.
        foreach (var p in _pages.Values) if (p != page) p.IsVisible = false;
        page.IsVisible = true;
        if (page.FindLogicalDescendantOfType<ScrollViewer>() is { } sv) sv.Offset = default;
        (_pages.GetValueOrDefault("Welcome") as WelcomePage)?.SetActive(name == "Welcome");

        if (instant || Vm?.Animations == false) _fade.Finish(page);
        else _fade.Run(page, this);

        foreach (var b in _nav) b.Classes.Set("active", (string)b.Tag! == name);
        Crumb.Text = Titles.GetValueOrDefault(name, name);
        PlaceIndicator();
    }

    void PlaceIndicator()
    {
        var active = _nav.FirstOrDefault(b => b.Classes.Contains("active"));
        if (active is null) { NavIndicator.Opacity = 0; return; }
        Dispatcher.UIThread.Post(() =>
        {
            if (active.TranslatePoint(default, NavPanel) is not { } p) return;
            NavIndicator.Opacity = 1;
            NavIndicator.RenderTransform = TransformOperations.Parse($"translateY({p.Y:0.##}px)");
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Opens the rail as if hovered (screenshots).</summary>
    public void ShowSidebarExpanded() => SetSidebar(true);

    void SetSidebar(bool expanded)
    {
        Sidebar.Width = expanded ? Expanded : Collapsed;
        Sidebar.BoxShadow = BoxShadows.Parse(expanded && Vm?.CompactSidebar != false ? "10 0 28 -10 #B0000000" : "0 0 0 0 #00000000");
        foreach (var label in Sidebar.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("navlabel")))
            label.Opacity = expanded ? 1 : 0;
    }

    void ApplyAppearance()
    {
        if (Vm is not { } vm) return;
        bool pinned = !vm.CompactSidebar;
        PageHost.Margin = new Thickness(pinned ? Expanded : Collapsed, 0, 0, 0);
        SetSidebar(pinned);

        var (_, a, b) = Accents[Math.Clamp(vm.AccentIndex, 0, Accents.Length - 1)];
        SetAccent(Color.Parse(a), Color.Parse(b));
    }

    static void SetAccent(Color a, Color b)
    {
        var r = Application.Current!.Resources;
        r["Accent"] = new SolidColorBrush(a);
        r["AccentBright"] = new SolidColorBrush(b);
        r["AccentSoft"] = new SolidColorBrush(Color.FromArgb(0x1A, a.R, a.G, a.B));
        r["AccentLine"] = new SolidColorBrush(Color.FromArgb(0x38, a.R, a.G, a.B));
        r["AccentTrack"] = new SolidColorBrush(Color.FromArgb(0x61, a.R, a.G, a.B));
        r["AccentGradientH"] = new SolidColorBrush(a);
        r["SystemAccentColor"] = a;
    }

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Ctrl+Up/Down moves between tabs from anywhere.
        if (e.KeyModifiers != KeyModifiers.Control || e.Key is not (Key.Up or Key.Down)) return;
        var order = _nav.Select(b => (string)b.Tag!).Prepend("Welcome").ToList();
        int i = order.IndexOf(_current), n = order.Count;
        Navigate(order[(i + (e.Key == Key.Down ? 1 : -1) + n) % n]);
        e.Handled = true;
    }

    public void ShowToast(string text)
    {
        var toast = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F2262427")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3a3539")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 9),
            MaxWidth = 440,
            BoxShadow = BoxShadows.Parse("0 10 24 0 #70000000"),
            Child = new TextBlock { Text = text, FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#ece4e8")), TextWrapping = TextWrapping.Wrap },
        };
        Toasts.Children.Add(toast);
        _ = AnimateToast(toast);
    }

    async Task AnimateToast(Border toast)
    {
        var easing = new SplineEasing(.16, 1, .3, 1);
        toast.Opacity = 0;
        toast.RenderTransform = TransformOperations.Parse("translateY(8px)");
        toast.Transitions =
        [
            new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(200), Easing = easing },
            new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(260), Easing = easing },
        ];
        await Task.Delay(16);
        toast.Opacity = 1;
        toast.RenderTransform = TransformOperations.Parse("translateY(0px)");
        await Task.Delay(3200);
        toast.Opacity = 0;
        await Task.Delay(220);
        Toasts.Children.Remove(toast);
    }
}
