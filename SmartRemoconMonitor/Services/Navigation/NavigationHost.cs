using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SmartRemoconMonitor.Services.Navigation;

/// <summary>
/// 表示中の画面（ViewModel）を管理し、画面を切り替える。
/// </summary>
public sealed partial class NavigationHost : ObservableObject, IAsyncDisposable
{
    /// <summary>
    /// ViewModelを作るためのサービスプロバイダー
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>
    /// ロガー
    /// </summary>
    private readonly ILogger<NavigationHost> _logger;

    /// <summary>
    /// 画面の切り替えを1つずつ行うためのロック
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// 破棄済みか
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 表示中の画面のViewModel
    /// </summary>
    [ObservableProperty]
    private ObservableObject? currentViewModel;

    /// <summary>
    /// 依存するサービスを受け取って作る。
    /// </summary>
    public NavigationHost(IServiceProvider services, ILogger<NavigationHost> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// 指定したViewModelの画面へ切り替える。前の画面の後片付けをしてから、新しい画面に表示を通知する。
    /// </summary>
    public async Task NavigateToAsync<TViewModel>(CancellationToken cancellationToken = default)
        where TViewModel : ObservableObject
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(true);
        try
        {
            var next = ActivatorUtilities.CreateInstance<TViewModel>(_services);
            await TeardownAsync(CurrentViewModel).ConfigureAwait(true);
            CurrentViewModel = next;
            _logger.LogDebug("Navigated to {ViewModel}", typeof(TViewModel).Name);

            if (next is INavigationAware aware)
            {
                await aware.OnNavigatedToAsync(cancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 表示中の画面の後片付けをして、破棄する。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await TeardownAsync(CurrentViewModel).ConfigureAwait(false);
        CurrentViewModel = null;
        _gate.Dispose();
    }

    /// <summary>
    /// 画面を外すことをViewModelに通知し、破棄する。失敗してもログを出すだけで例外は投げない。
    /// </summary>
    private async Task TeardownAsync(ObservableObject? viewModel)
    {
        if (viewModel is null)
        {
            return;
        }

        if (viewModel is INavigationAware aware)
        {
            try
            {
                await aware.OnNavigatedFromAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OnNavigatedFromAsync failed for {ViewModel}", viewModel.GetType().Name);
            }
        }

        try
        {
            switch (viewModel)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync().ConfigureAwait(true);
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dispose failed for {ViewModel}", viewModel.GetType().Name);
        }
    }
}
