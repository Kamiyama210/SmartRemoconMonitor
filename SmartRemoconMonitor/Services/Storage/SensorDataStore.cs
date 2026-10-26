using System.IO;
using System.Globalization;
using System.Threading.Channels;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartRemoconMonitor.Configuration;

namespace SmartRemoconMonitor.Services.Storage;

/// <summary>
/// 測定値をSQLiteに保存する。
/// </summary>
public sealed class SensorDataStore : BackgroundService, ISensorDataStore
{
    /// <summary>
    /// 書き込み待ちとして溜めておける測定値の件数
    /// </summary>
    private const int BufferCapacity = 10_000;

    /// <summary>
    /// 保存の設定
    /// </summary>
    private readonly StorageOptions _options;

    /// <summary>
    /// ロガー
    /// </summary>
    private readonly ILogger<SensorDataStore> _logger;

    /// <summary>
    /// SQLiteへの接続文字列
    /// </summary>
    private readonly string _connectionString;

    /// <summary>
    /// 書き込み待ちの測定値
    /// </summary>
    private readonly Channel<SensorRecord> _buffer;

    /// <summary>
    /// バッファから取り出したが、まだ書き込めていない測定値
    /// </summary>
    private readonly List<SensorRecord> _pending = [];

    /// <summary>
    /// テーブルの用意が済んだか
    /// </summary>
    private bool _schemaReady;

    /// <summary>
    /// データベースの操作に失敗し続けているか
    /// </summary>
    private bool _failing;

    /// <summary>
    /// 保存先のフォルダを作り、接続文字列と書き込み待ちのバッファを用意する。
    /// </summary>
    public SensorDataStore(IOptions<StorageOptions> options, ILogger<SensorDataStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _logger = logger;

        var path = _options.ResolveDatabasePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();

        _buffer = Channel.CreateBounded<SensorRecord>(new BoundedChannelOptions(BufferCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
    }

    /// <inheritdoc/>
    public bool Enqueue(SensorRecord record) => _buffer.Writer.TryWrite(record);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SensorRecord>> ReadRangeAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();

        command.CommandText = """
            SELECT timestamp_ms, temperature, humidity
            FROM sensor_data
            WHERE timestamp_ms BETWEEN $from AND $to
            ORDER BY timestamp_ms
            """;
        command.Parameters.AddWithValue("$from", fromUtc.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$to", toUtc.ToUnixTimeMilliseconds());

        var results = new List<SensorRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new SensorRecord(
                DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(0)),
                reader.GetDouble(1),
                reader.GetDouble(2)));
        }

