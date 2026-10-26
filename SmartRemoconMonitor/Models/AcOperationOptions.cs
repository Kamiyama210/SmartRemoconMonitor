using SmartRemoconMonitor.Services.Mqtt;

namespace SmartRemoconMonitor.Models;

/// <summary>
/// エアコン操作の選択肢1つ。列挙値・通信用の文字列・画面の表示名の対応。
/// </summary>
/// <param name="Value">列挙値</param>
/// <param name="Wire">MQTTのペイロードで使う文字列</param>
/// <param name="Label">画面に表示する名前</param>
public sealed record AcOption<T>(T Value, string Wire, string Label);

/// <summary>
/// エアコン操作の選択肢の対応表、変換
/// </summary>
public static class AcOperationOptions
{
    /// <summary>
    /// 運転モードの選択肢
    /// </summary>
    public static IReadOnlyList<AcOption<OperationMode>> Modes { get; } =
    [
        new(OperationMode.Cooling, "cooling", "冷房"),
        new(OperationMode.Heating, "heating", "暖房"),
    ];

    /// <summary>
    /// 風量の選択肢
    /// </summary>
    public static IReadOnlyList<AcOption<FanSpeed>> FanSpeeds { get; } =
    [
        new(FanSpeed.Auto, "auto", "自動"),
        new(FanSpeed.Level1, "1", "1"),
        new(FanSpeed.Level2, "2", "2"),
        new(FanSpeed.Level3, "3", "3"),
        new(FanSpeed.Level4, "4", "4"),
    ];

    /// <summary>
    /// 風向きの選択肢
    /// </summary>
    public static IReadOnlyList<AcOption<AirflowDirection>> AirflowDirections { get; } =
    [
        new(AirflowDirection.Auto, "auto", "自動"),
        new(AirflowDirection.Level1, "1", "1"),
        new(AirflowDirection.Level2, "2", "2"),
        new(AirflowDirection.Level3, "3", "3"),
        new(AirflowDirection.Level4, "4", "4"),
        new(AirflowDirection.Level5, "5", "5"),
    ];

    /// <summary>
    /// 運転モードを通信用の文字列にする。
    /// </summary>
    public static string ToWire(OperationMode value) => WireOf(Modes, value);

    /// <summary>
    /// 風量を通信用の文字列にする。
    /// </summary>
    public static string ToWire(FanSpeed value) => WireOf(FanSpeeds, value);

    /// <summary>
    /// 風向きを通信用の文字列にする。
    /// </summary>
    public static string ToWire(AirflowDirection value) => WireOf(AirflowDirections, value);

    /// <summary>
    /// 通信用の文字列を運転モードにする。対応する値が無ければfalse。
    /// </summary>
    public static bool TryFromWire(string? wire, out OperationMode value) => TryValueOf(Modes, wire, out value);

    /// <summary>
    /// 通信用の文字列を風量にする。対応する値が無ければfalse。
    /// </summary>
    public static bool TryFromWire(string? wire, out FanSpeed value) => TryValueOf(FanSpeeds, wire, out value);

    /// <summary>
    /// 通信用の文字列を風向きにする。対応する値が無ければfalse。
    /// </summary>
    public static bool TryFromWire(string? wire, out AirflowDirection value) => TryValueOf(AirflowDirections, wire, out value);

    /// <summary>
    /// 列挙値に対応する表示名を返す。
    /// </summary>
    public static string LabelOf<T>(IReadOnlyList<AcOption<T>> options, T value)
        where T : notnull
        => Find(options, value)?.Label ?? value.ToString() ?? string.Empty;

    /// <summary>
    /// 列挙値に対応する通信用の文字列を返す。見つからなければ<see cref="ArgumentOutOfRangeException"/>を投げる。
    /// </summary>
    private static string WireOf<T>(IReadOnlyList<AcOption<T>> options, T value)
        where T : notnull
        => Find(options, value)?.Wire ?? throw new ArgumentOutOfRangeException(nameof(value), value, "未定義の値です。");

    /// <summary>
    /// 通信用の文字列に対応する列挙値を探す。見つからなければfalse。
    /// </summary>
    private static bool TryValueOf<T>(IReadOnlyList<AcOption<T>> options, string? wire, out T value)
        where T : struct
    {
        var found = options.FirstOrDefault(option => option.Wire == wire);
        value = found?.Value ?? default;
        return found is not null;
    }

    /// <summary>
    /// 列挙値に対応する選択肢を探す。見つからなければnull。
    /// </summary>
    private static AcOption<T>? Find<T>(IReadOnlyList<AcOption<T>> options, T value)
        where T : notnull
        => options.FirstOrDefault(option => option.Value.Equals(value));
}
