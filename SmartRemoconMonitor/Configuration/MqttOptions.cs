using System.ComponentModel.DataAnnotations;

namespace SmartRemoconMonitor.Configuration;

/// <summary>
/// MQTT設定
/// </summary>
public sealed class MqttOptions : IValidatableObject
{
    /// <summary>
    /// 設定のセクション名
    /// </summary>
    public const string SectionName = "Mqtt";

    /// <summary>
    /// ブローカのホストの既定値
    /// </summary>
    private const string DefaultHost = "192.168.0.10";

    /// <summary>
    /// ポート番号の下限
    /// </summary>
    private const int MinPort = 1;

    /// <summary>
    /// ポート番号の上限
    /// </summary>
    private const int MaxPort = 65535;

    /// <summary>
    /// MQTTの標準ポート番号
    /// </summary>
    private const int DefaultMqttPort = 1883;

    /// <summary>
    /// キープアライブの間隔の下限（s）
    /// </summary>
    private const int MinKeepAliveSeconds = 5;

    /// <summary>
    /// キープアライブの間隔の上限（s）
    /// </summary>
    private const int MaxKeepAliveSeconds = 300;

    /// <summary>
    /// キープアライブの間隔の既定値（s）
    /// </summary>
    private const int DefaultKeepAliveSeconds = 30;

    /// <summary>
    /// 待つ時間の上限の下限（s）
    /// </summary>
    private const int MinTimeoutSeconds = 1;

    /// <summary>
    /// 待つ時間の上限の上限（s）
    /// </summary>
    private const int MaxTimeoutSeconds = 60;

    /// <summary>
    /// 接続を待つ時間の上限の既定値（s）
    /// </summary>
    private const int DefaultConnectTimeoutSeconds = 10;

    /// <summary>
    /// 送信を待つ時間の上限の既定値（s）
    /// </summary>
    private const int DefaultOperationTimeoutSeconds = 5;

    /// <summary>
    /// 再接続の待ち時間の下限（s）
    /// </summary>
    private const int MinReconnectDelaySeconds = 1;

    /// <summary>
    /// 再接続の待ち時間の初期値として設定できる上限（s）
    /// </summary>
    private const int MaxReconnectInitialDelaySeconds = 600;

    /// <summary>
    /// 再接続の待ち時間の上限として設定できる上限（s）
    /// </summary>
    private const int MaxReconnectMaxDelaySeconds = 3600;

    /// <summary>
    /// 再接続の待ち時間の初期値の既定値（s）
    /// </summary>
    private const int DefaultReconnectInitialDelaySeconds = 2;

    /// <summary>
    /// 再接続の待ち時間の上限の既定値（s）
    /// </summary>
    private const int DefaultReconnectMaxDelaySeconds = 60;

    /// <summary>
    /// ブローカのホスト名またはIPアドレス
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Host { get; set; } = DefaultHost;

    /// <summary>
    /// ブローカのポート番号
    /// </summary>
    [Range(MinPort, MaxPort)]
    public int Port { get; set; } = DefaultMqttPort;

    /// <summary>
    /// クライアントID
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string ClientId { get; set; } = AppPaths.ProductName;

    /// <summary>
    /// キープアライブの間隔（s）
    /// </summary>
    [Range(MinKeepAliveSeconds, MaxKeepAliveSeconds)]
    public int KeepAliveSeconds { get; set; } = DefaultKeepAliveSeconds;

    /// <summary>
    /// 接続を待つ時間の上限（s）
    /// </summary>
    [Range(MinTimeoutSeconds, MaxTimeoutSeconds)]
    public int ConnectTimeoutSeconds { get; set; } = DefaultConnectTimeoutSeconds;

    /// <summary>
    /// 送信を待つ時間の上限（s）
    /// </summary>
    [Range(MinTimeoutSeconds, MaxTimeoutSeconds)]
    public int OperationTimeoutSeconds { get; set; } = DefaultOperationTimeoutSeconds;

    /// <summary>
    /// 再接続の待ち時間の初期値（s）
    /// </summary>
    [Range(MinReconnectDelaySeconds, MaxReconnectInitialDelaySeconds)]
    public int ReconnectInitialDelaySeconds { get; set; } = DefaultReconnectInitialDelaySeconds;

    /// <summary>
    /// 再接続の待ち時間の上限（s）
    /// </summary>
    [Range(MinReconnectDelaySeconds, MaxReconnectMaxDelaySeconds)]
    public int ReconnectMaxDelaySeconds { get; set; } = DefaultReconnectMaxDelaySeconds;

    /// <summary>
    /// <see cref="KeepAliveSeconds"/>を<see cref="TimeSpan"/>にしたもの
    /// </summary>
    public TimeSpan KeepAlive => TimeSpan.FromSeconds(KeepAliveSeconds);

    /// <summary>
    /// <see cref="ConnectTimeoutSeconds"/>を<see cref="TimeSpan"/>にしたもの
    /// </summary>
    public TimeSpan ConnectTimeout => TimeSpan.FromSeconds(ConnectTimeoutSeconds);

    /// <summary>
    /// <see cref="OperationTimeoutSeconds"/>を<see cref="TimeSpan"/>にしたもの
    /// </summary>
    public TimeSpan OperationTimeout => TimeSpan.FromSeconds(OperationTimeoutSeconds);

    /// <summary>
    /// <see cref="ReconnectInitialDelaySeconds"/>を<see cref="TimeSpan"/>にしたもの
    /// </summary>
    public TimeSpan ReconnectInitialDelay => TimeSpan.FromSeconds(ReconnectInitialDelaySeconds);

    /// <summary>
    /// <see cref="ReconnectMaxDelaySeconds"/>を<see cref="TimeSpan"/>にしたもの
    /// </summary>
    public TimeSpan ReconnectMaxDelay => TimeSpan.FromSeconds(ReconnectMaxDelaySeconds);

    /// <summary>
    /// 複数の項目にまたがる検証を行う。
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ReconnectMaxDelaySeconds < ReconnectInitialDelaySeconds)
        {
            yield return new ValidationResult(
                $"{nameof(ReconnectMaxDelaySeconds)}は{nameof(ReconnectInitialDelaySeconds)}以上である必要があります。",
                new[] { nameof(ReconnectMaxDelaySeconds) });
        }
    }
}
