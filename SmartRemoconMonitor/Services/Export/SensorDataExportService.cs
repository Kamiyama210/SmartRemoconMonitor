using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using SmartRemoconMonitor.Services.Storage;
using SmartRemoconMonitor.Services.Ui;

namespace SmartRemoconMonitor.Services.Export;

/// <inheritdoc cref="ISensorDataExportService"/>
public sealed class SensorDataExportService : ISensorDataExportService
{
    /// <summary>
    /// 見出しの行の最大文字数
    /// </summary>
    private const int CsvHeaderCapacity = 64;

    /// <summary>
    /// 1行あたりの最大文字数
    /// </summary>
    private const int EstimatedBytesPerRow = 40;

    /// <summary>
    /// 保存ダイアログで選べるファイルの種類
    /// </summary>
    private const string Filter = "CSVファイル(*.csv)|*.csv|すべてのファイル(*.*)|*.*";

    /// <summary>
    /// 測定値の保存先
    /// </summary>
    private readonly ISensorDataStore _store;

    /// <summary>
    /// 保存ダイアログ
    /// </summary>
    private readonly IFileDialogService _fileDialog;

    /// <summary>
    /// ロガー
    /// </summary>
    private readonly ILogger<SensorDataExportService> _logger;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public SensorDataExportService(
        ISensorDataStore store,
        IFileDialogService fileDialog,
        ILogger<SensorDataExportService> logger)
    {
        _store = store;
        _fileDialog = fileDialog;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ExportResult> ExportAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        var records = await _store.ReadRangeAsync(fromUtc, toUtc, cancellationToken).ConfigureAwait(true);
        if (records.Count == 0)
        {
            return new ExportResult(Cancelled: false, FilePath: null, RowCount: 0);
        }

        var defaultName = string.Create(
            CultureInfo.InvariantCulture,
            $"sensor_data_{fromUtc.ToLocalTime():yyyyMMdd_HHmm}-{toUtc.ToLocalTime():yyyyMMdd_HHmm}.csv");

        var path = _fileDialog.AskSavePath("CSVに書き出す", defaultName, Filter);
        if (path is null)
        {
            return new ExportResult(Cancelled: true, FilePath: null, RowCount: 0);
        }

        var content = Build(records);

        await File.WriteAllTextAsync(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken)
            .ConfigureAwait(true);

        _logger.LogInformation("Exported {Count} rows to {Path}", records.Count, path);
        return new ExportResult(Cancelled: false, FilePath: path, RowCount: records.Count);
    }

    /// <summary>
    /// 測定値からCSVを組み立てる。
    /// </summary>
    internal static string Build(IReadOnlyList<SensorRecord> records)
    {
        var builder = new StringBuilder(capacity: CsvHeaderCapacity + (records.Count * EstimatedBytesPerRow));

        Csv.AppendRow(builder, "日時", "温度[℃]", "湿度[%RH]");

        foreach (var record in records)
        {
            Csv.AppendRow(
                builder,
                record.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Csv.Number(record.Temperature),
                Csv.Number(record.Humidity));
        }

        return builder.ToString();
    }
}
