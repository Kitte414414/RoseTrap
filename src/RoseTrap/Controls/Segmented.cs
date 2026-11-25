using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace RoseTrap.Controls;

/// <summary>A pill of options with a thumb that slides to the selected one.</summary>
public sealed class Segmented : Border
{
    public static readonly StyledProperty<string> OptionsProperty =
        AvaloniaProperty.Register<Segmented, string>(nameof(Options), "");

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<Segmented, int>(nameof(SelectedIndex), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Comma-separated labels, e.g. "Auto,Vulkan,OpenGL".</summary>
    public string Options { get => GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
    readonly Border _thumb = new()
    {
        Height = 26, CornerRadius = new CornerRadius(6), HorizontalAlignment = HorizontalAlignment.Left,
        Background = new SolidColorBrush(Color.Parse("#3a2f34")),
        BorderBrush = new SolidColorBrush(Color.Parse("#57414a")), BorderThickness = new Thickness(1),
        BoxShadow = BoxShadows.Parse("0 2 8 0 #60000000, inset 0 1 0 0 #10ffffff"),
    };
    bool _placed;

    public Segmented()
    {
        CornerRadius = new CornerRadius(9);
        BorderThickness = new Thickness(1);
        Padding = new Thickness(3);
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Center;
        Child = new Panel { Children = { _thumb, _buttons } };
        _buttons.LayoutUpdated += (_, _) => PlaceThumb();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OptionsProperty) Rebuild();
        else if (change.Property == SelectedIndexProperty) { SyncChecked(); PlaceThumb(); }
    }

    void Rebuild()
    {
        _buttons.Children.Clear();
        var labels = Options.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            var b = new ToggleButton { Content = labels[i] };
            b.Click += (_, _) => { SelectedIndex = index; SyncChecked(); };
            _buttons.Children.Add(b);
        }
        SyncChecked();
    }

    void SyncChecked()
    {
        for (int i = 0; i < _buttons.Children.Count; i++)
            ((ToggleButton)_buttons.Children[i]).IsChecked = i == SelectedIndex;
    }

    void PlaceThumb()
    {
        if (SelectedIndex < 0 || SelectedIndex >= _buttons.Children.Count) { _thumb.Opacity = 0; return; }
        var b = _buttons.Children[SelectedIndex].Bounds;
        if (b.Width <= 0) return;
        _thumb.Opacity = 1;
        if (Math.Abs(_thumb.Width - b.Width) < .5 && _placed && _thumb.Tag is double x && Math.Abs(x - b.X) < .5) return;

        _thumb.Width = b.Width;
        _thumb.Tag = b.X;
        _thumb.RenderTransform = TransformOperations.Parse($"translateX({b.X:0.##}px)");
        if (!_placed)
        {
            _placed = true; // first placement is instant; later moves slide
            _thumb.Transitions =
            [
                new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(380), Easing = new SplineEasing(.34, 1.36, .64, 1) },
                new DoubleTransition { Property = WidthProperty, Duration = TimeSpan.FromMilliseconds(300), Easing = new SplineEasing(.16, 1, .3, 1) },
            ];
        }
    }
}
