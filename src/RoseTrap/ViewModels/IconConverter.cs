using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace RoseTrap.ViewModels;

/// <summary>Maps a page name ("FFlags Settings") to its "Icon.FFlagsSettings" geometry resource.</summary>
public sealed class IconConverter : IValueConverter
{
    public static readonly IconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string name && Application.Current!.TryGetResource("Icon." + name.Replace(" ", ""), null, out var g)
            ? g as Geometry
            : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
