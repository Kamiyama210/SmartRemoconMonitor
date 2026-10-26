namespace SmartRemoconMonitor.Services.Storage;

/// <summary>
/// 測定値の保存先
/// </summary>
public interface ISensorDataStore
{
    /// <summary>
    /// 測定値の保存を依頼する。
    /// </summary>
    bool Enqueue(SensorRecord record);

    /// <summary>
    /// 指定した期間の測定値を、時刻の古い順に返す。
    /// </summary>
    Task<IReadOnlyList<SensorRecord>> ReadRangeAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 保存された測定値
/// </summary>
public readonly record struct SensorRecord(DateTimeOffset Timestamp, double Temperature, double Humidity);