        return results;
    }

    /// <summary>
    /// テーブルを用意し、溜まった測定値を一定間隔で書き込む。古い測定値の削除は1日1回行う。
    /// テーブルの用意や書き込みに失敗したら、次の間隔で再試行する。
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lastCleanup = DateTimeOffset.UtcNow;
        using var timer = new PeriodicTimer(_options.FlushInterval);

        try
        {
            await TryInitializeAsync(stoppingToken).ConfigureAwait(false);

            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                if (!_schemaReady && !await TryInitializeAsync(stoppingToken).ConfigureAwait(false))
                {
                    continue;
                }

                await TryFlushAsync(stoppingToken).ConfigureAwait(false);

                if (DateTimeOffset.UtcNow - lastCleanup > TimeSpan.FromDays(1))
                {
                    lastCleanup = DateTimeOffset.UtcNow;
                    await TryDeleteExpiredAsync(stoppingToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        if (_schemaReady || await TryInitializeAsync(CancellationToken.None).ConfigureAwait(false))
        {
            await TryFlushAsync(CancellationToken.None).ConfigureAwait(false);
        }

        if (_pending.Count > 0 || _buffer.Reader.TryPeek(out _))
        {
            _logger.LogWarning("{Count} sensor records were not persisted on shutdown.", _pending.Count + _buffer.Reader.Count);
        }
    }

    /// <summary>
    /// テーブルを用意し、古い測定値を削除する。失敗したらログを出してfalseを返す。
    /// </summary>
    private async Task<bool> TryInitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await InitializeSchemaAsync(cancellationToken).ConfigureAwait(false);
            _schemaReady = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ReportFailure(ex, "initialization");
            return false;
        }

        await TryDeleteExpiredAsync(cancellationToken).ConfigureAwait(false);
        ReportRecovery();
        return true;
    }

    /// <summary>
    /// 溜まった測定値を書き込む。失敗したらログを出し、書き込めなかった測定値は次回に持ち越す。
    /// </summary>
    private async Task TryFlushAsync(CancellationToken cancellationToken)
    {
        try
        {
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            ReportRecovery();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ReportFailure(ex, "flush");
        }
    }

    /// <summary>
    /// 古い測定値を削除する。失敗したらログを出すだけで例外は投げない。
    /// </summary>
    private async Task TryDeleteExpiredAsync(CancellationToken cancellationToken)
    {
        try
        {
            await DeleteExpiredAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to delete expired sensor records.");
        }
    }

    /// <summary>
    /// 失敗をログに出す。続けて失敗している間は、2回目以降をDebugにする。
    /// </summary>
    private void ReportFailure(Exception ex, string operation)
    {
        if (_failing)
        {
            _logger.LogDebug(ex, "Sensor database {Operation} failed again. Will retry.", operation);
            return;
        }

        _failing = true;
        _logger.LogError(ex, "Sensor database {Operation} failed. Will retry.", operation);
    }

    /// <summary>
    /// 失敗が続いていた状態から回復したら、ログに出す。
    /// </summary>
    private void ReportRecovery()
    {
        if (!_failing)
        {
            return;
        }

        _failing = false;
        _logger.LogInformation("Sensor database recovered.");
    }

    /// <summary>
    /// テーブルとインデックスが無ければ作り、WALモードにする。
    /// </summary>
    private async Task InitializeSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();

        command.CommandText = """
            PRAGMA journal_mode=WAL;

            CREATE TABLE IF NOT EXISTS sensor_data (
                timestamp_ms INTEGER NOT NULL,
                temperature  REAL    NOT NULL,
                humidity     REAL    NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_sensor_data_timestamp
                ON sensor_data (timestamp_ms);
            """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Sensor database ready at {Path}", _options.ResolveDatabasePath());
    }

    /// <summary>
    /// 溜まった測定値を、1つのトランザクションでまとめて書き込む。成功したときだけ持ち越し分を空にする。
    /// </summary>
    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        while (_buffer.Reader.TryRead(out var record))
        {
            _pending.Add(record);
        }

        if (_pending.Count == 0)
        {
            return;
        }

        if (_pending.Count > BufferCapacity)
        {
            var dropped = _pending.Count - BufferCapacity;
            _pending.RemoveRange(0, dropped);
            _logger.LogWarning("Dropped {Count} oldest unsaved sensor records.", dropped);
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            INSERT INTO sensor_data (timestamp_ms, temperature, humidity)
            VALUES ($ts, $temp, $hum)
            """;

        var timestamp = command.CreateParameter();
        timestamp.ParameterName = "$ts";
        var temperature = command.CreateParameter();
        temperature.ParameterName = "$temp";
        var humidity = command.CreateParameter();
        humidity.ParameterName = "$hum";
        command.Parameters.AddRange(new[] { timestamp, temperature, humidity });

        foreach (var record in _pending)
        {
            timestamp.Value = record.Timestamp.ToUnixTimeMilliseconds();
            temperature.Value = record.Temperature;
            humidity.Value = record.Humidity;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Persisted {Count} sensor records.", _pending.Count);
        _pending.Clear();
    }

    /// <summary>
    /// 保持日数より古い測定値を削除する。
    /// </summary>
    private async Task DeleteExpiredAsync(CancellationToken cancellationToken)
    {
        var retentionDays = _options.RetentionDays;
        var threshold = DateTimeOffset.UtcNow.AddDays(-retentionDays).ToUnixTimeMilliseconds();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM sensor_data WHERE timestamp_ms < $threshold";
        command.Parameters.AddWithValue("$threshold", threshold);

        var deleted = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (deleted > 0)
        {
            _logger.LogInformation(
                "Deleted {Count} sensor records older than {Days} days.",
                deleted,
                retentionDays.ToString(CultureInfo.InvariantCulture));
        }
    }
}
