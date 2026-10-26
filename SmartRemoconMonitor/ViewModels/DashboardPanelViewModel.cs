using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartRemoconMonitor.Configuration;
using SmartRemoconMonitor.Models;
using SmartRemoconMonitor.Services.Mqtt;
using SmartRemoconMonitor.Services.Navigation;
using SmartRemoconMonitor.Services.Storage;
using SmartRemoconMonitor.Services.Ui;

namespace SmartRemoconMonitor.ViewModels;

/// <summary>
/// ダッシュボード。現在の温湿度、エアコンの状態、接続状態、グラフを表示する。
/// </summary>
public sealed partial class DashboardPanelViewModel : ObservableObject, INavigationAware, IAsyncDisposable
{
    /// <summary>
    /// グラフに表示する期間
    /// </summary>
    private static readonly TimeSpan TrendWindow = TimeSpan.FromHours(24);

    /// <summary>
    /// 測定値の途絶を確認する間隔
    /// </summary>
    private static readonly TimeSpan StaleCheckInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 途絶の確認を何回行うごとにグラフを更新するか
    /// </summary>
    private const int TrendRefreshEveryNTicks = 6;

    /// <summary>
    /// MQTT通信
    /// </summary>
    private readonly IMqttService _mqtt;

    /// <summary>
    /// 測定値の保存
    /// </summary>
    private readonly ISensorDataStore _store;

    /// <summary>
    /// UIスレッドへの受け渡し
    /// </summary>
    private readonly IUiDispatcher _dispatcher;

    /// <summary>
    /// エアコンまわりの設定
    /// </summary>
    private readonly AcOptions _acOptions;

    /// <summary>
    /// ロガー
    /// </summary>
    private readonly ILogger<DashboardPanelViewModel> _logger;

    /// <summary>
    /// 監視ループを止めるためのトークン
    /// </summary>
    private CancellationTokenSource? _watchCts;

    /// <summary>
    /// 実行中の監視ループ
    /// </summary>
    private Task? _watchTask;

