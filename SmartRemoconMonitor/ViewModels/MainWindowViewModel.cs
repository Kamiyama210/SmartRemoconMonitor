using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MahApps.Metro.IconPacks;
using Microsoft.Extensions.Logging;
using SmartRemoconMonitor.Models;
using SmartRemoconMonitor.Services.Navigation;

namespace SmartRemoconMonitor.ViewModels;

/// <summary>
/// メインウィンドウ。左側のメニューと、表示中の画面を管理する。
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    /// <summary>
    /// ロガー
    /// </summary>
    private readonly ILogger<MainWindowViewModel> _logger;

    /// <summary>
    /// メニューの文字を表示するか。falseならアイコンだけを表示する。
    /// </summary>
    [ObservableProperty]
    private bool isMenuLabelVisible;

    /// <summary>
    /// 画面の下に表示するメッセージ
    /// </summary>
    [ObservableProperty]
    private string? statusMessage;

    /// <summary>
    /// コンストラクタ。画面の切り替えを受け取り、メニュー項目を用意する。
    /// </summary>
    public MainWindowViewModel(NavigationHost navigation, ILogger<MainWindowViewModel> logger)
    {
        Navigation = navigation;
        _logger = logger;

        MenuItems =
        [
            new NavigationMenuItem(AppSection.Dashboard, "ダッシュボード", PackIconMaterialKind.ViewDashboard),
            new NavigationMenuItem(AppSection.Remote, "リモコン操作", PackIconMaterialKind.Remote),
            new NavigationMenuItem(AppSection.Settings, "設定", PackIconMaterialKind.Cog),
        ];
    }

    /// <summary>
    /// 画面の切り替え
    /// </summary>
    public NavigationHost Navigation { get; }

    /// <summary>
    /// 左側のメニュー項目
    /// </summary>
    public IReadOnlyList<NavigationMenuItem> MenuItems { get; }

    /// <summary>
    /// 最初の画面（タイトル画面）を表示する。
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
        => await NavigateAsync(AppSection.Title, cancellationToken).ConfigureAwait(true);

    /// <summary>
    /// 表示中の画面のdisposeをする。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await Navigation.DisposeAsync().ConfigureAwait(false);
        _logger.LogDebug("Disposed {ViewModel}", nameof(MainWindowViewModel));
    }

    /// <summary>
    /// メニュー文字の表示・非表示を切り替える。
    /// </summary>
    [RelayCommand]
    private void ToggleMenuLabels() => IsMenuLabelVisible = !IsMenuLabelVisible;

    /// <summary>
    /// 指定した画面へ切り替え、メニューの選択状態を更新する。
    /// </summary>
    [RelayCommand]
    private async Task NavigateAsync(AppSection section, CancellationToken cancellationToken)
    {
        try
        {
            StatusMessage = null;

            await (section switch
            {
                AppSection.Title => Navigation.NavigateToAsync<AppTitleViewModel>(cancellationToken),
                AppSection.Dashboard => Navigation.NavigateToAsync<DashboardPanelViewModel>(cancellationToken),
                AppSection.Remote => Navigation.NavigateToAsync<RemotePanelViewModel>(cancellationToken),
                AppSection.Settings => Navigation.NavigateToAsync<SettingPanelViewModel>(cancellationToken),
            }).ConfigureAwait(true);

            foreach (var item in MenuItems)
            {
                item.IsSelected = item.Section == section;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation to {Section} failed.", section);
            StatusMessage = $"画面の切り替えに失敗しました: {ex.Message}";
        }
    }
}
