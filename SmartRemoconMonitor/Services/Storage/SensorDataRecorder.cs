using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartRemoconMonitor.Configuration;
using SmartRemoconMonitor.Services.Mqtt;

namespace SmartRemoconMonitor.Services.Storage;

/// <summary>
/// 受信した測定値を、間引いてから保存先へ渡す。
/// </summary>
public sealed class SensorDataRecorder : IHostedService
{
    /// <summary>
    /// 測定値の受信元
    /// </summary>
    private readonly IMqttService _mqtt;

    /// <summary>
    /// 測定値の保存先
    /// </summary>
    private readonly ISensorDataStore _store;

    /// <summary>
    /// 保存の設定
    /// </summary>
    private readonly StorageOptions _options;

    /// <summary>
    /// ロガー
    /// </summary>
    private readonly ILogger<SensorDataRecorder> _logger;

    /// <summary>
    /// 最後に保存を依頼した測定値の受信時刻
    /// </summary>
    private DateTimeOffset _lastWrittenAt = DateTimeOffset.MinValue;

    /// <summary>
    /// 保存先の満杯をすでにログに出したか
    /// </summary>
    private bool _bufferFullReported;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public SensorDataRecorder(
        IMqttService mqtt,
        ISensorDataStore store,
        IOptions<StorageOptions> options,
        ILogger<SensorDataRecorder> logger)
    {
        _mqtt = mqtt;
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 測定値の受信イベントを登録する。
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _mqtt.SensorReadingReceived += OnSensorReadingReceived;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 測定値の受信イベントを解除する。
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _mqtt.SensorReadingReceived -= OnSensorReadingReceived;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 前回の保存から最小間隔が過ぎていれば、測定値の保存を依頼する。
    /// </summary>
    private void OnSensorReadingReceived(object? sender, SensorReading reading)
    {
        if (reading.ReceivedAt - _lastWrittenAt < _options.MinimumWriteInterval)
        {
            return;
        }

        _lastWrittenAt = reading.ReceivedAt;

        var accepted = _store.Enqueue(new SensorRecord(
            reading.ReceivedAt,
            reading.Temperature,
            reading.Humidity));

        if (accepted)
        {
            _bufferFullReported = false;
            return;
        }

        if (!_bufferFullReported)
        {
            _bufferFullReported = true;
            _logger.LogWarning("Sensor data buffer is full. Older records are being dropped.");
        }
    }
}
