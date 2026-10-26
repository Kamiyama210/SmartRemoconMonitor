using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SmartRemoconMonitor.Models;

namespace SmartRemoconMonitor.Converters;

/// <summary>
/// 0.0〜1.0に正規化した点の並びを、描画領域の大きさに合わせた折れ線の<see cref="Geometry"/>に変換する。
/// </summary>
public sealed class TrendPointsConverter : IMultiValueConverter
{
    /// <summary>
    /// 受け取る値の数（点の並び、幅、高さ）。
    /// </summary>
    private const int ExpectedValueCount = 3;

    /// <summary>
    /// 点の並び・幅・高さから、折れ線を作る。描けない場合は空の図形を返す。
    /// </summary>
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Length < ExpectedValueCount ||
            values[0] is not IReadOnlyList<TrendPoint> points ||
            values[1] is not double width ||
            values[2] is not double height)
        {
            return Geometry.Empty;
        }

        if (points.Count < 2 || !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
        {
            return Geometry.Empty;
        }

        var screen = new Point[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            screen[i] = new Point(points[i].X * width, (1 - points[i].Y) * height);
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(screen[0], isFilled: false, isClosed: false);
            context.PolyLineTo(screen[1..], isStroked: true, isSmoothJoin: false);
        }

        geometry.Freeze();
        return geometry;
    }

    /// <summary>
    /// 逆変換には対応しない。
    /// </summary>
    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
