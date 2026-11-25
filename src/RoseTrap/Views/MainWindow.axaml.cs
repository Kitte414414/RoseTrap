using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using Avalonia.Threading;
using RoseTrap.Controls;
using RoseTrap.ViewModels;
using RoseTrap.Views.Pages;

namespace RoseTrap.Views;

public partial class MainWindow : Window
{
    const double Collapsed = 58, Expanded = 178;

    static readonly (string Name, string A, string B)[] Accents =
    [
        ("Rose", "#d99aa9", "#f3adbd"), ("Crimson", "#d9727f", "#f2909b"), ("Lilac", "#b49ad9", "#cdb6f3"),
        ("Peach", "#e0a98a", "#f5c3a6"), ("Mint", "#8fc9b0", "#ace4cb"),
    ];

    readonly Dictionary<string, Func<Control>> _factories = new()
    {
        ["Welcome"] = () => new WelcomePage(),
        ["General"] = () => new GeneralPage(),
        ["Global"] = () => new GlobalPage(),
        ["FFlags"] = () => new FlagsPage(),
        ["FFlags Settings"] = () => new FlagSettingsPage(),
        ["Preset"] = () => new PresetPage(),
        ["Shaders"] = () => new ShadersPage(),
        ["Settings"] = () => new SettingsPage(),
        ["Appearance"] = () => new AppearancePage(),
    };
    readonly Dictionary<string, Control> _pages = new();
    readonly List<Button> _nav;
    readonly List<string> _order;
    readonly DispatcherTimer _expandDelay = new() { Interval = TimeSpan.FromMilliseconds(90) };
    string _current = "";

    public MainWindow()
    {
        InitializeComponent();
        _nav = NavTop.Children.Concat(NavBottom.Children).OfType<Button>().ToList();
        _order = ["Welcome", .. _nav.Select(b => (string)b.Tag!)];

        foreach (var b in _nav) b.Click += (_, _) => Navigate((string)b.Tag!);
        BrandHome.Click += (_, _) => Navigate("Welcome");
        TitleBar.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.Source == TitleBar) BeginMoveDrag(e);
        };

        // Compact by default; hovering opens the sidebar over the page.
        _expandDelay.Tick += (_, _) => { _expandDelay.Stop(); SetSidebar(true); };
        Sidebar.PointerEntered += (_, _) => { if (Vm?.CompactSidebar != false) _expandDelay.Start(); };
        Sidebar.PointerExited += (_, _) => { _expandDelay.Stop(); if (Vm?.CompactSidebar != false) SetSidebar(false); };
        Sidebar.PropertyChanged += (_, e) => { if (e.Property == BoundsProperty) PlaceIndicator(); };

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
        vm.AppearanceChanged += ApplyAppearance;
        ApplyAppearance();
    }

    public string CurrentPage => _current;

    public void Navigate(string name, bool instant = false)
    {
        if (name == _current || !_factories.ContainsKey(name)) return;
        bool forward = _order.IndexOf(name) >= _order.IndexOf(_current);
        _current = name;

        if (!_pages.TryGetValue(name, out var page))
            _pages[name] = page = _factories[name]();

        var transition = Pages.PageTransition;
        if (instant) Pages.PageTransition = null;
        Pages.IsTransitionReversed = !forward;
        Pages.Content = page;
        if (instant) Pages.PageTransition = transition;

        foreach (var b in _nav) b.Classes.Set("active", (string)b.Tag! == name);
        Crumb.Text = name.ToUpperInvariant();
        PlaceIndicator();
    }

    void PlaceIndicator()
    {
        var active = _nav.FirstOrDefault(b => b.Classes.Contains("active"));
        if (active is null) { NavIndicator.Opacity = 0; return; }
        Dispatcher.UIThread.Post(() =>
        {
            var p = active.TranslatePoint(default, NavPanel);
            if (p is null) return;
            NavIndicator.Opacity = 1;
            NavIndicator.RenderTransform = TransformOperations.Parse($"translateY({p.Value.Y:0.##}px)");
        }, DispatcherPriority.Loaded);
    }

    void SetSidebar(bool expanded)
    {
        Sidebar.Width = expanded ? Expanded : Collapsed;
        Sidebar.BoxShadow = BoxShadows.Parse(expanded && Vm?.CompactSidebar != false ? "12 0 32 -8 #A0000000" : "0 0 0 0 #00000000");
        foreach (var label in Sidebar.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("navlabel")))
            label.Opacity = expanded ? 1 : 0;
    }

    void ApplyAppearance()
    {
        if (Vm is not { } vm) return;
        StaggerTransition.Enabled = vm.Animations;

        bool pinned = !vm.CompactSidebar;
        ContentHost.Margin = new Thickness(pinned ? Expanded : Collapsed, 0, 0, 0);
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
        r["AccentGradient"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new(b, 0), new(a, 1) },
        };
        r["AccentGradientH"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = { new(a, 0), new(b, 1) },
        };
        r["SystemAccentColor"] = a;
    }

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Ctrl+Up/Down moves between tabs from anywhere.
        if (e.KeyModifiers != KeyModifiers.Control || e.Key is not (Key.Up or Key.Down)) return;
        int i = _order.IndexOf(_current), n = _order.Count;
        Navigate(_order[(i + (e.Key == Key.Down ? 1 : -1) + n) % n]);
        e.Handled = true;
    }

    public void ShowToast(string text)
    {
        var toast = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#EE2a2629")),
            BorderBrush = new SolidColorBrush(Color.Parse("#4a3d43")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(13, 9),
            MaxWidth = 420,
            BoxShadow = BoxShadows.Parse("0 12 30 0 #80000000"),
            Child = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 9,
                Children =
                {
                    new Avalonia.Controls.Shapes.Ellipse { Width = 7, Height = 7, Fill = (IBrush)Application.Current!.Resources["AccentBright"]!, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
                    new TextBlock { Text = text, FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#f0e6ea")), TextWrapping = TextWrapping.Wrap, MaxWidth = 380 },
                },
            },
        };
        Toasts.Children.Add(toast);
        _ = AnimateToast(toast);
    }

    async Task AnimateToast(Border toast)
    {
        var easing = new SplineEasing(.16, 1, .3, 1);
        toast.Opacity = 0;
        toast.RenderTransform = TransformOperations.Parse("translateY(10px)");
        toast.Transitions =
        [
            new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(260), Easing = easing },
            new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(320), Easing = easing },
        ];
        await Task.Delay(16);
        toast.Opacity = 1;
        toast.RenderTransform = TransformOperations.Parse("translateY(0px)");
        await Task.Delay(2800);
        toast.Opacity = 0;
        toast.RenderTransform = TransformOperations.Parse("translateY(6px)");
        await Task.Delay(260);
        Toasts.Children.Remove(toast);
    }
}
