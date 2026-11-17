using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TubeCadCam.Geometry;
using TubeCadCam.Presentation;

namespace TubeCadCam.Host.Wpf;

/// <summary>Turns the normalised section loops into one geometry filled with the even-odd rule (hollow sections stay hollow).</summary>
public sealed class LoopsToGeometryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var geometry = new StreamGeometry { FillRule = FillRule.EvenOdd };
        if (value is IEnumerable<IReadOnlyList<Vector2>> loops)
        {
            using StreamGeometryContext ctx = geometry.Open();
            foreach (IReadOnlyList<Vector2> loop in loops)
            {
                if (loop.Count < 3)
                {
                    continue;
                }

                ctx.BeginFigure(new Point(loop[0].X, loop[0].Y), isFilled: true, isClosed: true);
                ctx.PolyLineTo(loop.Skip(1).Select(v => new Point(v.X, v.Y)).ToList(), isStroked: true, isSmoothJoin: false);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class FeatureKindLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is FeatureKind k ? FeatureDescriptors.Label(k) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class ProfileKindLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ProfileKind k ? ProfileKindLabels.Label(k) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
