using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Admins.Bans.Manager;

/// <summary>
/// SQLite-backed repository that stores and queries server bans.
///
/// Schema (single table):
///   CREATE TABLE IF NOT EXISTS server_bans (
///       id              INTEGER PRIMARY KEY AUTOINCREMENT,
///       steam_id        TEXT    NOT NULL DEFAULT '',
///       ip              TEXT    NOT NULL DEFAULT '',
///       reason          TEXT    NOT NULL DEFAULT '',
///       admin_name      TEXT    NOT NULL DEFAULT '',
///       admin_steam_id  TEXT    NOT NULL DEFAULT '',
///       created_at      TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ','now')),
///       expires_at      TEXT
///   );
///
/// All writes (AddBan, RemoveBan) are synchronous and issued on the game-server
/// thread.  <see cref="FindActiveBanAsync"/> is the one async entry-point; it is
/// designed to be called fire-and-forget from a synchronous event handler while
/// still surfacing results back on the game thread via the scheduler callback
/// supplied by the caller.
/// </summary>
public sealed class ServerBans
{
    private readonly ILogger<ServerBans> _logger;

    private string? _connectionString;
    private bool    _ready;

    public ServerBans(ILogger<ServerBans> logger)
    {
        _logger = logger;
    }

    // ── Initialisation ────────────────────────────────────────────────────────

