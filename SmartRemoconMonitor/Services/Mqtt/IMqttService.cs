namespace SmartRemoconMonitor.Services.Mqtt;

/// <summary>
/// MQTT通信関連
/// </summary>
public interface IMqttService
{
    /// <summary>
    /// ブローカと接続しているか
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 最後に受信した測定値。未受信ならnull。
    /// </summary>
    SensorReading? LastSensorReading { get; }

    /// <summary>
    /// 最後に受信した運転状態。未受信ならnull。
    /// </summary>
    AcState? LastAcState { get; }

    /// <summary>
    /// デバイスがブローカに接続しているか。未受信ならnull。
    /// </summary>
    bool? IsDeviceOnline { get; }

    /// <summary>
    /// ac/statusを受信したときに発生する。
    /// </summary>
    event EventHandler<bool>? DeviceOnlineChanged;

    /// <summary>
    /// ブローカとの接続状態が変わったときに発生する。
    /// </summary>
    event EventHandler<bool>? ConnectionChanged;

    /// <summary>
    /// 測定値を受信したときに発生する。
    /// </summary>
    event EventHandler<SensorReading>? SensorReadingReceived;

    /// <summary>
    /// 運転状態を受信したときに発生する。
    /// </summary>
    event EventHandler<AcState>? AcStateReceived;

    /// <summary>
    /// 運転操作を送信する。
    /// </summary>
    Task PublishCommandAsync(AcCommand command, CancellationToken cancellationToken = default);
}
