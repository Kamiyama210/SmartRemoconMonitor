using System.Globalization;
using SmartRemoconMonitor.Services.Storage;

namespace SmartRemoconMonitor.Models;

/// <summary>
/// グラフの折れ線1本分。点の並びと、縦軸の下限・上限を持つ。
/// </summary>
/// <param name="Points">0.0〜1.0に正規化した点の並び</param>
/// <param name="Minimum">縦軸の下限</param>
/// <param name="Maximum">縦軸の上限</param>
public sealed record TrendSeries(
    IReadOnlyList<TrendPoint> Points,
    double Minimum,
    double Maximum)
{
    /// <summary>
    /// 点の数の上限の既定値
    /// </summary>
    private const int DefaultMaxPoints = 500;

    /// <summary>
    /// 線を引くのに必要な点の数
    /// </summary>
    private const int MinimumPointsForLine = 2;

    /// <summary>
    /// 上限と下限の差がこれより小さければ、値が一定とみなす
    /// </summary>
    private const double FlatValueEpsilon = 0.001;

    /// <summary>
    /// 値が一定のとき、縦軸の上下に足す余白
    /// </summary>
    private const double FlatValuePadding = 0.5;

    /// <summary>
    /// 描画できる点が無い状態
    /// </summary>
    public static TrendSeries Empty { get; } = new([], 0, 0);

    /// <summary>
    /// 線を引けるだけの点があるか
    /// </summary>
    public bool HasData => Points.Count >= MinimumPointsForLine;

    /// <summary>
    /// 縦軸の下限の表示用文字列
    /// </summary>
    public string MinimumLabel => Format(Minimum);

    /// <summary>
    /// 縦軸の上限の表示用文字列
    /// </summary>
    public string MaximumLabel => Format(Maximum);

    /// <summary>
    /// 保存された測定値から、指定した項目の折れ線を作る。
    /// </summary>
    public static TrendSeries Create(
        IReadOnlyList<SensorRecord> records,
        Func<SensorRecord, double> selector,
        int maxPoints = DefaultMaxPoints)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPoints, MinimumPointsForLine);

        if (records.Count < MinimumPointsForLine)
        {
            return Empty;
        }

        var sampled = Downsample(records, selector, maxPoints);

        var minimum = double.MaxValue;
        var maximum = double.MinValue;
        foreach (var (_, value) in sampled)
        {
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
        }

        if (Math.Abs(maximum - minimum) < FlatValueEpsilon)
        {
            minimum -= FlatValuePadding;
            maximum += FlatValuePadding;
        }

        var startTicks = sampled[0].TimestampTicks;
        var span = (double)(sampled[^1].TimestampTicks - startTicks);
        var valueSpan = maximum - minimum;

        var points = new TrendPoint[sampled.Count];
        for (var i = 0; i < sampled.Count; i++)
        {
            var x = span > 0 ? (sampled[i].TimestampTicks - startTicks) / span : 0;
            var y = (sampled[i].Value - minimum) / valueSpan;
            points[i] = new TrendPoint(x, y);
        }

        return new TrendSeries(points, minimum, maximum);
    }

    /// <summary>
    /// 点の数を上限以下に間引く。区間ごとの平均値を1点にする。
    /// </summary>
    private static List<(long TimestampTicks, double Value)> Downsample(
        IReadOnlyList<SensorRecord> records,
        Func<SensorRecord, double> selector,
        int maxPoints)
    {
        if (records.Count <= maxPoints)
        {
            var all = new List<(long, double)>(records.Count);
            foreach (var record in records)
            {
                all.Add((record.Timestamp.UtcTicks, selector(record)));
            }

            return all;
        }

        var result = new List<(long, double)>(maxPoints);
        var bucketSize = (double)records.Count / maxPoints;

        for (var bucket = 0; bucket < maxPoints; bucket++)
        {
            var start = (int)(bucket * bucketSize);
            var end = Math.Min((int)((bucket + 1) * bucketSize), records.Count);
            if (start >= end)
            {
                continue;
            }

            double sum = 0;
            for (var i = start; i < end; i++)
            {
                sum += selector(records[i]);
            }

            var timestamp = records[start + ((end - start) / 2)].Timestamp.UtcTicks;
            result.Add((timestamp, sum / (end - start)));
        }

        return result;
    }

    /// <summary>
    /// 目盛の値を、小数点以下1桁の文字列にする。
    /// </summary>
    private static string Format(double value) => value.ToString("0.0", CultureInfo.CurrentCulture);
}

/// <summary>
/// グラフ上の1点
/// </summary>
public readonly record struct TrendPoint(double X, double Y);
