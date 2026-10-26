using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartRemoconMonitor.Configuration;
using SmartRemoconMonitor.Models;
using SmartRemoconMonitor.Services.Mqtt;
using SmartRemoconMonitor.Services.Navigation;
using SmartRemoconMonitor.Services.Ui;

namespace SmartRemoconMonitor.ViewModels;

/// <summary>
/// リモコン操作画面
/// </summary>
public sealed partial class RemotePanelViewModel : ObservableObject, INavigationAware
{
    /// <summary>
    /// MQTT通信
    /// </summary>
    private readonly IMqttService _mqtt;

    /// <summary>
    /// エアコンまわりの設定
    /// </summary>
    private readonly AcOptions _acOptions;

    /// <summary>
    /// UIスレッドへの受け渡し
    /// </summary>
    private readonly IUiDispatcher _dispatcher;

    /// <summary>
    /// ロガー
    /// </summary>
    private readonly ILogger<RemotePanelViewModel> _logger;

    /// <summary>
    /// 接続状態とマイコンの生死のイベントを登録中か
    /// </summary>
    private bool _subscribed;

    /// <summary>
    /// 目標温度
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IncreaseTemperatureCommand))]
    [NotifyCanExecuteChangedFor(nameof(DecreaseTemperatureCommand))]
    private int targetTemperature;

    /// <summary>
    /// 選択中の運転モード
    /// </summary>
    [ObservableProperty]
    private AcOption<OperationMode> selectedMode;

    /// <summary>
    /// 選択中の風量
    /// </summary>
    [ObservableProperty]
    private AcOption<FanSpeed> selectedFanSpeed;

    /// <summary>
    /// 選択中の風向き
    /// </summary>
    [ObservableProperty]
    private AcOption<AirflowDirection> selectedAirflowDirection;

    /// <summary>
    /// ブローカと接続しているか
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommandCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool isConnected;

    /// <summary>
    /// マイコンがブローカに接続しているか。nullは不明
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommandCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool? isDeviceOnline;

    /// <summary>
    /// 送信結果などのメッセージ
    /// </summary>
    [ObservableProperty]
    private string? statusMessage;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public RemotePanelViewModel(
        IMqttService mqtt,
        IUiDispatcher dispatcher,
        IOptionsMonitor<AcOptions> acOptions,
        ILogger<RemotePanelViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(acOptions);

        _mqtt = mqtt;
        _dispatcher = dispatcher;
        _acOptions = acOptions.CurrentValue;
        _logger = logger;

        isConnected = mqtt.IsConnected;
        isDeviceOnline = mqtt.IsDeviceOnline;
        targetTemperature = _acOptions.Clamp(_acOptions.DefaultTargetTemperature);
        selectedMode = ModeOptions[0];
        selectedFanSpeed = FanSpeedOptions[0];
        selectedAirflowDirection = AirflowOptions[0];
    }

    /// <summary>
    /// 運転モードの選択肢
    /// </summary>
    public IReadOnlyList<AcOption<OperationMode>> ModeOptions { get; } = AcOperationOptions.Modes;

    /// <summary>
    /// 風量の選択肢
    /// </summary>
    public IReadOnlyList<AcOption<FanSpeed>> FanSpeedOptions { get; } = AcOperationOptions.FanSpeeds;

    /// <summary>
    /// 風向きの選択肢
    /// </summary>
    public IReadOnlyList<AcOption<AirflowDirection>> AirflowOptions { get; } = AcOperationOptions.AirflowDirections;

    /// <summary>
    /// 目標温度の下限。
    /// </summary>
    public int MinTargetTemperature => _acOptions.MinTargetTemperature;

    /// <summary>
    /// 目標温度の上限。
    /// </summary>
    public int MaxTargetTemperature => _acOptions.MaxTargetTemperature;

    /// <summary>
    /// 送信できる状態か。ブローカに接続していて、マイコンが停止していると分かっていなければ送信できる。
    /// </summary>
    public bool CanSend => IsConnected && IsDeviceOnline != false;

    /// <summary>
    /// 接続状態とマイコンの生死のイベントを登録し、マイコンが報告した最新の状態を選択肢に反映する。
    /// </summary>
    public Task OnNavigatedToAsync(CancellationToken cancellationToken)
    {
        if (!_subscribed)
        {
            _mqtt.ConnectionChanged += OnConnectionChanged;
            _mqtt.DeviceOnlineChanged += OnDeviceOnlineChanged;
            _subscribed = true;
        }

        IsConnected = _mqtt.IsConnected;
        IsDeviceOnline = _mqtt.IsDeviceOnline;

        if (_mqtt.LastAcState is { } state)
        {
            ApplyState(state);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 接続状態とマイコンの生死のイベントを解除する。
    /// </summary>
    public Task OnNavigatedFromAsync()
    {
        if (_subscribed)
        {
            _mqtt.ConnectionChanged -= OnConnectionChanged;
            _mqtt.DeviceOnlineChanged -= OnDeviceOnlineChanged;
            _subscribed = false;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 目標温度を1℃上げる。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanIncreaseTemperature))]
    private void IncreaseTemperature() => TargetTemperature = _acOptions.Clamp(TargetTemperature + 1);

    /// <summary>
    /// 目標温度を上げられるか。
    /// </summary>
    private bool CanIncreaseTemperature() => TargetTemperature < _acOptions.MaxTargetTemperature;

    /// <summary>
    /// 目標温度を1℃下げる。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDecreaseTemperature))]
    private void DecreaseTemperature() => TargetTemperature = _acOptions.Clamp(TargetTemperature - 1);

    /// <summary>
    /// 目標温度を下げられるか。
    /// </summary>
    private bool CanDecreaseTemperature() => TargetTemperature > _acOptions.MinTargetTemperature;

    /// <summary>
    /// 選択中の内容で運転操作を送る。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSend))]
    private Task SendCommandAsync(CancellationToken cancellationToken)
        => PublishAsync(power: true, "運転操作", cancellationToken);

    /// <summary>
    /// 停止操作を送る。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSend))]
    private Task StopAsync(CancellationToken cancellationToken)
        => PublishAsync(power: false, "停止操作", cancellationToken);

    /// <summary>
    /// マイコンから受信した状態を、画面の選択肢に反映する。
    /// </summary>
    private void ApplyState(AcState state)
    {
        TargetTemperature = _acOptions.Clamp(state.TargetTemperature);
        SelectedMode = Find(ModeOptions, state.Mode);
        SelectedFanSpeed = Find(FanSpeedOptions, state.FanSpeed);
        SelectedAirflowDirection = Find(AirflowOptions, state.AirflowDirection);
    }

    /// <summary>
    /// 列挙値に対応する選択肢を探す。見つからなければ先頭の選択肢を返す。
    /// </summary>
    private static AcOption<T> Find<T>(IReadOnlyList<AcOption<T>> options, T value)
        where T : notnull
    {
        foreach (var option in options)
        {
            if (option.Value.Equals(value))
            {
                return option;
            }
        }

        return options[0];
    }

    /// <summary>
    /// 接続状態イベントを画面に反映する。
    /// </summary>
    private void OnConnectionChanged(object? sender, bool connected)
        => _dispatcher.Dispatch(() => IsConnected = connected);

    /// <summary>
    /// マイコンの生死を画面に反映する。
    /// </summary>
    private void OnDeviceOnlineChanged(object? sender, bool online)
        => _dispatcher.Dispatch(() => IsDeviceOnline = online);

    /// <summary>
    /// 選択中の内容で操作内容を作って送信し、結果をメッセージに表示する。
    /// </summary>
    private async Task PublishAsync(bool power, string label, CancellationToken cancellationToken)
    {
        try
        {
            StatusMessage = $"{label}を送信中...";

            var command = new AcCommand(
                power,
                SelectedMode.Value,
                TargetTemperature,
                SelectedFanSpeed.Value,
                SelectedAirflowDirection.Value);

            await _mqtt.PublishCommandAsync(command, cancellationToken).ConfigureAwait(true);
            StatusMessage = $"{label}を送信しました（{DateTime.Now:HH:mm:ss}）";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = $"{label}の送信を中止しました。";
        }
        catch (MqttPublishException ex)
        {
            _logger.LogWarning(ex, "Failed to publish {Label}.", label);
            StatusMessage = $"{label}の送信に失敗しました: {ex.Message}";
        }
    }
}
