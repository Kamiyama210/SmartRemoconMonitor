namespace SmartRemoconMonitor.Services.Export;

/// <summary>
/// CSV出力結果
/// </summary>
/// <param name="Cancelled">ユーザーが保存先の選択を取り消したか。</param>
/// <param name="FilePath">保存したファイルのパス。保存しなかった場合はnull。</param>
/// <param name="RowCount">書き出した行数</param>
public readonly record struct ExportResult(bool Cancelled, string? FilePath, int RowCount);

/// <summary>
/// 保存された測定値をCSVに書き出す。
/// </summary>
public interface ISensorDataExportService
{
    /// <summary>
    /// 指定した期間の測定値を、ユーザーが選んだファイルへ書き出す。
    /// </summary>
    Task<ExportResult> ExportAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);
}
