using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace ZORank;

/// <summary>
/// Manages SQLite persistence for ZO Rank stats.
///
/// Schema (single table):
///   CREATE TABLE IF NOT EXISTS zo_rank_stats (
///       steam_id   TEXT    PRIMARY KEY,
///       name       TEXT    NOT NULL,
///       kills      INTEGER NOT NULL DEFAULT 0,
///       deaths     INTEGER NOT NULL DEFAULT 0,
///       infections INTEGER NOT NULL DEFAULT 0,
///       assists    INTEGER NOT NULL DEFAULT 0,
///       damage     INTEGER NOT NULL DEFAULT 0
///   );
///
/// All reads and writes happen synchronously on the game-server thread
/// (SwiftlyS2 does not expose a background-thread scheduler in a safe way).
/// SQLite WAL mode is enabled to reduce lock contention when many players
/// connect / disconnect in quick succession.
/// </summary>
public sealed class ZORankDatabase(ILogger<ZORankDatabase> logger)
{
    private string? _connectionString;
    private bool _ready;

    // ── Initialisation ────────────────────────────────────────────────────────

    /// <summary>
    /// Opens (or creates) the SQLite database at <paramref name="dbPath"/>,
    /// enables WAL journal mode, and ensures the stats table exists.
    /// Must be called once from <c>ZORankPlugin.Load</c>.
    /// </summary>
    public void EnsureSchema(string dbPath)
    {
        try
        {
            // Create the directory if needed.
            string? dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode       = SqliteOpenMode.ReadWriteCreate
            }.ToString();

            using var conn = Open();
            using var cmd  = conn.CreateCommand();

            // WAL mode: writers don't block readers.
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            cmd.ExecuteNonQuery();

            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS zo_rank_stats (
                    steam_id   TEXT    PRIMARY KEY,
                    name       TEXT    NOT NULL DEFAULT '',
                    kills      INTEGER NOT NULL DEFAULT 0,
                    deaths     INTEGER NOT NULL DEFAULT 0,
                    infections INTEGER NOT NULL DEFAULT 0,
                    assists    INTEGER NOT NULL DEFAULT 0,
                    damage     INTEGER NOT NULL DEFAULT 0
                );
                """;
            // Note: name defaults to '' so rows inserted from batch-load don't fail
            // if the player hasn't been seen online yet.  The name is always
            // overwritten with the live value on the next connect/stat update.
            cmd.ExecuteNonQuery();

            _ready = true;
            logger.LogInformation("[ZORank-DB] SQLite database ready: {Path}", dbPath);
        }
        catch (Exception ex)
        {
            logger.LogError("[ZORank-DB] Failed to initialise database at '{Path}': {Ex}", dbPath, ex.Message);
        }
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the stat row for <paramref name="steamId"/> from the database.
    /// Returns <c>null</c> when the player has no stored row yet.
    /// </summary>
    public StoredStat? Load(ulong steamId)
    {
        if (!_ready) return null;
        try
        {
            using var conn = Open();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = """
                SELECT name, kills, deaths, infections, assists, damage
                FROM zo_rank_stats
                WHERE steam_id = $sid
                LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("$sid", steamId.ToString());

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            return new StoredStat
            {
                Name       = reader.GetString(0),
                Kills      = reader.GetInt32(1),
                Deaths     = reader.GetInt32(2),
                Infections = reader.GetInt32(3),
                Assists    = reader.GetInt32(4),
                Damage     = reader.GetInt64(5)
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning("[ZORank-DB] Load({SteamId}) failed: {Ex}", steamId, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Loads every stored row into <paramref name="stats"/>.
    /// Called at plugin load so that the top list is populated immediately
    /// even before any player connects.
    /// </summary>
    public void LoadAll(Dictionary<ulong, ZORankPlugin.PlayerStat> stats)
    {
        if (!_ready) return;
        try
        {
            using var conn = Open();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = """
                SELECT steam_id, name, kills, deaths, infections, assists, damage
                FROM zo_rank_stats;
                """;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (!ulong.TryParse(reader.GetString(0), out ulong sid)) continue;
                var s = new ZORankPlugin.PlayerStat
                {
                    Name       = reader.GetString(1),
                    Kills      = reader.GetInt32(2),
                    Deaths     = reader.GetInt32(3),
                    Infections = reader.GetInt32(4),
                    Assists    = reader.GetInt32(5),
                    Damage     = reader.GetInt64(6)
                };
                stats[sid] = s;
            }

            logger.LogInformation("[ZORank-DB] Loaded {Count} player records.", stats.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning("[ZORank-DB] LoadAll failed: {Ex}", ex.Message);
        }
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Upserts (INSERT OR REPLACE) one player's stats.
    /// Called on player disconnect and on round end as a safety net.
    /// </summary>
    public void Save(ulong steamId, ZORankPlugin.PlayerStat stat)
    {
        if (!_ready) return;
        try
        {
            using var conn = Open();
            SaveOne(conn, steamId, stat);
        }
        catch (Exception ex)
        {
            logger.LogWarning("[ZORank-DB] Save({SteamId}) failed: {Ex}", steamId, ex.Message);
        }
    }

    /// <summary>
    /// Upserts all entries in <paramref name="stats"/> in one transaction.
    /// Called at plugin unload and on round end.
    /// </summary>
    public void SaveAll(Dictionary<ulong, ZORankPlugin.PlayerStat> stats)
    {
        if (!_ready || stats.Count == 0) return;
        try
        {
            using var conn = Open();
            using var tx   = conn.BeginTransaction();
            foreach (var (sid, stat) in stats)
                SaveOne(conn, sid, stat);
            tx.Commit();
        }
        catch (Exception ex)
        {
            logger.LogWarning("[ZORank-DB] SaveAll failed: {Ex}", ex.Message);
        }
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private static void SaveOne(SqliteConnection conn, ulong steamId, ZORankPlugin.PlayerStat stat)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO zo_rank_stats (steam_id, name, kills, deaths, infections, assists, damage)
            VALUES ($sid, $name, $kills, $deaths, $infections, $assists, $damage)
            ON CONFLICT(steam_id) DO UPDATE SET
                name       = excluded.name,
                kills      = excluded.kills,
                deaths     = excluded.deaths,
                infections = excluded.infections,
                assists    = excluded.assists,
                damage     = excluded.damage;
            """;
        cmd.Parameters.AddWithValue("$sid",        steamId.ToString());
        cmd.Parameters.AddWithValue("$name",       stat.Name);
        cmd.Parameters.AddWithValue("$kills",      stat.Kills);
        cmd.Parameters.AddWithValue("$deaths",     stat.Deaths);
        cmd.Parameters.AddWithValue("$infections", stat.Infections);
        cmd.Parameters.AddWithValue("$assists",    stat.Assists);
        cmd.Parameters.AddWithValue("$damage",     stat.Damage);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Row returned by <see cref="Load"/>.</summary>
    public sealed class StoredStat
    {
        public string Name       { get; init; } = string.Empty;
        public int    Kills      { get; init; }
        public int    Deaths     { get; init; }
        public int    Infections { get; init; }
        public int    Assists    { get; init; }
        public long   Damage     { get; init; }
    }
}
