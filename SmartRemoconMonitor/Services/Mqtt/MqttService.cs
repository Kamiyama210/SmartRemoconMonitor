using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using SmartRemoconMonitor.Configuration;
using SmartRemoconMonitor.Models;

namespace SmartRemoconMonitor.Services.Mqtt;

/// <summary>
/// MQTTブローカとの接続・再接続・送受信を行うバックグラウンドサービス
/// </summary>
public sealed class MqttService : BackgroundService, IMqttService
{
    /// <summary>
    /// 受け付ける温度の下限
    /// </summary>
    private const double MinTemperatureCelsius = -50;

    /// <summary>
    /// 受け付ける温度の上限
    /// </summary>
    private const double MaxTemperatureCelsius = 100;

    /// <summary>
    /// 受け付ける湿度の下限
    /// </summary>
    private const double MinHumidityPercent = 0;

    /// <summary>
    /// 受け付ける湿度の上限
    /// </summary>
    private const double MaxHumidityPercent = 100;

    /// <summary>
    /// ログに出すペイロードの最大文字数
    /// </summary>
    private const int MaxLoggedPayloadLength = 256;

    /// <summary>
    /// 接続設定
    /// </summary>
    private readonly MqttOptions _options;

    /// <summary>
    /// ロガー
    /// </summary>
    private readonly ILogger<MqttService> _logger;

    /// <summary>
    /// MQTTnetのオブジェクト
    /// </summary>
    private readonly MqttClientFactory _factory = new();

    /// <summary>
    /// MQTTクライアント
    /// </summary>
    private readonly IMqttClient _client;

    /// <summary>
    /// 接続に使うオプション
    /// </summary>
    private readonly MqttClientOptions _clientOptions;

    /// <summary>
    /// 最新の受信値を読み書きするときのロック
    /// </summary>
    private readonly Lock _latestLock = new();

    /// <summary>
    /// 最新の測定値
    /// </summary>
    private SensorReading? _lastSensorReading;

    /// <summary>
    /// 最新の運転状態
    /// </summary>
    private AcState? _lastAcState;

    /// <summary>
    /// 最新のマイコンの生死
    /// </summary>
    private bool? _isDeviceOnline;

    /// <summary>
    /// 切断されたことを再接続ループへ知らせる合図
    /// </summary>
    private TaskCompletionSource _disconnectedSignal =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public MqttService(IOptions<MqttOptions> options, ILogger<MqttService> logger)
    {
        _options = options.Value;
        _logger = logger;

        _client = _factory.CreateMqttClient();
        _clientOptions = BuildClientOptions(_options);

        _client.ApplicationMessageReceivedAsync += OnApplicationMessageReceivedAsync;
        _client.ConnectedAsync += OnConnectedAsync;
        _client.DisconnectedAsync += OnDisconnectedAsync;
    }

    /// <inheritdoc/>
    public bool IsConnected => _client.IsConnected;

    /// <inheritdoc/>
    public SensorReading? LastSensorReading
    {
        get { lock (_latestLock) { return _lastSensorReading; } }
        private set { lock (_latestLock) { _lastSensorReading = value; } }
    }

    /// <inheritdoc/>
    public AcState? LastAcState
    {
        get { lock (_latestLock) { return _lastAcState; } }
        private set { lock (_latestLock) { _lastAcState = value; } }
    }

    /// <inheritdoc/>
    public bool? IsDeviceOnline
    {
        get { lock (_latestLock) { return _isDeviceOnline; } }
        private set { lock (_latestLock) { _isDeviceOnline = value; } }
    }

    /// <inheritdoc/>
    public event EventHandler<bool>? DeviceOnlineChanged;

    /// <inheritdoc/>
    public event EventHandler<bool>? ConnectionChanged;

    /// <inheritdoc/>
    public event EventHandler<SensorReading>? SensorReadingReceived;

    /// <inheritdoc/>
    public event EventHandler<AcState>? AcStateReceived;

