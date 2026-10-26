using System.Windows.Threading;

namespace SmartRemoconMonitor.Services.Ui;

/// <summary>
/// <see cref="IUiDispatcher"/>。
/// </summary>
public sealed class UiDispatcher : IUiDispatcher
{
    /// <summary>
    /// Dispatcher
    /// </summary>
    private readonly Dispatcher _dispatcher;

    /// <summary>
    /// UIスレッドのディスパッチャーを受け取って作る。
    /// </summary>
    public UiDispatcher(Dispatcher dispatcher) => _dispatcher = dispatcher;

    /// <summary>
    /// 処理をUIスレッドで実行する。アプリの終了処理が始まっていたら何もしない。
    /// </summary>
    public void Dispatch(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_dispatcher.CheckAccess())
        {
            action();
            return;
        }

        if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
        {
            return;
        }

        _dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
    }
}
