using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SmartRemoconMonitor.Models;

namespace SmartRemoconMonitor.Converters;

/// <summary>
/// 表示用の一方向の変換器の基底クラス。逆変換には対応しない。
/// </summary>
public abstract class OneWayValueConverter : IValueConverter
{
    /// <summary>
    /// 値を表示用に変換する。
    /// </summary>
    public abstract object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture);

    /// <summary>
    /// 逆変換には対応しない。
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// falseのときに表示し、trueのときに隠す。
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class InverseBooleanToVisibilityConverter : OneWayValueConverter
{
    /// <summary>
    /// trueならCollapsed、それ以外ならVisibleを返す。
    /// </summary>
    public override object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;
}

/// <summary>
/// nullまたは空の文字列なら隠し、それ以外なら表示する。
/// </summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class NullToVisibilityConverter : OneWayValueConverter
{
    /// <summary>
    /// nullまたは空白だけの文字列ならCollapsed、それ以外ならVisibleを返す。
    /// </summary>
    public override object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            null => Visibility.Collapsed,
            string s when string.IsNullOrWhiteSpace(s) => Visibility.Collapsed,
            _ => Visibility.Visible,
        };
}

/// <summary>
/// 状態ランプの段階を色に変換する。
/// </summary>
[ValueConversion(typeof(StatusLevel), typeof(Brush))]
public sealed class StatusLevelToBrushConverter : OneWayValueConverter
{
    /// <summary>
    /// 正常のときの色。
    /// </summary>
    public Brush? Ok { get; set; }

    /// <summary>
    /// 注意のときの色。
    /// </summary>
    public Brush? Warning { get; set; }

    /// <summary>
    /// 異常のときの色。
    /// </summary>
    public Brush? Error { get; set; }

    /// <summary>
    /// 段階に対応する色を返す。
    /// </summary>
    public override object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            StatusLevel.Ok => Ok,
            StatusLevel.Warning => Warning,
            _ => Error,
        };
}