    /// <inheritdoc/>
    public async Task PublishCommandAsync(AcCommand command, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(
            new AcCommandPayload
            {
                Power = command.Power,
                Mode = AcOperationOptions.ToWire(command.Mode),
                Target = command.TargetTemperature,
                Fan = AcOperationOptions.ToWire(command.FanSpeed),
                Airflow = AcOperationOptions.ToWire(command.AirflowDirection),
            },
            MqttJson.Options);

        await PublishAsync(MqttTopics.Command, payload, retain: false, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Published command to {Topic}: {Payload}", MqttTopics.Command, payload);
    }

    /// <summary>
    /// 再接続ループを止め、ブローカから切断する。
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await TryDisconnectAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// イベントの登録を解除し、MQTTクライアントを破棄する。
    /// </summary>
    public override void Dispose()
    {
        _client.ApplicationMessageReceivedAsync -= OnApplicationMessageReceivedAsync;
        _client.ConnectedAsync -= OnConnectedAsync;
        _client.DisconnectedAsync -= OnDisconnectedAsync;
        _client.Dispose();
        base.Dispose();
    }

    /// <summary>
    /// 接続とサブスクライブを行い、切断されたら待ち時間を置いて再接続する。
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var failures = 0;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _disconnectedSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                TimeSpan wait;
                if (await TryConnectAsync(stoppingToken).ConfigureAwait(false))
                {
                    failures = 0;
                    await _disconnectedSignal.Task.WaitAsync(stoppingToken).ConfigureAwait(false);
                    _logger.LogWarning("Disconnected from MQTT broker. Reconnecting...");
                    wait = _options.ReconnectInitialDelay;
                }
                else
                {
                    wait = RetryDelay(_options.ReconnectInitialDelay, _options.ReconnectMaxDelay, failures);
                    failures++;
                    _logger.LogWarning("Retrying MQTT connection in {Delay}.", wait);
                }

                await Task.Delay(wait, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "MQTT reconnect loop stopped.");
        }
    }

    /// <summary>
    /// ac/sensorのペイロードを解析する。不正な値や範囲外の値ならfalseを返す。
    /// </summary>
    internal bool TryParseSensorReading(string? json, out SensorReading reading)
    {
        reading = default;

        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogWarning("Empty sensor payload received.");
            return false;
        }

        SensorPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SensorPayload>(json, MqttJson.Options);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Malformed sensor payload: {Payload}", Truncate(json));
            return false;
        }

        if (payload?.Temp is not { } temp || payload.Hum is not { } hum)
        {
            _logger.LogWarning("Sensor payload missing temp/hum: {Payload}", Truncate(json));
            return false;
        }

        if (!double.IsFinite(temp) || temp < MinTemperatureCelsius || temp > MaxTemperatureCelsius ||
            !double.IsFinite(hum) || hum < MinHumidityPercent || hum > MaxHumidityPercent)
        {
            _logger.LogWarning("Sensor reading out of range (temp={Temp}, hum={Hum}). Discarded.", temp, hum);
            return false;
        }

        reading = new SensorReading(temp, hum, DateTimeOffset.UtcNow);
        return true;
    }

    /// <summary>
    /// ac/stateのペイロードを解析する。項目が無い、範囲外、未知の値のいずれかならfalseを返す。
    /// </summary>
    internal bool TryParseAcState(string? json, out AcState state)
    {
        state = default;

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        AcStatePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<AcStatePayload>(json, MqttJson.Options);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Malformed state payload: {Payload}", Truncate(json));
            return false;
        }

        if (payload?.Power is not { } power || payload.Target is not { } target)
        {
            _logger.LogWarning("State payload missing power/target: {Payload}", Truncate(json));
            return false;
        }

        if (target < AcOptions.MinAllowedTemperature || target > AcOptions.MaxAllowedTemperature)
        {
            _logger.LogWarning("State target out of range ({Target}): {Payload}", target, Truncate(json));
            return false;
        }

        if (!AcOperationOptions.TryFromWire(payload.Mode, out OperationMode mode) ||
            !AcOperationOptions.TryFromWire(payload.Fan, out FanSpeed fan) ||
            !AcOperationOptions.TryFromWire(payload.Airflow, out AirflowDirection airflow))
        {
            _logger.LogWarning("State payload has unknown mode/fan/airflow: {Payload}", Truncate(json));
            return false;
        }

        state = new AcState(power, mode, target, fan, airflow, DateTimeOffset.UtcNow);
        return true;
    }

    /// <summary>
    /// ac/statusのペイロードを解析する。それ以外ならfalseを返す。
    /// </summary>
    internal bool TryParseDeviceStatus(string? payload, out bool online)
    {
        switch (payload)
        {
            case MqttStatus.Online:
                online = true;
                return true;
            case MqttStatus.Offline:
                online = false;
                return true;
            default:
                online = false;
                _logger.LogWarning("Unknown device status payload: {Payload}", Truncate(payload ?? string.Empty));
                return false;
        }
    }

    /// <summary>
    /// 接続の失敗がfailures回続いたあとの待ち時間を返す。
    /// </summary>
    internal static TimeSpan RetryDelay(TimeSpan initial, TimeSpan max, int failures)
    {
        var delay = initial;
        for (var i = 0; i < failures && delay < max; i++)
        {
            delay = TimeSpan.FromTicks(delay.Ticks * 2);
        }

        return delay > max ? max : delay;
    }

    /// <summary>
    /// ブローカへ接続してサブスクライブする。
    /// </summary>
    private async Task<bool> TryConnectAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            connectCts.CancelAfter(_options.ConnectTimeout);
            await _client.ConnectAsync(_clientOptions, connectCts.Token).ConfigureAwait(false);
            await SubscribeAsync(connectCts.Token).ConfigureAwait(false);

            _logger.LogInformation("Connected to MQTT broker {Host}:{Port}", _options.Host, _options.Port);
            return true;
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "MQTT connection attempt failed.");
            return false;
        }
    }

    /// <summary>
    /// QoS 1でメッセージを送信する。失敗したら<see cref="MqttPublishException"/>を投げる。
    /// </summary>
    private async Task PublishAsync(string topic, string payload, bool retain, CancellationToken cancellationToken)
    {
        if (!_client.IsConnected)
        {
            throw new MqttPublishException("MQTTブローカへ接続していないため送信できません。");
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(retain)
            .Build();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.OperationTimeout);

        try
        {
            var result = await _client.PublishAsync(message, timeout.Token).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                throw new MqttPublishException($"ブローカが送信を受理しませんでした。(ReasonCode={result.ReasonCode})");
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MqttPublishException($"送信がタイムアウトしました（{_options.OperationTimeoutSeconds}秒）。");
        }
        catch (Exception ex) when (ex is not MqttPublishException and not OperationCanceledException)
        {
            throw new MqttPublishException("送信中にエラーが発生しました。", ex);
        }
    }

    /// <summary>
    /// 設定から、接続に使うオプションを組み立てる。
    /// </summary>
    private static MqttClientOptions BuildClientOptions(MqttOptions options)
        => new MqttClientOptionsBuilder()
            .WithProtocolVersion(MqttProtocolVersion.V311)
            .WithTcpServer(options.Host, options.Port)
            .WithClientId(options.ClientId)
            .WithKeepAlivePeriod(options.KeepAlive)
            .Build();

    /// <summary>
    /// デバイスからPCへのトピックをサブスクライブする。
    /// </summary>
    private async Task SubscribeAsync(CancellationToken cancellationToken)
    {
        var subscribeOptions = _factory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(f => f
                .WithTopic(MqttTopics.Sensor)
                .WithAtLeastOnceQoS())
            .WithTopicFilter(f => f
                .WithTopic(MqttTopics.State)
                .WithAtLeastOnceQoS())
            .WithTopicFilter(f => f
                .WithTopic(MqttTopics.Status)
                .WithAtLeastOnceQoS())
            .Build();

        await _client.SubscribeAsync(subscribeOptions, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Subscribed to {SensorTopic}, {StateTopic} and {StatusTopic}",
            MqttTopics.Sensor,
            MqttTopics.State,
            MqttTopics.Status);
    }

    /// <summary>
    /// 接続中なら、ブローカから正常に切断する。失敗してもログを出すだけで例外は投げない。
    /// </summary>
    private async Task TryDisconnectAsync(CancellationToken cancellationToken)
    {
        if (!_client.IsConnected)
        {
            return;
        }

        try
        {
            var disconnectOptions = _factory.CreateClientDisconnectOptionsBuilder()
                .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                .Build();

            await _client.DisconnectAsync(disconnectOptions, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("MQTT broker disconnected gracefully.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Graceful MQTT disconnect failed.");
        }
    }

    /// <summary>
    /// 接続したことを通知する。
    /// </summary>
    private Task OnConnectedAsync(MqttClientConnectedEventArgs eventArgs)
    {
        ConnectionChanged?.Invoke(this, true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 切断されたことを再接続ループへ通知する。
    /// </summary>
    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs eventArgs)
    {
        _disconnectedSignal.TrySetResult();
        ConnectionChanged?.Invoke(this, false);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 受信したメッセージをトピックで振り分け、解析して通知する。
    /// </summary>
    private Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs eventArgs)
    {
        try
        {
            var topic = eventArgs.ApplicationMessage.Topic;
            var json = Encoding.UTF8.GetString(eventArgs.ApplicationMessage.Payload);

            if (string.Equals(topic, MqttTopics.Sensor, StringComparison.Ordinal))
            {
                if (TryParseSensorReading(json, out var reading))
                {
                    LastSensorReading = reading;
                    SensorReadingReceived?.Invoke(this, reading);
                }
            }
            else if (string.Equals(topic, MqttTopics.State, StringComparison.Ordinal))
            {
                if (TryParseAcState(json, out var state))
                {
                    LastAcState = state;
                    AcStateReceived?.Invoke(this, state);
                }
            }
            else if (string.Equals(topic, MqttTopics.Status, StringComparison.Ordinal))
            {
                if (TryParseDeviceStatus(json, out var online))
                {
                    IsDeviceOnline = online;
                    DeviceOnlineChanged?.Invoke(this, online);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error while processing an MQTT message.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 長い文字列を切り詰める。
    /// </summary>
    private static string Truncate(string value) =>
        value.Length <= MaxLoggedPayloadLength ? value : value[..MaxLoggedPayloadLength] + "...";
}
