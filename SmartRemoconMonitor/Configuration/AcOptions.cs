using System.ComponentModel.DataAnnotations;

namespace SmartRemoconMonitor.Configuration;

/// <summary>
/// エアコンまわりの設定
/// </summary>
public sealed class AcOptions : IValidatableObject
{
    /// <summary>
    /// 設定のセクション名
    /// </summary>
    public const string SectionName = "Ac";

    /// <summary>
    /// 入力として受け付ける目標温度の下限
    /// </summary>
    public const int MinAllowedTemperature = 16;

    /// <summary>
    /// 入力として受け付ける目標温度の上限
    /// </summary>
    public const int MaxAllowedTemperature = 30;

    /// <summary>
    /// 画面で選べる目標温度の下限の既定値
    /// </summary>
    private const int DefaultMinTargetTemperature = 18;

    /// <summary>
    /// 画面で選べる目標温度の上限の既定値
    /// </summary>
    private const int DefaultMaxTargetTemperature = MaxAllowedTemperature;

    /// <summary>
    /// 目標温度の初期値の既定値
    /// </summary>
    private const int DefaultTargetTemperatureValue = 25;

    /// <summary>
    /// 途絶と判定するまでの時間の下限（s）
    /// </summary>
    private const int MinSensorTimeoutSeconds = 5;

    /// <summary>
    /// 途絶と判定するまでの時間の上限（s）
    /// </summary>
    private const int MaxSensorTimeoutSeconds = 3600;

    /// <summary>
    /// 途絶と判定するまでの時間の既定値（s）
    /// </summary>
    private const int DefaultSensorTimeoutSeconds = 120;

    /// <summary>
    /// 画面で選べる目標温度の下限
    /// </summary>
    [Range(MinAllowedTemperature, MaxAllowedTemperature)]
    public int MinTargetTemperature { get; set; } = DefaultMinTargetTemperature;

    /// <summary>
    /// 画面で選べる目標温度の上限
    /// </summary>
    [Range(MinAllowedTemperature, MaxAllowedTemperature)]
    public int MaxTargetTemperature { get; set; } = DefaultMaxTargetTemperature;

    /// <summary>
    /// リモコン画面を開いたときの目標温度の初期値
    /// </summary>
    [Range(MinAllowedTemperature, MaxAllowedTemperature)]
    public int DefaultTargetTemperature { get; set; } = DefaultTargetTemperatureValue;

    /// <summary>
    /// 測定値がこの秒数届かなければ途絶と判定する
    /// </summary>
    [Range(MinSensorTimeoutSeconds, MaxSensorTimeoutSeconds)]
    public int SensorTimeoutSeconds { get; set; } = DefaultSensorTimeoutSeconds;

    /// <summary>
    /// <see cref="SensorTimeoutSeconds"/>を<see cref="TimeSpan"/>にしたもの
    /// </summary>
    public TimeSpan SensorTimeout => TimeSpan.FromSeconds(SensorTimeoutSeconds);

    /// <summary>
    /// 温度を目標温度の下限〜上限の範囲に収める
    /// </summary>
    public int Clamp(int temperature) => Math.Clamp(temperature, MinTargetTemperature, MaxTargetTemperature);

    /// <summary>
    /// 複数の項目にまたがる検証を行う。
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinTargetTemperature >= MaxTargetTemperature)
        {
            yield return new ValidationResult(
                $"{nameof(MinTargetTemperature)}は{nameof(MaxTargetTemperature)}より小さい必要があります。",
                new[] { nameof(MinTargetTemperature), nameof(MaxTargetTemperature) });
        }
    }
}
