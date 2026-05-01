using System.Text.Json;
using Microsoft.Data.Sqlite;
using WinPerfMon.Shared.Models;

namespace WinPerfMon.Storage;

/// <summary>
/// SQLite-backed ring buffer for all metric snapshots.
/// Thread-safe for concurrent reads and writes via WAL mode.
/// </summary>
public sealed class MetricStore : IDisposable
{
    private readonly string _connectionString;
    private readonly TimeSpan _retention;

    public MetricStore(string dbPath, TimeSpan? retention = null)
    {
        _retention = retention ?? TimeSpan.FromDays(7);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();

        Initialize();
    }

    private void Initialize()
    {
        using var conn = Open();
        conn.ExecuteNonQuery("PRAGMA journal_mode=WAL;");
        conn.ExecuteNonQuery("PRAGMA synchronous=NORMAL;");
        conn.ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS cpu_snapshots (
                id        INTEGER PRIMARY KEY,
                ts        INTEGER NOT NULL,
                payload   TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_cpu_ts ON cpu_snapshots(ts);

            CREATE TABLE IF NOT EXISTS gpu_snapshots (
                id        INTEGER PRIMARY KEY,
                ts        INTEGER NOT NULL,
                payload   TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_gpu_ts ON gpu_snapshots(ts);

            CREATE TABLE IF NOT EXISTS network_snapshots (
                id        INTEGER PRIMARY KEY,
                ts        INTEGER not null,
                payload   TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_net_ts ON network_snapshots(ts);

            CREATE TABLE IF NOT EXISTS storage_snapshots (
                id        INTEGER PRIMARY KEY,
                ts        INTEGER NOT NULL,
                payload   TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_stor_ts ON storage_snapshots(ts);
            """);
    }

    public void WriteCpu(CpuMetrics m) => Write("cpu_snapshots", m.Timestamp, m);
    public void WriteGpu(GpuMetrics m) => Write("gpu_snapshots", m.Timestamp, m);
    public void WriteNetwork(NetworkMetrics m) => Write("network_snapshots", m.Timestamp, m);
    public void WriteStorage(StorageMetrics m) => Write("storage_snapshots", m.Timestamp, m);

    public IReadOnlyList<CpuMetrics> ReadCpu(DateTimeOffset from, DateTimeOffset to)
        => Read<CpuMetrics>("cpu_snapshots", from, to);

    public IReadOnlyList<GpuMetrics> ReadGpu(DateTimeOffset from, DateTimeOffset to)
        => Read<GpuMetrics>("gpu_snapshots", from, to);

    public IReadOnlyList<NetworkMetrics> ReadNetwork(DateTimeOffset from, DateTimeOffset to)
        => Read<NetworkMetrics>("network_snapshots", from, to);

    public IReadOnlyList<StorageMetrics> ReadStorage(DateTimeOffset from, DateTimeOffset to)
        => Read<StorageMetrics>("storage_snapshots", from, to);

    public void Prune()
    {
        long cutoff = DateTimeOffset.UtcNow.Subtract(_retention).ToUnixTimeSeconds();
        using var conn = Open();
        foreach (var table in new[] { "cpu_snapshots", "gpu_snapshots", "network_snapshots", "storage_snapshots" })
            conn.ExecuteNonQuery($"DELETE FROM {table} WHERE ts < {cutoff};");
    }

    private void Write<T>(string table, DateTimeOffset ts, T value)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO {table} (ts, payload) VALUES ($ts, $payload);";
        cmd.Parameters.AddWithValue("$ts", ts.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(value));
        cmd.ExecuteNonQuery();
    }

    private IReadOnlyList<T> Read<T>(string table, DateTimeOffset from, DateTimeOffset to)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT payload FROM {table} WHERE ts BETWEEN $from AND $to ORDER BY ts;";
        cmd.Parameters.AddWithValue("$from", from.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$to", to.ToUnixTimeSeconds());

        var results = new List<T>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var obj = JsonSerializer.Deserialize<T>(reader.GetString(0));
            if (obj is not null) results.Add(obj);
        }
        return results;
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public void Dispose() { }
}

internal static class SqliteConnectionExtensions
{
    internal static void ExecuteNonQuery(this SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
