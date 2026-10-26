using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SmartRemoconMonitor.Models;
using SmartRemoconMonitor.Services.Export;

namespace SmartRemoconMonitor.ViewModels;

/// <summary>
/// システム設定画面
/// </summary>
public sealed partial class SettingPanelViewModel : ObservableObject
{
    /// <summary>
    /// CSV出力
    /// </summary>
    private readonly ISensorDataExportService _exporter;

    /// <summary>
    /// ロガー
    /// </summary>
    private readonly ILogger<SettingPanelViewModel> _logger;

    /// <summary>
    /// CSVに書き出す期間
    /// </summary>
    [ObservableProperty]
    private HistoryRange exportRange;

    /// <summary>
    /// CSV出力の結果のメッセージ
    /// </summary>
    [ObservableProperty]
    private string? exportStatusMessage;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public SettingPanelViewModel(
        ISensorDataExportService exporter,
        ILogger<SettingPanelViewModel> logger)
    {
        _exporter = exporter;
        _logger = logger;

        exportRange = ExportRangeOptions[1];
    }

    /// <summary>
    /// CSVに書き出す期間の選択肢
    /// </summary>
    public IReadOnlyList<HistoryRange> ExportRangeOptions { get; } = HistoryRange.Defaults;

    /// <summary>
    /// 選択した期間の測定値をCSVに書き出す。
    /// </summary>
    [RelayCommand]
    private async Task ExportCsvAsync(CancellationToken cancellationToken)
    {
        try
        {
            ExportStatusMessage = "書き出しています...";

            var nowUtc = DateTimeOffset.UtcNow;
            var result = await _exporter
                .ExportAsync(nowUtc - ExportRange.Duration, nowUtc, cancellationToken)
                .ConfigureAwait(true);

            ExportStatusMessage = result switch
            {
                { Cancelled: true } => "書き出しを中止しました。",
                { RowCount: 0 } => $"{ExportRange.Label}に記録がありません。",
                _ => $"{result.RowCount}件を書き出しました: {result.FilePath}",
            };
        }
        catch (OperationCanceledException)
        {
            ExportStatusMessage = "書き出しを中止しました。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSV export failed.");
            ExportStatusMessage = $"書き出しに失敗しました: {ex.Message}";
        }
    }
}
