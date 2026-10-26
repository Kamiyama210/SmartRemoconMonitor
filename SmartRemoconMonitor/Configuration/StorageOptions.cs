using System.ComponentModel.DataAnnotations;

namespace SmartRemoconMonitor.Configuration;

/// <summary>
/// 測定値の保存の設定
/// </summary>
public sealed class StorageOptions
{
    /// <summary>
    /// 設定のセクション名
    /// </summary>
    public const string SectionName = "Storage";

    /// <summary>
    /// 保持日数として受け付ける下限
    /// </summary>
    public const int MinRetentionDays = 1;

    /// <summary>
    /// 保持日数として受け付ける上限
    /// </summary>
    public const int MaxRetentionDays = 3650;

    /// <summary>
    /// 保持日数の既定値
    /// </summary>
    private const int DefaultRetentionDays = 30;

    /// <summary>
    /// SQLiteのデータベースファイル名の既定値
    /// </summary>
    private const string DefaultDatabaseFileName = "sensor_data.db";

    /// <summary>
    /// 間隔の下限（s）
    /// </summary>
    private const int MinIntervalSeconds = 1;

    /// <summary>
    /// 保存する最小の間隔の上限（s）
    /// </summary>
    private const int MaxMinimumWriteIntervalSeconds = 3600;

    /// <summary>
    /// 保存する最小の間隔の既定値（s）
    /// </summary>
    private const int DefaultMinimumWriteIntervalSeconds = 10;

    /// <summary>
    /// 書き込む間隔の上限（s）
    /// </summary>
    private const int MaxFlushIntervalSeconds = 600;

    /// <summary>
    /// 書き込む間隔の既定値（s）
    /// </summary>
    private const int DefaultFlushIntervalSeconds = 5;

    /// <summary>
    /// SQLiteのデータベースファイル名
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string DatabaseFileName { get; set; } = DefaultDatabaseFileName;

    /// <summary>
    /// 測定値を保存する最小の間隔（s）
    /// </summary>
    [Range(MinIntervalSeconds, MaxMinimumWriteIntervalSeconds)]
    public int MinimumWriteIntervalSeconds { get; set; } = DefaultMinimumWriteIntervalSeconds;

    /// <summary>
    /// 溜めた測定値をデータベースへ書き込む間隔（s）
    /// </summary>
    [Range(MinIntervalSeconds, MaxFlushIntervalSeconds)]
    public int FlushIntervalSeconds { get; set; } = DefaultFlushIntervalSeconds;

    /// <summary>
    /// 測定値の保持日数
    /// </summary>
    [Range(MinRetentionDays, MaxRetentionDays)]
    public int RetentionDays { get; set; } = DefaultRetentionDays;

    /// <summary>
    /// <see cref="MinimumWriteIntervalSeconds"/>を<see cref="TimeSpan"/>にしたもの
    /// </summary>
    public TimeSpan MinimumWriteInterval => TimeSpan.FromSeconds(MinimumWriteIntervalSeconds);

    /// <summary>
    /// <see cref="FlushIntervalSeconds"/>を<see cref="TimeSpan"/>にしたもの
    /// </summary>
    public TimeSpan FlushInterval => TimeSpan.FromSeconds(FlushIntervalSeconds);

    /// <summary>
    /// データベースファイルのフルパスを返す。
    /// </summary>
    public string ResolveDatabasePath() => AppPaths.InLocalAppData(DatabaseFileName);
}
