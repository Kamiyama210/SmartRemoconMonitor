namespace SmartRemoconMonitor.Services.Navigation;

/// <summary>
/// 画面の表示・非表示の通知を受け取るViewModel
/// </summary>
public interface INavigationAware
{
    /// <summary>
    /// 画面が表示された直後に1回呼ばれる。
    /// </summary>
    Task OnNavigatedToAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 画面が外される直前に呼ばれる。
    /// </summary>
    Task OnNavigatedFromAsync();
}
