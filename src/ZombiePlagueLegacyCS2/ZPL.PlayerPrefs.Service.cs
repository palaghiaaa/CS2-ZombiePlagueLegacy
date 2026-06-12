using System.Data;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace ZombiePlagueLegacyCS2;

/// <summary>
/// MySQL-backed persistence for per-player preferences.
///
/// Schema:
///   CREATE TABLE IF NOT EXISTS zpl_user_preferences (
///       steam_id                    VARCHAR(32)  NOT NULL,
///       zombie_class                VARCHAR(64)  NOT NULL DEFAULT '',
///       weapon_remember             TINYINT(1)   NOT NULL DEFAULT 0,
///       primary_weapon_classname    VARCHAR(64)  NOT NULL DEFAULT '',
///       primary_weapon_name         VARCHAR(64)  NOT NULL DEFAULT '',
///       secondary_weapon_classname  VARCHAR(64)  NOT NULL DEFAULT '',
///       secondary_weapon_name       VARCHAR(64)  NOT NULL DEFAULT '',
///       vox_sounds                  TINYINT(1)   NOT NULL DEFAULT 1,
///       fog_enabled                 TINYINT(1)   NOT NULL DEFAULT 1,
///       flashlight_enabled          TINYINT(1)   NOT NULL DEFAULT 1,
///       tags_enabled                TINYINT(1)   NOT NULL DEFAULT 1,
///       ads_enabled                 TINYINT(1)   NOT NULL DEFAULT 1,
///       hide_players                TINYINT(1)   NOT NULL DEFAULT 0,
///       vip_reward_messages         TINYINT(1)   NOT NULL DEFAULT 1,
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
    /// Creates or updates the preferences table and marks the service ready.
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
                CREATE TABLE IF NOT EXISTS zpl_user_preferences (
                    steam_id                   VARCHAR(32) NOT NULL,
                    zombie_class               VARCHAR(64) NOT NULL DEFAULT '',
                    weapon_remember            TINYINT(1) NOT NULL DEFAULT 0,
                    primary_weapon_classname   VARCHAR(64) NOT NULL DEFAULT '',
                    primary_weapon_name        VARCHAR(64) NOT NULL DEFAULT '',
                    secondary_weapon_classname VARCHAR(64) NOT NULL DEFAULT '',
                    secondary_weapon_name      VARCHAR(64) NOT NULL DEFAULT '',
                    vox_sounds                 TINYINT(1) NOT NULL DEFAULT 1,
                    fog_enabled                TINYINT(1) NOT NULL DEFAULT 1,
                    flashlight_enabled         TINYINT(1) NOT NULL DEFAULT 1,
                    tags_enabled               TINYINT(1) NOT NULL DEFAULT 1,
                    ads_enabled                TINYINT(1) NOT NULL DEFAULT 1,
                    hide_players               TINYINT(1) NOT NULL DEFAULT 0,
                    vip_reward_messages        TINYINT(1) NOT NULL DEFAULT 1,
                    PRIMARY KEY (steam_id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                """;
            cmd.ExecuteNonQuery();

            EnsurePreferenceColumns(conn);

            bool migratedOldTable = false;
            cmd.CommandText = """
                INSERT INTO zpl_user_preferences (steam_id, zombie_class)
                SELECT steam_id, zombie_class
                FROM zpl_zombie_preferences
                ON DUPLICATE KEY UPDATE zombie_class = VALUES(zombie_class);
                """;
            try
            {
                cmd.ExecuteNonQuery();
                migratedOldTable = true;
            }
            catch
            {
                // Fresh installs do not have the old zombie-only table.
            }

            if (migratedOldTable)
            {
                cmd.CommandText = "DROP TABLE IF EXISTS zpl_zombie_preferences";
                cmd.ExecuteNonQuery();
            }

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
    public void LoadAll(PlayerZombieState zombieState, ZPLGlobals globals)
    {
        if (!_ready) return;
        try
        {
            using var conn = Open();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = """
                SELECT steam_id,
                       zombie_class,
                       weapon_remember,
                       primary_weapon_classname,
                       primary_weapon_name,
                       secondary_weapon_classname,
                       secondary_weapon_name,
                       vox_sounds,
                       fog_enabled,
                       flashlight_enabled,
                       tags_enabled,
                       ads_enabled,
                       hide_players,
                       vip_reward_messages
                FROM zpl_user_preferences
                """;

            using var reader = cmd.ExecuteReader();
            int count = 0;
            while (reader.Read())
            {
                if (!ulong.TryParse(reader.GetString(0), out ulong sid)) continue;
                var cls = reader.GetString(1);
                if (!string.IsNullOrEmpty(cls))
                    zombieState.ExternalPreferences[sid] = cls;

                var weaponPreference = new WeaponLoadoutPreference
                {
                    RememberChoice = Convert.ToBoolean(reader.GetValue(2)),
                    PrimaryClassname = reader.GetString(3),
                    PrimaryName = reader.GetString(4),
                    SecondaryClassname = reader.GetString(5),
                    SecondaryName = reader.GetString(6)
                };

                if (weaponPreference.RememberChoice ||
                    !string.IsNullOrWhiteSpace(weaponPreference.PrimaryClassname) ||
                    !string.IsNullOrWhiteSpace(weaponPreference.SecondaryClassname))
                    globals.WeaponLoadoutPreferences[sid] = weaponPreference;

                globals.UserPreferences[sid] = new UserPreferenceSettings
                {
                    VoxSounds = Convert.ToBoolean(reader.GetValue(7)),
                    Fog = Convert.ToBoolean(reader.GetValue(8)),
                    Flashlight = Convert.ToBoolean(reader.GetValue(9)),
                    Tags = Convert.ToBoolean(reader.GetValue(10)),
                    Ads = Convert.ToBoolean(reader.GetValue(11)),
                    HidePlayers = Convert.ToBoolean(reader.GetValue(12)),
                    VipRewardMessages = Convert.ToBoolean(reader.GetValue(13))
                };

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
                INSERT INTO zpl_user_preferences (steam_id, zombie_class)
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

    public void SaveWeaponPreference(ulong steamId, WeaponLoadoutPreference preference)
    {
        if (!_ready || steamId == 0) return;
        try
        {
            using var conn = Open();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO zpl_user_preferences (
                    steam_id,
                    weapon_remember,
                    primary_weapon_classname,
                    primary_weapon_name,
                    secondary_weapon_classname,
                    secondary_weapon_name
                )
                VALUES (@sid, @remember, @primaryClass, @primaryName, @secondaryClass, @secondaryName)
                ON DUPLICATE KEY UPDATE
                    weapon_remember = VALUES(weapon_remember),
                    primary_weapon_classname = VALUES(primary_weapon_classname),
                    primary_weapon_name = VALUES(primary_weapon_name),
                    secondary_weapon_classname = VALUES(secondary_weapon_classname),
                    secondary_weapon_name = VALUES(secondary_weapon_name)
                """;
            AddParam(cmd, "@sid", steamId.ToString());
            AddParam(cmd, "@remember", preference.RememberChoice ? 1 : 0);
            AddParam(cmd, "@primaryClass", preference.PrimaryClassname ?? string.Empty);
            AddParam(cmd, "@primaryName", preference.PrimaryName ?? string.Empty);
            AddParam(cmd, "@secondaryClass", preference.SecondaryClassname ?? string.Empty);
            AddParam(cmd, "@secondaryName", preference.SecondaryName ?? string.Empty);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            logger.LogWarning("[ZPL-Prefs] SaveWeapon({SteamId}) failed: {Ex}", steamId, ex.Message);
        }
    }

    public void SaveUserPreferences(ulong steamId, UserPreferenceSettings preference)
    {
        if (!_ready || steamId == 0) return;
        try
        {
            using var conn = Open();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO zpl_user_preferences (
                    steam_id,
                    vox_sounds,
                    fog_enabled,
                    flashlight_enabled,
                    tags_enabled,
                    ads_enabled,
                    hide_players,
                    vip_reward_messages
                )
                VALUES (@sid, @vox, @fog, @flashlight, @tags, @ads, @hidePlayers, @vipRewards)
                ON DUPLICATE KEY UPDATE
                    vox_sounds = VALUES(vox_sounds),
                    fog_enabled = VALUES(fog_enabled),
                    flashlight_enabled = VALUES(flashlight_enabled),
                    tags_enabled = VALUES(tags_enabled),
                    ads_enabled = VALUES(ads_enabled),
                    hide_players = VALUES(hide_players),
                    vip_reward_messages = VALUES(vip_reward_messages)
                """;
            AddParam(cmd, "@sid", steamId.ToString());
            AddParam(cmd, "@vox", preference.VoxSounds ? 1 : 0);
            AddParam(cmd, "@fog", preference.Fog ? 1 : 0);
            AddParam(cmd, "@flashlight", preference.Flashlight ? 1 : 0);
            AddParam(cmd, "@tags", preference.Tags ? 1 : 0);
            AddParam(cmd, "@ads", preference.Ads ? 1 : 0);
            AddParam(cmd, "@hidePlayers", preference.HidePlayers ? 1 : 0);
            AddParam(cmd, "@vipRewards", preference.VipRewardMessages ? 1 : 0);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            logger.LogWarning("[ZPL-Prefs] SaveUserPreferences({SteamId}) failed: {Ex}", steamId, ex.Message);
        }
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private IDbConnection Open()
    {
        var conn = core.Database.GetConnection(_connectionName);
        conn.Open();
        return conn;
    }

    private static void EnsurePreferenceColumns(IDbConnection conn)
    {
        EnsureColumn(conn, "zombie_class", "`zombie_class` VARCHAR(64) NOT NULL DEFAULT ''");
        EnsureColumn(conn, "weapon_remember", "`weapon_remember` TINYINT(1) NOT NULL DEFAULT 0");
        EnsureColumn(conn, "primary_weapon_classname", "`primary_weapon_classname` VARCHAR(64) NOT NULL DEFAULT ''");
        EnsureColumn(conn, "primary_weapon_name", "`primary_weapon_name` VARCHAR(64) NOT NULL DEFAULT ''");
        EnsureColumn(conn, "secondary_weapon_classname", "`secondary_weapon_classname` VARCHAR(64) NOT NULL DEFAULT ''");
        EnsureColumn(conn, "secondary_weapon_name", "`secondary_weapon_name` VARCHAR(64) NOT NULL DEFAULT ''");
        EnsureColumn(conn, "vox_sounds", "`vox_sounds` TINYINT(1) NOT NULL DEFAULT 1");
        EnsureColumn(conn, "fog_enabled", "`fog_enabled` TINYINT(1) NOT NULL DEFAULT 1");
        EnsureColumn(conn, "flashlight_enabled", "`flashlight_enabled` TINYINT(1) NOT NULL DEFAULT 1");
        EnsureColumn(conn, "tags_enabled", "`tags_enabled` TINYINT(1) NOT NULL DEFAULT 1");
        EnsureColumn(conn, "ads_enabled", "`ads_enabled` TINYINT(1) NOT NULL DEFAULT 1");
        EnsureColumn(conn, "hide_players", "`hide_players` TINYINT(1) NOT NULL DEFAULT 0");
        EnsureColumn(conn, "vip_reward_messages", "`vip_reward_messages` TINYINT(1) NOT NULL DEFAULT 1");
    }

    private static void EnsureColumn(IDbConnection conn, string columnName, string columnDefinition)
    {
        using var check = conn.CreateCommand();
        check.CommandText = """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'zpl_user_preferences'
              AND COLUMN_NAME = @columnName
            """;
        AddParam(check, "@columnName", columnName);

        bool exists = Convert.ToInt32(check.ExecuteScalar()) > 0;
        if (exists)
            return;

        using var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE zpl_user_preferences ADD COLUMN {columnDefinition}";
        alter.ExecuteNonQuery();
    }

    private static void AddParam(IDbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }
}
