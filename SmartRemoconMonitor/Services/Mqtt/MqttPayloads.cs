using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartRemoconMonitor.Services.Mqtt;

/// <summary>
/// ac/sensorのペイロード
/// </summary>
public sealed class SensorPayload
{
    /// <summary>
    /// 温度
    /// </summary>
    [JsonPropertyName("temp")]
    public double? Temp { get; set; }

    /// <summary>
    /// 湿度
    /// </summary>
    [JsonPropertyName("hum")]
    public double? Hum { get; set; }
}

/// <summary>
/// ac/state/setのペイロード
/// </summary>
public sealed class AcCommandPayload
{
    /// <summary>
    /// 運転するか
    /// </summary>
    [JsonPropertyName("power")]
    public bool Power { get; set; }

    /// <summary>
    /// 運転モードの通信用文字列
    /// </summary>
    [JsonPropertyName("mode")]
    public required string Mode { get; set; }

    /// <summary>
    /// 目標温度
    /// </summary>
    [JsonPropertyName("target")]
    public int Target { get; set; }

    /// <summary>
    /// 風量の通信用文字列
    /// </summary>
    [JsonPropertyName("fan")]
    public required string Fan { get; set; }

    /// <summary>
    /// 風向きの通信用文字列
    /// </summary>
    [JsonPropertyName("airflow")]
    public required string Airflow { get; set; }
}

/// <summary>
/// ac/stateのペイロード
/// </summary>
public sealed class AcStatePayload
{
    /// <summary>
    /// 運転中か
    /// </summary>
    [JsonPropertyName("power")]
    public bool? Power { get; set; }

    /// <summary>
    /// 運転モードの通信用文字列
    /// </summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    /// <summary>
    /// 目標温度
    /// </summary>
    [JsonPropertyName("target")]
    public int? Target { get; set; }

    /// <summary>
    /// 風量の通信用文字列
    /// </summary>
    [JsonPropertyName("fan")]
    public string? Fan { get; set; }

    /// <summary>
    /// 風向きの通信用文字列
    /// </summary>
    [JsonPropertyName("airflow")]
    public string? Airflow { get; set; }
}

/// <summary>
/// MQTTのペイロードのJSON変換で共通に使う設定
/// </summary>
internal static class MqttJson
{
    /// <summary>
    /// JSONの変換設定
    /// </summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
