using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace RoseTrap.Views;

/// <summary>The panel RoseTrap opens with: Play starts Roblox, Settings opens the settings window.</summary>
public partial class StartWindow : Window
{
    public event Action? PlayRequested;
    public event Action? SettingsRequested;

    public StartWindow()
    {
        InitializeComponent();
        PlayButton.Click += (_, _) => PlayRequested?.Invoke();
        SettingsButton.Click += (_, _) => SettingsRequested?.Invoke();
        CloseButton.Click += (_, _) => Close();
        Card.PointerPressed += (_, e) =>
        {
            // Dragging on the rose spins it instead of moving the window.
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.Source is not Controls.LogoRose) BeginMoveDrag(e);
        };
        PlayButton.PointerEntered += (_, _) => GlowRose(true);
        PlayButton.PointerExited += (_, _) => GlowRose(false);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
            else if (e.Key == Key.Enter) PlayRequested?.Invoke();
        };
        Opened += (_, _) =>
        {
            Card.Opacity = 1;
            Card.RenderTransform = TransformOperations.Identity;
            PlayButton.Focus();
        };
    }

    void GlowRose(bool on)
    {
        RoseTile.BorderBrush = on && this.TryFindResource("AccentLine", ActualThemeVariant, out var b) && b is IBrush brush
            ? brush : new SolidColorBrush(Color.Parse("#232225"));
        RoseTile.BoxShadow = on ? BoxShadows.Parse("0 0 24 0 #30d99aa9") : default;
    }

}
