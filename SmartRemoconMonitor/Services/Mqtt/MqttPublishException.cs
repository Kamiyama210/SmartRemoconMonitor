namespace SmartRemoconMonitor.Services.Mqtt;

/// <summary>
/// MQTT送信例外
/// </summary>
public sealed class MqttPublishException : Exception
{
    /// <summary>
    /// メッセージを指定して作る。
    /// </summary>
    public MqttPublishException(string message) : base(message)
    {
    }

    /// <summary>
    /// メッセージと、原因になった例外を指定して作る
    /// </summary>
    public MqttPublishException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
