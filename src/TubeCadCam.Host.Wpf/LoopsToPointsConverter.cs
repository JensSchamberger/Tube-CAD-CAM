using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TubeCadCam.Geometry;

namespace TubeCadCam.Host.Wpf;

public sealed class LoopToPointsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        new PointCollection(((IEnumerable<Vector2>)value).Select(v => new System.Windows.Point(v.X, v.Y)));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
