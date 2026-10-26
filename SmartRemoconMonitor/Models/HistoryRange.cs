namespace SmartRemoconMonitor.Models;

/// <summary>
/// CSV出力で選べる期間
/// </summary>
/// <param name="Label">画面に表示する名前</param>
/// <param name="Duration">現在からさかのぼる長さ</param>
public sealed record HistoryRange(string Label, TimeSpan Duration)
{
    /// <summary>
    /// 画面に並べる期間の選択肢
    /// </summary>
    public static IReadOnlyList<HistoryRange> Defaults { get; } =
    [
        new("直近1時間", TimeSpan.FromHours(1)),
        new("直近24時間", TimeSpan.FromHours(24)),
        new("直近7日", TimeSpan.FromDays(7)),
    ];
}