    /// <summary>
    /// 破棄済みか
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 現在の温度
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSensorReading))]
    [NotifyPropertyChangedFor(nameof(DeviceStatus))]
    [NotifyPropertyChangedFor(nameof(DeviceStatusText))]
    private double? currentTemperature;

    /// <summary>
    /// 現在の湿度
    /// </summary>
    [ObservableProperty]
    private double? currentHumidity;

    /// <summary>
    /// 測定値が途絶えているか
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceStatus))]
    [NotifyPropertyChangedFor(nameof(DeviceStatusText))]
    private bool isSensorDataStale = true;

    /// <summary>
    /// デバイスがブローカに接続しているか
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceStatus))]
    [NotifyPropertyChangedFor(nameof(DeviceStatusText))]
    private bool? isDeviceOnline;

    /// <summary>
    /// 最後に測定値を受信した時刻
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastUpdatedLocal))]
    private DateTimeOffset? lastUpdatedAt;

    /// <summary>
    /// マイコンが報告した運転状態
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAcState))]
    [NotifyPropertyChangedFor(nameof(PowerText))]
    [NotifyPropertyChangedFor(nameof(ModeText))]
    [NotifyPropertyChangedFor(nameof(TargetTemperatureText))]
    [NotifyPropertyChangedFor(nameof(FanSpeedText))]
    [NotifyPropertyChangedFor(nameof(AirflowText))]
    private AcState? currentAcState;

    /// <summary>
    /// ブローカと接続しているか
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BrokerStatus))]
    [NotifyPropertyChangedFor(nameof(BrokerStatusText))]
    [NotifyPropertyChangedFor(nameof(DeviceStatus))]
    [NotifyPropertyChangedFor(nameof(DeviceStatusText))]
    private bool isConnected;

    /// <summary>
    /// 画面の下に表示するメッセージ
    /// </summary>
    [ObservableProperty]
    private string? statusMessage;

    /// <summary>
    /// 温度のグラフ
    /// </summary>
    [ObservableProperty]
    private TrendSeries temperatureTrend = TrendSeries.Empty;

    /// <summary>
    /// 湿度のグラフ
    /// </summary>
    [ObservableProperty]
    private TrendSeries humidityTrend = TrendSeries.Empty;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public DashboardPanelViewModel(
        IMqttService mqtt,
        ISensorDataStore store,
        IUiDispatcher dispatcher,
        IOptionsMonitor<AcOptions> acOptions,
        ILogger<DashboardPanelViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(acOptions);

        _mqtt = mqtt;
        _store = store;
        _dispatcher = dispatcher;
        _acOptions = acOptions.CurrentValue;
        _logger = logger;

        isConnected = mqtt.IsConnected;
        isDeviceOnline = mqtt.IsDeviceOnline;
        currentAcState = mqtt.LastAcState;
    }

    /// <summary>
    /// 測定値を1回でも受信したか
    /// </summary>
    public bool HasSensorReading => CurrentTemperature.HasValue;

    /// <summary>
    /// ブローカの状態ランプの色
    /// </summary>
    public StatusLevel BrokerStatus => IsConnected ? StatusLevel.Ok : StatusLevel.Error;

    /// <summary>
    /// ブローカの状態
    /// </summary>
    public string BrokerStatusText => IsConnected ? "ブローカ接続" : "ブローカ未接続";

    /// <summary>
    /// マイコンの状態ランプの色
    /// </summary>
    public StatusLevel DeviceStatus => EvaluateDevice().Level;

    /// <summary>
    /// マイコンの状態
    /// </summary>
    public string DeviceStatusText => EvaluateDevice().Text;

    /// <summary>
    /// 最後に測定値を受信した時刻
    /// </summary>
    public DateTimeOffset? LastUpdatedLocal => LastUpdatedAt?.ToLocalTime();

    /// <summary>
    /// 運転状態を1回でも受信したか
    /// </summary>
    public bool HasAcState => CurrentAcState.HasValue;

    /// <summary>
    /// 運転・停止
    /// </summary>
    public string PowerText => CurrentAcState is { } s ? (s.Power ? "運転中" : "停止中") : "不明";

    /// <summary>
    /// 運転モード
    /// </summary>
    public string ModeText => CurrentAcState is { } s ? AcOperationOptions.LabelOf(AcOperationOptions.Modes, s.Mode) : "--";

    /// <summary>
    /// 目標温度
    /// </summary>
    public string TargetTemperatureText => CurrentAcState is { } s ? $"{s.TargetTemperature}℃" : "--";

    /// <summary>
    /// 風量
    /// </summary>
    public string FanSpeedText => CurrentAcState is { } s ? AcOperationOptions.LabelOf(AcOperationOptions.FanSpeeds, s.FanSpeed) : "--";

    /// <summary>
    /// 風向き
    /// </summary>
    public string AirflowText => CurrentAcState is { } s ? AcOperationOptions.LabelOf(AcOperationOptions.AirflowDirections, s.AirflowDirection) : "--";

    /// <summary>
    /// MQTTのイベントを登録し、最新の値を反映して、監視ループとグラフの読み込みを始める。
    /// </summary>
    public Task OnNavigatedToAsync(CancellationToken cancellationToken)
    {
        _mqtt.SensorReadingReceived += OnSensorReadingReceived;
        _mqtt.AcStateReceived += OnAcStateReceived;
        _mqtt.ConnectionChanged += OnConnectionChanged;
        _mqtt.DeviceOnlineChanged += OnDeviceOnlineChanged;

        IsConnected = _mqtt.IsConnected;
        IsDeviceOnline = _mqtt.IsDeviceOnline;

        if (_mqtt.LastSensorReading is { } last)
        {
            ApplySensorReading(last);
        }

        CurrentAcState = _mqtt.LastAcState;

        _watchCts = new CancellationTokenSource();
        _watchTask = WatchAsync(_watchCts.Token);
        _ = RefreshTrendAsync(CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <summary>
    /// MQTTイベントを解除し、監視ループを止める。
    /// </summary>
    public async Task OnNavigatedFromAsync()
    {
        Unsubscribe();
        await StopWatchAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// MQTTイベントを解除し、監視ループを止める。2回目以降は何もしない。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Unsubscribe();
        await StopWatchAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 受信した測定値を画面に反映する。
    /// </summary>
    private void OnSensorReadingReceived(object? sender, SensorReading reading)
        => _dispatcher.Dispatch(() => ApplySensorReading(reading));

    /// <summary>
    /// ブローカとの接続状態の変化を画面に反映する。
    /// </summary>
    private void OnConnectionChanged(object? sender, bool connected)
        => _dispatcher.Dispatch(() => IsConnected = connected);

    /// <summary>
    /// 受信した運転状態を画面に反映する。
    /// </summary>
    private void OnAcStateReceived(object? sender, AcState state)
        => _dispatcher.Dispatch(() => CurrentAcState = state);

    /// <summary>
    /// 受信したマイコンの状態を画面に反映する。
    /// </summary>
    private void OnDeviceOnlineChanged(object? sender, bool online)
        => _dispatcher.Dispatch(() => IsDeviceOnline = online);

    /// <summary>
    /// 測定値を画面に反映する。
    /// </summary>
    private void ApplySensorReading(SensorReading reading)
    {
        CurrentTemperature = reading.Temperature;
        CurrentHumidity = reading.Humidity;
        LastUpdatedAt = reading.ReceivedAt;
        IsSensorDataStale = IsStale(reading.ReceivedAt);
    }

    /// <summary>
    /// ブローカの状態、マイコンの状態、測定値の届き方から、マイコンのランプの色と文字を決める。
    /// </summary>
    private (StatusLevel Level, string Text) EvaluateDevice()
    {
        if (!IsConnected)
        {
            return (StatusLevel.Warning, "デバイス状態不明");
        }

        if (IsDeviceOnline == false)
        {
            return (StatusLevel.Error, "デバイス停止");
        }

        var sensorOk = HasSensorReading && !IsSensorDataStale;
        return (IsDeviceOnline, sensorOk, HasSensorReading) switch
        {
            (_, true, _) => (StatusLevel.Ok, "デバイス稼働中"),
            (true, false, _) => (StatusLevel.Warning, "センサー異常の疑い"),
            (null, false, false) => (StatusLevel.Error, "デバイス受信なし"),
            _ => (StatusLevel.Warning, "測定値が途絶"),
        };
    }

    /// <summary>
    /// 受信時刻から、測定値が途絶えたとみなす時間が過ぎたか。
    /// </summary>
    private bool IsStale(DateTimeOffset receivedAt)
        => DateTimeOffset.UtcNow - receivedAt > _acOptions.SensorTimeout;

    /// <summary>
    /// 直近24時間の測定値を読み込み、温度と湿度のグラフを作り直す。
    /// </summary>
    private async Task RefreshTrendAsync(CancellationToken cancellationToken)
    {
        try
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var records = await _store
                .ReadRangeAsync(nowUtc - TrendWindow, nowUtc, cancellationToken)
                .ConfigureAwait(false);

            var temperature = TrendSeries.Create(records, static r => r.Temperature);
            var humidity = TrendSeries.Create(records, static r => r.Humidity);

            _dispatcher.Dispatch(() =>
            {
                TemperatureTrend = temperature;
                HumidityTrend = humidity;
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load trend data.");
        }
    }

    /// <summary>
    /// MQTTイベントを解除する。
    /// </summary>
    private void Unsubscribe()
    {
        _mqtt.SensorReadingReceived -= OnSensorReadingReceived;
        _mqtt.AcStateReceived -= OnAcStateReceived;
        _mqtt.ConnectionChanged -= OnConnectionChanged;
        _mqtt.DeviceOnlineChanged -= OnDeviceOnlineChanged;
    }

    /// <summary>
    /// 監視ループを止め、終わるのを待つ。
    /// </summary>
    private async Task StopWatchAsync()
    {
        if (_watchCts is null)
        {
            return;
        }

        await _watchCts.CancelAsync().ConfigureAwait(false);

        if (_watchTask is not null)
        {
            try
            {
                await _watchTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _watchCts.Dispose();
        _watchCts = null;
        _watchTask = null;
    }

    /// <summary>
    /// 一定間隔で測定値の途絶を確認し、何回かに1回グラフを更新する。
    /// </summary>
    private async Task WatchAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(StaleCheckInterval);

        try
        {
            var tick = 0;
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                _dispatcher.Dispatch(() => IsSensorDataStale = LastUpdatedAt is not { } at || IsStale(at));

                if (++tick % TrendRefreshEveryNTicks == 0)
                {
                    await RefreshTrendAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