    /// <summary>
    /// Opens (or creates) the SQLite database at <paramref name="dbPath"/>,
    /// enables WAL journal mode, and ensures the bans table and its indexes
    /// exist.  Must be called once from the plugin's <c>Load</c> method.
    /// </summary>
    public void EnsureSchema(string dbPath)
    {
        try
        {
            string? dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode       = SqliteOpenMode.ReadWriteCreate
            }.ToString();

            using var conn = OpenConnection();
            using var cmd  = conn.CreateCommand();

            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            cmd.ExecuteNonQuery();

            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS server_bans (
                    id             INTEGER PRIMARY KEY AUTOINCREMENT,
                    steam_id       TEXT    NOT NULL DEFAULT '',
                    ip             TEXT    NOT NULL DEFAULT '',
                    reason         TEXT    NOT NULL DEFAULT '',
                    admin_name     TEXT    NOT NULL DEFAULT '',
                    admin_steam_id TEXT    NOT NULL DEFAULT '',
                    created_at     TEXT    NOT NULL
                                   DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ','now')),
                    expires_at     TEXT
                );
                """;
            cmd.ExecuteNonQuery();

            cmd.CommandText =
                "CREATE INDEX IF NOT EXISTS idx_bans_steam_id ON server_bans (steam_id);";
            cmd.ExecuteNonQuery();

            cmd.CommandText =
                "CREATE INDEX IF NOT EXISTS idx_bans_ip ON server_bans (ip);";
            cmd.ExecuteNonQuery();

            _ready = true;
            _logger.LogInformation("[AdminsBans] Database initialised at '{Path}'.", dbPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "[AdminsBans] Failed to initialise database at '{Path}': {Ex}",
                dbPath, ex.Message);
        }
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Asynchronously looks up the first active ban that matches either
    /// <paramref name="steamId64"/> or <paramref name="playerIp"/>.
    /// Returns <c>null</c> when no active ban is found or when the database
    /// has not yet been initialised.
    ///
    /// <para><b>Bug fixes applied here:</b></para>
    /// <list type="bullet">
    ///   <item><description>
    ///     <b>NullReferenceException (was at the database-open line):</b>
    ///     The method now returns <c>null</c> immediately when
    ///     <c>_connectionString</c> is <c>null</c> (i.e. when
    ///     <see cref="EnsureSchema"/> has not run yet or failed silently).
    ///     Previously the code attempted to open a connection with a null
    ///     connection string and threw <see cref="NullReferenceException"/>,
    ///     which propagated as an unobserved task exception and aborted the
    ///     server.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Unobserved task exception:</b>
    ///     All database work is now wrapped in a try/catch so that any I/O
    ///     or SQL error is logged and returns <c>null</c> rather than
    ///     propagating as an unhandled exception on a fire-and-forget call
    ///     site.  The caller is also responsible for observing the task via
    ///     <c>ContinueWith</c> (see <c>AdminsBansPlugin</c>).
    ///   </description></item>
    /// </list>
    /// </summary>
    public async Task<BanRecord?> FindActiveBanAsync(long steamId64, string playerIp)
    {
        // Guard: return null immediately when the database is not ready.
        // This prevents the NullReferenceException that occurred when
        // _connectionString was null and the caller did not await this task.
        if (!_ready || _connectionString is null)
            return null;

        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT id, steam_id, ip, reason, admin_name, admin_steam_id,
                       created_at, expires_at
                FROM   server_bans
                WHERE  (steam_id = $sid OR ip = $ip)
                  AND  (expires_at IS NULL
                        OR expires_at > strftime('%Y-%m-%dT%H:%M:%SZ', 'now'))
                LIMIT  1;
                """;
            cmd.Parameters.AddWithValue("$sid", steamId64.ToString());
            cmd.Parameters.AddWithValue("$ip",  playerIp ?? string.Empty);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return new BanRecord
            {
                Id             = reader.GetInt64(0),
                SteamId64      = long.TryParse(reader.GetString(1), out long sid)  ? sid  : 0L,
                Ip             = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Reason         = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                AdminName      = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                AdminSteamId64 = long.TryParse(
                                     reader.IsDBNull(5) ? "0" : reader.GetString(5),
                                     out long asid) ? asid : 0L,
                CreatedAt      = DateTime.TryParse(
                                     reader.IsDBNull(6) ? null : reader.GetString(6),
                                     out DateTime ca) ? ca : DateTime.UtcNow,
                ExpiresAt      = reader.IsDBNull(7)
                                 ? null
                                 : DateTime.TryParse(reader.GetString(7), out DateTime ea)
                                   ? ea
                                   : (DateTime?)null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "[AdminsBans] FindActiveBanAsync({SteamId}, {Ip}) failed: {Ex}",
                steamId64, playerIp, ex.Message);
            return null;
        }
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a new ban row.  Pass <c>null</c> for <paramref name="expiresAt"/>
    /// to create a permanent ban.
    /// </summary>
    public void AddBan(
        long     steamId64,
        string   ip,
        string   reason,
        string   adminName,
        long     adminSteamId64,
        DateTime? expiresAt)
    {
        if (!_ready || _connectionString is null) return;
        try
        {
            using var conn = OpenConnection();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO server_bans
                    (steam_id, ip, reason, admin_name, admin_steam_id, expires_at)
                VALUES ($sid, $ip, $reason, $admin, $asid, $exp);
                """;
            cmd.Parameters.AddWithValue("$sid",    steamId64.ToString());
            cmd.Parameters.AddWithValue("$ip",     ip ?? string.Empty);
            cmd.Parameters.AddWithValue("$reason", reason ?? string.Empty);
            cmd.Parameters.AddWithValue("$admin",  adminName ?? string.Empty);
            cmd.Parameters.AddWithValue("$asid",   adminSteamId64.ToString());
            cmd.Parameters.AddWithValue(
                "$exp",
                expiresAt.HasValue
                    ? expiresAt.Value.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    : (object)DBNull.Value);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger.LogError("[AdminsBans] AddBan({SteamId}) failed: {Ex}", steamId64, ex.Message);
        }
    }

    /// <summary>
    /// Removes all active bans for the given <paramref name="steamId64"/>.
    /// </summary>
    public void RemoveBan(long steamId64)
    {
        if (!_ready || _connectionString is null) return;
        try
        {
            using var conn = OpenConnection();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText =
                "DELETE FROM server_bans WHERE steam_id = $sid;";
            cmd.Parameters.AddWithValue("$sid", steamId64.ToString());
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger.LogError("[AdminsBans] RemoveBan({SteamId}) failed: {Ex}", steamId64, ex.Message);
        }
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private SqliteConnection OpenConnection()
    {
        // _connectionString is guaranteed non-null by all callers (they check
        // _ready && _connectionString is not null before calling), but we use
        // the null-forgiving operator to make the contract explicit.
        var conn = new SqliteConnection(_connectionString!);
        conn.Open();
        return conn;
    }
}
