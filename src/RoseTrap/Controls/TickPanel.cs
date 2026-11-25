using Avalonia;
using Avalonia.Controls;

namespace RoseTrap.Controls;

/// <summary>Centres each label under a slider stop (the thumb's travel is inset by half its width).</summary>
public sealed class TickPanel : Panel
{
    public static readonly StyledProperty<double> InsetProperty =
        AvaloniaProperty.Register<TickPanel, double>(nameof(Inset), 11);

    public double Inset { get => GetValue(InsetProperty); set => SetValue(InsetProperty, value); }

    protected override Size MeasureOverride(Size available)
    {
        double h = 0;
        foreach (var c in Children) { c.Measure(Size.Infinity); h = Math.Max(h, c.DesiredSize.Height); }
        return new Size(double.IsInfinity(available.Width) ? 0 : available.Width, h);
    }

    protected override Size ArrangeOverride(Size final)
    {
        int n = Children.Count;
        double span = final.Width - 2 * Inset;
        for (int i = 0; i < n; i++)
        {
            var c = Children[i];
            double x = Inset + (n == 1 ? span / 2 : span * i / (n - 1)) - c.DesiredSize.Width / 2;
            c.Arrange(new Rect(x, 0, c.DesiredSize.Width, final.Height));
        }
        return final;
    }
}
