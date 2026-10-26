namespace SmartRemoconMonitor.Services.Mqtt;

/// <summary>
/// MQTTのトピック名
/// </summary>
public static class MqttTopics
{
    /// <summary>
    /// 温度・湿度の測定値（マイコン→PC）
    /// </summary>
    public const string Sensor = "ac/sensor";

    /// <summary>
    /// デバイスが最後に送った赤外線の内容（マイコン→PC）
    /// </summary>
    public const string State = "ac/state";

    /// <summary>
    /// 運転操作（PC→ マイコン）
    /// </summary>
    public const string Command = "ac/state/set";

    /// <summary>
    /// デバイスの生死（マイコン →PC）。
    /// </summary>
    public const string Status = "ac/status";
}

/// <summary>
/// ac/statusのペイロード
/// </summary>
public static class MqttStatus
{
    /// <summary>
    /// デバイスが接続している
    /// </summary>
    public const string Online = "online";

    /// <summary>
    /// デバイスが切断した（LWT）
    /// </summary>
    public const string Offline = "offline";
}

/// <summary>
/// 運転モード
/// </summary>
public enum OperationMode
{
    /// <summary>
    /// 冷房
    /// </summary>
    Cooling,

    /// <summary>
    /// 暖房
    /// </summary>
    Heating,
}

/// <summary>
/// 風量
/// </summary>
public enum FanSpeed
{
    /// <summary>
    /// 自動
    /// </summary>
    Auto,

    /// <summary>
    /// 1段階目
    /// </summary>
    Level1,

    /// <summary>
    /// 2段階目
    /// </summary>
    Level2,

    /// <summary>
    /// 3段階目
    /// </summary>
    Level3,

    /// <summary>
    /// 4段階目
    /// </summary>
    Level4,
}

/// <summary>
/// 風向き
/// </summary>
public enum AirflowDirection
{
    /// <summary>
    /// 自動
    /// </summary>
    Auto,

    /// <summary>
    /// 1段階目
    /// </summary>
    Level1,

    /// <summary>
    /// 2段階目
    /// </summary>
    Level2,

    /// <summary>
    /// 3段階目
    /// </summary>
    Level3,

    /// <summary>
    /// 4段階目
    /// </summary>
    Level4,

    /// <summary>
    /// 5段階目
    /// </summary>
    Level5,
}

/// <summary>
/// 受信した温度・湿度の測定値
/// </summary>
/// <param name="Temperature">温度</param>
/// <param name="Humidity">湿度</param>
/// <param name="ReceivedAt">受信した時刻</param>
public readonly record struct SensorReading(double Temperature, double Humidity, DateTimeOffset ReceivedAt);

/// <summary>
/// マイコンが報告した運転状態
/// </summary>
/// <param name="Power">true:運転 false:停止</param>
/// <param name="Mode">運転モード</param>
/// <param name="TargetTemperature">目標温度</param>
/// <param name="FanSpeed">風量</param>
/// <param name="AirflowDirection">風向き</param>
/// <param name="ReceivedAt">受信した時刻</param>
public readonly record struct AcState(
    bool Power,
    OperationMode Mode,
    int TargetTemperature,
    FanSpeed FanSpeed,
    AirflowDirection AirflowDirection,
    DateTimeOffset ReceivedAt);

/// <summary>
/// エアコンへ送信する運転操作
/// </summary>
/// <param name="Power">true:運転 false:停止</param>
/// <param name="Mode">運転モード</param>
/// <param name="TargetTemperature">目標温度</param>
/// <param name="FanSpeed">風量</param>
/// <param name="AirflowDirection">風向き</param>
public readonly record struct AcCommand(
    bool Power,
    OperationMode Mode,
    int TargetTemperature,
    FanSpeed FanSpeed,
    AirflowDirection AirflowDirection);
