namespace SmartRemoconMonitor.Services.Ui;

/// <summary>
/// 処理をUIスレッドで実行する。
/// </summary>
public interface IUiDispatcher
{
    /// <summary>
    /// 処理をUIスレッドで実行する。
    /// </summary>
    void Dispatch(Action action);
}
