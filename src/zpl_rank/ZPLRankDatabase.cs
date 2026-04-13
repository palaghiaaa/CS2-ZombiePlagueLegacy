using System.Data;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace ZPLRank;

/// <summary>
/// Manages MySQL persistence for ZPL Rank stats via SwiftlyS2's
/// <c>Core.Database.GetConnection()</c> abstraction.
///
/// Schema (single table):
///   CREATE TABLE IF NOT EXISTS zpl_rank_stats (
///       steam_id   VARCHAR(32)  NOT NULL,
///       name       VARCHAR(128) NOT NULL DEFAULT '',
///       kills      INT          NOT NULL DEFAULT 0,
///       deaths     INT          NOT NULL DEFAULT 0,
///       infections INT          NOT NULL DEFAULT 0,
///       assists    INT          NOT NULL DEFAULT 0,
///       damage     BIGINT       NOT NULL DEFAULT 0,
///       PRIMARY KEY (steam_id)
///   ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
///
/// All reads and writes happen synchronously on the game-server thread.
/// The connection name references an entry in SwiftlyS2's
/// <c>configs/database.jsonc</c> (default: "host").
/// </summary>
public sealed class ZPLRankDatabase(ISwiftlyCore core, ILogger<ZPLRankDatabase> logger)
{
    private string _connectionName = "host";
    private bool _ready;

    // ── Initialisation ────────────────────────────────────────────────────────

    /// <summary>
    /// Connects to the named database, creates the stats table if absent,
    /// and marks the service as ready.
    /// Must be called once from <c>ZPLRankPlugin.Load</c>.
    /// </summary>
    public void EnsureSchema(string connectionName)
    {
        _connectionName = connectionName;
        try
        {
            using var conn = Open();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS zpl_rank_stats (
                    steam_id   VARCHAR(32)  NOT NULL,
                    name       VARCHAR(128) NOT NULL DEFAULT '',
                    kills      INT          NOT NULL DEFAULT 0,
                    deaths     INT          NOT NULL DEFAULT 0,
                    infections INT          NOT NULL DEFAULT 0,
                    assists    INT          NOT NULL DEFAULT 0,
                    damage     BIGINT       NOT NULL DEFAULT 0,
                    PRIMARY KEY (steam_id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                """;
            cmd.ExecuteNonQuery();
            _ready = true;
            logger.LogInformation("[ZPLRank-DB] Schema ready (connection='{Name}').", connectionName);
        }
        catch (Exception ex)
        {
            logger.LogError("[ZPLRank-DB] Failed to initialise database (connection='{Name}'): {Ex}", connectionName, ex.Message);
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
            cmd.CommandText =
                "SELECT name, kills, deaths, infections, assists, damage " +
                "FROM zpl_rank_stats WHERE steam_id = @sid LIMIT 1";
            AddParam(cmd, "@sid", steamId.ToString());

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
            logger.LogWarning("[ZPLRank-DB] Load({SteamId}) failed: {Ex}", steamId, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Loads every stored row into <paramref name="stats"/>.
    /// Called at plugin load so that the top list is populated immediately
    /// even before any player connects.
    /// </summary>
    public void LoadAll(Dictionary<ulong, ZPLRankPlugin.PlayerStat> stats)
    {
        if (!_ready) return;
        try
        {
            using var conn = Open();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText =
                "SELECT steam_id, name, kills, deaths, infections, assists, damage " +
                "FROM zpl_rank_stats";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (!ulong.TryParse(reader.GetString(0), out ulong sid)) continue;
                stats[sid] = new ZPLRankPlugin.PlayerStat
                {
                    Name       = reader.GetString(1),
                    Kills      = reader.GetInt32(2),
                    Deaths     = reader.GetInt32(3),
                    Infections = reader.GetInt32(4),
                    Assists    = reader.GetInt32(5),
                    Damage     = reader.GetInt64(6)
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("[ZPLRank-DB] LoadAll failed: {Ex}", ex.Message);
        }
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Upserts one player's stats.
    /// Called on player disconnect and on round end as a safety net.
    /// </summary>
    public void Save(ulong steamId, ZPLRankPlugin.PlayerStat stat)
    {
        if (!_ready) return;
        try
        {
            using var conn = Open();
            SaveOne(conn, steamId, stat);
        }
        catch (Exception ex)
        {
            logger.LogWarning("[ZPLRank-DB] Save({SteamId}) failed: {Ex}", steamId, ex.Message);
        }
    }

    /// <summary>
    /// Upserts all entries in <paramref name="stats"/> in one transaction.
    /// Called at plugin unload and on round end.
    /// </summary>
    public void SaveAll(Dictionary<ulong, ZPLRankPlugin.PlayerStat> stats)
    {
        if (!_ready || stats.Count == 0) return;
        try
        {
            using var conn = Open();
            using var tx   = conn.BeginTransaction();
            try
            {
                foreach (var (sid, stat) in stats)
                    SaveOne(conn, sid, stat, tx);
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("[ZPLRank-DB] SaveAll failed: {Ex}", ex.Message);
        }
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private IDbConnection Open()
    {
        var conn = core.Database.GetConnection(_connectionName);
        conn.Open();
        return conn;
    }

    private static void SaveOne(IDbConnection conn, ulong steamId, ZPLRankPlugin.PlayerStat stat, IDbTransaction? tx = null)
    {
        using var cmd = conn.CreateCommand();
        if (tx != null) cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO zpl_rank_stats (steam_id, name, kills, deaths, infections, assists, damage)
            VALUES (@sid, @name, @kills, @deaths, @infections, @assists, @damage)
            ON DUPLICATE KEY UPDATE
                name       = VALUES(name),
                kills      = VALUES(kills),
                deaths     = VALUES(deaths),
                infections = VALUES(infections),
                assists    = VALUES(assists),
                damage     = VALUES(damage)
            """;
        AddParam(cmd, "@sid",        steamId.ToString());
        AddParam(cmd, "@name",       stat.Name);
        AddParam(cmd, "@kills",      stat.Kills);
        AddParam(cmd, "@deaths",     stat.Deaths);
        AddParam(cmd, "@infections", stat.Infections);
        AddParam(cmd, "@assists",    stat.Assists);
        AddParam(cmd, "@damage",     stat.Damage);
        cmd.ExecuteNonQuery();
    }

    private static void AddParam(IDbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
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
