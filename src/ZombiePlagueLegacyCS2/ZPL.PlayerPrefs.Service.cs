using System.Data;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace ZombiePlagueLegacyCS2;

/// <summary>
/// MySQL-backed persistence for per-player zombie class preferences.
///
/// Schema:
///   CREATE TABLE IF NOT EXISTS zpl_zombie_preferences (
///       steam_id   VARCHAR(32)  NOT NULL,
///       zombie_class VARCHAR(64) NOT NULL DEFAULT '',
///       PRIMARY KEY (steam_id)
///   ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
///
/// An empty <c>zombie_class</c> string means the player chose "random".
///
/// Thread model: all public methods are called synchronously on the game
/// thread (from Load, OnClientConnected, and ZPL_OnPreferenceChanged).
/// </summary>
public sealed class ZPLPlayerPrefsService(ISwiftlyCore core, ILogger<ZPLPlayerPrefsService> logger)
{
    private string _connectionName = "host";
    private bool   _ready;

    // ── Initialisation ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates the preferences table if absent and marks the service ready.
    /// Call once from <c>ZombiePlagueLegacyCS2.Load</c>.
    /// </summary>
    public void EnsureSchema(string connectionName)
    {
        _connectionName = connectionName;
        try
        {
            using var conn = Open();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS zpl_zombie_preferences (
                    steam_id    VARCHAR(32) NOT NULL,
                    zombie_class VARCHAR(64) NOT NULL DEFAULT '',
                    PRIMARY KEY (steam_id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                """;
            cmd.ExecuteNonQuery();
            _ready = true;
            logger.LogInformation("[ZPL-Prefs] Schema ready (connection='{Name}').", connectionName);
        }
        catch (Exception ex)
        {
            logger.LogError("[ZPL-Prefs] Failed to initialise database (connection='{Name}'): {Ex}", connectionName, ex.Message);
        }
    }

    // ── Bulk load ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads all stored zombie class preferences from the database and
    /// populates <see cref="PlayerZombieState.ExternalPreferences"/>.
    /// Call once after <see cref="EnsureSchema"/> so that connecting players
    /// already have their preference restored without an extra per-player query.
    /// </summary>
    public void LoadAll(PlayerZombieState zombieState)
    {
        if (!_ready) return;
        try
        {
            using var conn = Open();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = "SELECT steam_id, zombie_class FROM zpl_zombie_preferences";

            using var reader = cmd.ExecuteReader();
            int count = 0;
            while (reader.Read())
            {
                if (!ulong.TryParse(reader.GetString(0), out ulong sid)) continue;
                var cls = reader.GetString(1);
                if (!string.IsNullOrEmpty(cls))
                    zombieState.ExternalPreferences[sid] = cls;
                count++;
            }
            logger.LogInformation("[ZPL-Prefs] Loaded {Count} preference row(s) from database.", count);
        }
        catch (Exception ex)
        {
            logger.LogWarning("[ZPL-Prefs] LoadAll failed: {Ex}", ex.Message);
        }
    }

    // ── Single-player save ────────────────────────────────────────────────────

    /// <summary>
    /// Saves (or clears) the zombie class preference for one player.
    /// Intended as the handler for <see cref="IZombiePlagueLegacyAPI.ZPL_OnPreferenceChanged"/>.
    /// <paramref name="className"/> is null or empty when the player chose "random".
    /// </summary>
    public void OnPreferenceChanged(ulong steamId, string? className)
    {
        if (!_ready || steamId == 0) return;
        try
        {
            using var conn = Open();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO zpl_zombie_preferences (steam_id, zombie_class)
                VALUES (@sid, @cls)
                ON DUPLICATE KEY UPDATE zombie_class = VALUES(zombie_class)
                """;
            AddParam(cmd, "@sid", steamId.ToString());
            AddParam(cmd, "@cls", className ?? string.Empty);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            logger.LogWarning("[ZPL-Prefs] Save({SteamId}) failed: {Ex}", steamId, ex.Message);
        }
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private IDbConnection Open()
    {
        var conn = core.Database.GetConnection(_connectionName);
        conn.Open();
        return conn;
    }

    private static void AddParam(IDbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }
}
