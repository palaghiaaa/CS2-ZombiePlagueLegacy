using Admins.Core.Contract;
using Cookies.Contract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.NetMessages;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.ProtobufDefinitions;
using System.Net;
using System.Data;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TagsApi;
using ZombiePlagueLegacyCS2;
using ZombiePlagueLegacyCS2.SharedUi;
using static TagsApi.Tags;

namespace ZPLTags;

[PluginMetadata(
    Id          = "ZPLTags",
    Version     = "1.1.0",
    Name        = "ZPL Tags",
    Author      = "DeadPoolCS2",
    Description = "Tag-selection menu for any player with eligible tags (via Admins-plugin " +
                  "group or SwiftlyS2 permission). Bridges cs2-tags without native permissions.")]
public class ZPLTagsPlugin(ISwiftlyCore core) : BasePlugin(core)
{
    private const string ConfigSection           = "ZPLTags";
    private const float  TagsApiProbeIntervalSec = 2.0f;
    private const int    TagsApiProbeMaxAttempts = 45;

    // Guards OnMessageSayText2 against being called in a partially-torn-down
    // state (e.g. during a SwiftlyS2 hot-reload race between Unload() and the
    // network-receive thread dispatching a FilterMessage callback).  Must be
    // volatile so the write in Unload() is immediately visible to the network
    // thread that runs OnMessageSayText2.
    private volatile bool _isLoaded;

    private ILogger<ZPLTagsPlugin>?       _logger;
    private IOptionsMonitor<ZPLTagsCFG>?  _cfgMonitor;
    private ZPLTagsCFG                    _config = new();
    private ServiceProvider?              _sp;
    private IInterfaceManager?            _interfaceManager;

    // Shared interfaces — both are optional; the plugin degrades gracefully.
    // NOTE: _tagsBridge is a local (ZPLTags) type, so no TagsApi.dll resolution
    //       is needed to JIT-compile any method that reads/writes this field.
    private TagsBridge?           _tagsBridge;
    private IAdminsManager?       _adminsManager;
    private IPlayerCookiesAPIv1?  _cookiesApi;
    private IZombiePlagueLegacyAPI? _zplApi;

    // SteamID64 → ALL eligible GroupTagEntry items, sorted desc by Priority.
    private readonly Dictionary<ulong, List<GroupTagEntry>> _eligibleTags = new();

    // SteamID64 → currently active entry.
    //   key present + non-null  → a specific tag is active
    //   key present + null      → player explicitly chose "no tag"
    //   key absent              → no eligible tags found yet
    private readonly Dictionary<ulong, GroupTagEntry?> _activeTag = new();

    private CancellationTokenSource? _refreshCts;
    private CancellationTokenSource? _tagsApiProbeCts;

    // ── MySQL tag persistence ─────────────────────────────────────────────────
    // SteamID64 → saved composite cookie value loaded from MySQL at plugin start.
    // Takes precedence over the Cookies API when a database connection is configured.
    private readonly Dictionary<ulong, string> _savedTags = new();
    private bool   _dbReady;
    private string _dbConnectionName = string.Empty;

    // Prevents duplicate EventNextlevelChanged fires within a single world update.
    // Interlocked operations provide the required memory barriers; volatile is
    // added as an extra marker for clarity.
    private volatile int _scoreRefreshScheduled;

    // Must be shorter than cs2-tags' 1 s revalidation to avoid score-tag flicker.
    private const float  ScoreTagRefreshInterval = 0.8f;
    private const string ConfigFile              = "ZPLTagsCFG.jsonc";
    private const string CookieKey               = "zpltags.selected";
    private const string NullTagSentinel         = "__none__";
    private static readonly Regex HtmlTagRegex   = new("<[^>]+>", RegexOptions.Compiled);

     // Extended retry delays (seconds) used in OnClientConnected to catch permissions
    // that are granted asynchronously after the initial 2 s window.
    // Matches cs2-tags' 40 s PermissionWarmupWindow.
    private static readonly float[] ConnectRetryDelays = [5.0f, 10.0f, 20.0f, 40.0f];


    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void Load(bool hotReload)
    {
        // Guard with !hotReload: SwiftlyS2's PluginConfigurationService.Manager is a
        // lazy singleton that is never reset between hot-reloads (map changes).
        // Calling AddJsonFile on it again on every Load() appends a brand-new
        // FileSystemWatcher thread to the same ConfigurationManager, causing one
        // watcher thread to leak per map change.  Using !hotReload (not a static
        // flag) is correct because SwiftlyS2 loads each plugin into a fresh
        // AssemblyLoadContext on hot-reload, resetting all static variables, so a
        // static bool sentinel would be incorrect and would not prevent the leak.
        if (!hotReload)
        {
            Core.Configuration.InitializeJsonWithModel<ZPLTagsCFG>(ConfigFile, ConfigSection)
                .Configure(builder =>
                {
                    builder.AddJsonFile(ConfigFile, false, true);
                    builder.SetFileLoadExceptionHandler(ctx =>
                    {
                        Core.Logger.LogError(
                            "[ZPLTags] Failed to load {File}: {Error}. Using last valid config.",
                            ConfigFile, ctx.Exception.Message);
                        ctx.Ignore = true;
                    });
                });
        }

        var services = new ServiceCollection();
        services.AddSwiftly(Core);
        services.AddSingleton<ISwiftlyCore>(Core);
        services.AddOptions<ZPLTagsCFG>().BindConfiguration(ConfigSection);

        _sp         = services.BuildServiceProvider();
        _logger     = _sp.GetRequiredService<ILogger<ZPLTagsPlugin>>();
        _cfgMonitor = _sp.GetRequiredService<IOptionsMonitor<ZPLTagsCFG>>();
        _config     = _cfgMonitor.CurrentValue;
        _cfgMonitor.OnChange(cfg =>
        {
            _config = cfg;
            // Rebuild eligibility for all connected players on config change.
            Core.Scheduler.NextWorldUpdate(RebuildAllPlayers);
        });

        Core.Event.OnClientConnected    += OnClientConnected;
        Core.Event.OnClientDisconnected += OnClientDisconnected;

        Core.Command.RegisterCommand(_config.MenuCommand, CmdTags, true);

        // ── MySQL tag persistence ─────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(_config.DatabaseConnection))
            DbEnsureSchemaAndLoadAll(_config.DatabaseConnection);

        _refreshCts = new CancellationTokenSource();
        ScheduleScoreTagRefreshLoop(_refreshCts.Token);

        _tagsApiProbeCts = new CancellationTokenSource();

        if (hotReload)
            Core.Scheduler.NextWorldUpdate(RebuildAllPlayers);

        _isLoaded = true;
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        _interfaceManager = interfaceManager;

        // ── cs2-tags ──────────────────────────────────────────────────────────
        // IMPORTANT: all ITagApi / TagType / MessageProcess type references live
        // inside TryConnectTagsBridge (a NoInlining helper).  This method itself
        // never references any TagsApi type, so the JIT can compile it without
        // loading TagsApi.dll.  TryConnectTagsBridge is only ever called when
        // HasSharedInterface returns true, which means TagsApi.dll IS loaded, so
        // its own JIT compilation succeeds.
        if (!EnsureTagsBridgeConnected(interfaceManager))
        {
            if (_tagsApiProbeCts != null)
                ScheduleTagsApiProbe(_tagsApiProbeCts.Token, 1);
        }

        // ── Admins plugin (optional) ──────────────────────────────────────────
        if (interfaceManager.HasSharedInterface("Admins.Admins.V1"))
        {
            try
            {
                _adminsManager = interfaceManager.GetSharedInterface<IAdminsManager>("Admins.Admins.V1");
                if (_adminsManager != null)
                {
                    _adminsManager.OnAdminLoad += OnAdminLoad;
                    _logger?.LogInformation("[ZPLTags] Connected to Admins API.");
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger?.LogWarning("[ZPLTags] Admins API unavailable: {Error} – group-based matching disabled.", ex.Message);
            }
        }
        else
        {
        }

        // ── Cookies plugin (optional) ─────────────────────────────────────────
        if (interfaceManager.HasSharedInterface("Cookies.Player.v1"))
        {
            try
            {
                _cookiesApi = interfaceManager.GetSharedInterface<IPlayerCookiesAPIv1>("Cookies.Player.v1");
                if (_cookiesApi != null)
                    _logger?.LogInformation("[ZPLTags] Connected to Cookies API – tag selections will persist across sessions.");
            }
            catch (InvalidOperationException ex)
            {
                _logger?.LogWarning("[ZPLTags] Cookies API unavailable: {Error}", ex.Message);
            }
        }
        else
        {
            _logger?.LogInformation("[ZPLTags] Cookies API not found – tag selections will not persist across sessions.");
        }

        if (interfaceManager.HasSharedInterface("ZombiePlagueLegacy"))
        {
            try
            {
                _zplApi = interfaceManager.GetSharedInterface<IZombiePlagueLegacyAPI>("ZombiePlagueLegacy");
                if (_zplApi != null)
                {
                    _zplApi.ZPL_OnUserPreferenceChanged += OnZplUserPreferenceChanged;
                    _logger?.LogInformation("[ZPLTags] Connected to ZombiePlagueLegacy user preferences.");
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger?.LogWarning("[ZPLTags] ZombiePlagueLegacy API unavailable: {Error}", ex.Message);
            }
        }

        Core.Scheduler.NextWorldUpdate(RebuildAllPlayers);
    }

    /// <summary>
    /// Isolates all <c>ITagApi</c> / <c>TagType</c> type references so the JIT
    /// only loads <c>TagsApi.dll</c> when this method is actually called — i.e.
    /// only after <c>HasSharedInterface("Tags.Api")</c> has confirmed the DLL is
    /// present.  Must be <see cref="MethodImplOptions.NoInlining"/> so the
    /// compiler cannot hoist these type tokens into the calling method.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void TryConnectTagsBridge(IInterfaceManager interfaceManager)
    {
        try
        {
            if (_tagsBridge != null) return;
            var api = interfaceManager.GetSharedInterface<ITagApi>("Tags.Api");
            if (api != null)
            {
                _tagsBridge = new TagsBridge(api, _activeTag, AreTagsEnabled);
                _tagsBridge.Subscribe();
                _logger?.LogInformation("[ZPLTags] Connected to Tags API (cs2-tags).");
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogWarning("[ZPLTags] Tags API unavailable: {Error}", ex.Message);
        }
    }

    private bool EnsureTagsBridgeConnected(IInterfaceManager interfaceManager)
    {
        if (_tagsBridge != null) return true;
        if (!interfaceManager.HasSharedInterface("Tags.Api")) return false;
        TryConnectTagsBridge(interfaceManager);
        return _tagsBridge != null;
    }

    private void ScheduleTagsApiProbe(CancellationToken token, int attempt)
    {
        if (attempt > TagsApiProbeMaxAttempts || token.IsCancellationRequested || _tagsBridge != null)
            return;

        Core.Scheduler.DelayBySeconds(TagsApiProbeIntervalSec, () =>
        {
            if (token.IsCancellationRequested || _tagsBridge != null) return;

            Core.Scheduler.NextWorldUpdate(() =>
            {
                if (token.IsCancellationRequested || _tagsBridge != null) return;
                if (_interfaceManager != null && EnsureTagsBridgeConnected(_interfaceManager))
                {
                    _logger?.LogInformation("[ZPLTags] Connected to Tags API after delayed probe (attempt {Attempt}).", attempt);
                    return;
                }

                ScheduleTagsApiProbe(token, attempt + 1);
            });
        });
    }

    public override void Unload()
    {
        // Signal the [ServerNetMessageHandler] to stop immediately.  This is the
        // first thing in Unload() so that if the network-receive thread (which calls
        // FilterMessage / OnMessageSayText2 concurrently on a non-managed thread)
        // races with this cleanup, the handler bails out before touching any ZPL
        // state that is about to be torn down.  This mirrors the root cause of the
        // 2026-04-15 SIGSEGV: libnetworksystem.so → swiftlys2 FilterMessage →
        // null managed-delegate pointer after hot-reload GC.
        _isLoaded = false;

        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;

        _tagsApiProbeCts?.Cancel();
        _tagsApiProbeCts?.Dispose();
        _tagsApiProbeCts = null;

        _interfaceManager = null;

        if (_zplApi != null)
        {
            _zplApi.ZPL_OnUserPreferenceChanged -= OnZplUserPreferenceChanged;
            _zplApi = null;
        }

        foreach (var player in Core.PlayerManager.GetAllPlayers())
        {
            if (player != null && player.IsValid)
                Core.MenusAPI.CloseActiveMenu(player);
        }

        // Unsubscribes from the cs2-tags event; TagsBridge.Unsubscribe() is
        // [MethodImpl(NoInlining)] and references ITagApi in its body, so calling
        // it here does NOT force TagsApi.dll to load when Unload() is JIT-compiled.
        _tagsBridge?.Unsubscribe();
        _tagsBridge = null;

        if (_adminsManager != null)
        {
            _adminsManager.OnAdminLoad -= OnAdminLoad;
            _adminsManager = null;
        }

        _cookiesApi = null;

        Core.Event.OnClientConnected    -= OnClientConnected;
        Core.Event.OnClientDisconnected -= OnClientDisconnected;

        _eligibleTags.Clear();
        _activeTag.Clear();
        _savedTags.Clear();
        _dbReady = false;
        _sp?.Dispose();
        _sp = null;
    }

    // ── Eligibility helpers ───────────────────────────────────────────────────

    private static bool IsValidRealPlayer(IPlayer? player)
        => player != null && player.IsValid && !player.IsFakeClient && player.SteamID != 0;

    /// <summary>
    /// Returns true when <paramref name="player"/> qualifies for
    /// <paramref name="entry"/> via either admin group OR SwiftlyS2 permission.
    /// Safe to call from any thread (only reads _adminsManager, config, and
    /// Core.Permission — all thread-safe for reads).
    /// </summary>
    private bool PlayerMatchesEntry(IPlayer player, GroupTagEntry entry)
    {
        // 1) Admins-plugin group match
        if (!string.IsNullOrEmpty(entry.GroupName) && _adminsManager != null)
        {
            var admin = _adminsManager.GetAdmin(player);
            if (admin != null)
            {
                foreach (var g in admin.Groups)
                {
                    if (string.Equals(g, entry.GroupName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }

        // 2) SwiftlyS2 native permission match
        if (!string.IsNullOrEmpty(entry.Permission))
        {
            var permission = entry.Permission.Trim();
            if (permission.Length == 0) return false;

            if (Core.Permission.PlayerHasPermission(player.SteamID, permission))
                return true;

            // Accept both formats transparently: "@module/role" and "module/role".
            if (!permission.StartsWith('@'))
            {
                if (Core.Permission.PlayerHasPermission(player.SteamID, $"@{permission}"))
                    return true;
            }
            else
            {
                var withoutAt = permission[1..];
                if (!string.IsNullOrEmpty(withoutAt) &&
                    Core.Permission.PlayerHasPermission(player.SteamID, withoutAt))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Builds the sorted (desc Priority) list of all eligible entries for
    /// <paramref name="player"/>.
    /// </summary>
    private List<GroupTagEntry> BuildEligibleList(IPlayer player)
    {
        var cfg    = _config;
        var result = new List<GroupTagEntry>();

        foreach (var entry in cfg.GroupTags)
        {
            // Skip entries with neither condition set — they'd match everyone.
            if (string.IsNullOrEmpty(entry.GroupName) && string.IsNullOrEmpty(entry.Permission))
                continue;

            if (PlayerMatchesEntry(player, entry))
                result.Add(entry);
        }

        result.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        return result;
    }

    private void ApplyScoreTag(IPlayer player, GroupTagEntry? entry)
    {
        if (!IsValidRealPlayer(player)) return;
        if (_tagsBridge != null)
            _tagsBridge.ApplyScore(player, entry);
        else
            SetScoreTagDirect(player, entry?.ScoreTag);
    }

    /// <summary>
    /// Applies ALL tag attributes (score, chat tag, chat color, name color) for
    /// <paramref name="entry"/> when cs2-tags is loaded, or falls back to setting
    /// only the scoreboard clan tag when cs2-tags is absent.
    /// Use this on connect / restore so chat tags are active immediately.
    /// </summary>
    private void ApplyFullTag(IPlayer player, GroupTagEntry? entry)
    {
        if (!IsValidRealPlayer(player)) return;
        if (_tagsBridge != null)
        {
            if (entry != null)
                _tagsBridge.ApplyFull(player, entry);
            else
                _tagsBridge.ClearFull(player);
        }
        else
        {
            SetScoreTagDirect(player, entry?.ScoreTag);
        }
    }

    /// <summary>
    /// Applies a score (clan) tag directly via the player controller schema, used
    /// when cs2-tags is not loaded.  Must be called from the game thread.
    /// </summary>
    private void SetScoreTagDirect(IPlayer player, string? score)
    {
        var ctrl = player?.Controller;
        if (ctrl == null || !ctrl.IsValid) return;

        var normalized = score ?? string.Empty;
        if (ctrl.Clan != normalized)
            ctrl.Clan = normalized;
        // Notifies the game engine that the clan tag field changed so the
        // scoreboard is updated without requiring a full state-baseline flush.
        ctrl.ClanUpdated();
        FireScoreTagRefresh();
    }

    /// <summary>
    /// Fires <see cref="EventNextlevelChanged"/> at most once per world update to
    /// refresh the scoreboard clan-tag column.
    /// </summary>
    private void FireScoreTagRefresh()
    {
        if (System.Threading.Interlocked.Exchange(ref _scoreRefreshScheduled, 1) == 1) return;
        Core.Scheduler.NextWorldUpdate(() =>
        {
            System.Threading.Interlocked.Exchange(ref _scoreRefreshScheduled, 0);
            Core.GameEvent.Fire<EventNextlevelChanged>();
        });
    }

    // ── Cookie helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the composite cookie value for <paramref name="entry"/>.
    /// Used as the persistent key stored in the Cookies plugin.
    /// </summary>
    private static string MakeCookieValue(GroupTagEntry entry)
        => $"{entry.GroupName}|{entry.Permission}";

    /// <summary>
    /// Finds the first entry in <paramref name="eligible"/> whose composite key
    /// matches <paramref name="cookieVal"/>.
    /// </summary>
    private static GroupTagEntry? FindEntryByCookieValue(string cookieVal, List<GroupTagEntry> eligible)
    {
        foreach (var e in eligible)
            if (MakeCookieValue(e) == cookieVal) return e;
        return null;
    }

    /// <summary>
    /// Persists <paramref name="cookieVal"/> for <paramref name="player"/>.
    /// Saves to MySQL when configured; also writes to Cookies API as a fallback.
    /// Safe to fire-and-forget.
    /// </summary>
    private void SaveTagCookie(IPlayer player, string cookieVal)
    {
        // MySQL: update the in-memory cache and persist to DB.
        if (_dbReady)
        {
            _savedTags[player.SteamID] = cookieVal;
            DbSaveTag(player.SteamID, cookieVal);
        }

        // Cookies: keep as a secondary/fallback store.
        if (_cookiesApi == null) return;
        _cookiesApi.Set(player, CookieKey, cookieVal);
        _ = _cookiesApi.Save(player);
    }

    // ── MySQL tag persistence helpers ─────────────────────────────────────────

    /// <summary>
    /// Creates the <c>zpl_tag_selections</c> table if absent, then loads all
    /// rows into <see cref="_savedTags"/> so <see cref="InitPlayer"/> can
    /// restore preferences without a per-player query.
    /// </summary>
    private void DbEnsureSchemaAndLoadAll(string connectionName)
    {
        _dbConnectionName = connectionName;
        try
        {
            using var conn = DbOpen();
            using var createCmd = conn.CreateCommand();
            createCmd.CommandText = """
                CREATE TABLE IF NOT EXISTS zpl_tag_selections (
                    steam_id  VARCHAR(32)  NOT NULL,
                    tag_value VARCHAR(255) NOT NULL DEFAULT '',
                    PRIMARY KEY (steam_id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                """;
            createCmd.ExecuteNonQuery();

            using var selectCmd = conn.CreateCommand();
            selectCmd.CommandText = "SELECT steam_id, tag_value FROM zpl_tag_selections";
            using var reader = selectCmd.ExecuteReader();
            while (reader.Read())
            {
                if (!ulong.TryParse(reader.GetString(0), out ulong sid)) continue;
                _savedTags[sid] = reader.GetString(1);
            }

            _dbReady = true;
            _logger?.LogInformation("[ZPLTags] MySQL tag persistence ready (connection='{Name}', {Count} row(s) loaded).",
                connectionName, _savedTags.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError("[ZPLTags] Failed to initialise MySQL tag persistence (connection='{Name}'): {Ex}",
                connectionName, ex.Message);
        }
    }

    /// <summary>Upserts one player's tag selection in the database.</summary>
    private void DbSaveTag(ulong steamId, string tagValue)
    {
        try
        {
            using var conn = DbOpen();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO zpl_tag_selections (steam_id, tag_value)
                VALUES (@sid, @val)
                ON DUPLICATE KEY UPDATE tag_value = VALUES(tag_value)
                """;
            DbAddParam(cmd, "@sid", steamId.ToString());
            DbAddParam(cmd, "@val", tagValue);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("[ZPLTags] DbSaveTag({SteamId}) failed: {Ex}", steamId, ex.Message);
        }
    }

    private IDbConnection DbOpen()
    {
        var conn = Core.Database.GetConnection(_dbConnectionName);
        conn.Open();
        return conn;
    }

    private static void DbAddParam(IDbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }

    // ── ScoreTag refresh loop ─────────────────────────────────────────────────

    private void ScheduleScoreTagRefreshLoop(CancellationToken ct)
    {
        Core.Scheduler.DelayBySeconds(ScoreTagRefreshInterval, () =>
        {
            if (ct.IsCancellationRequested) return;
            Core.Scheduler.NextWorldUpdate(() =>
            {
                if (ct.IsCancellationRequested) return;

                foreach (var player in Core.PlayerManager.GetAllPlayers())
                {
                    if (!IsValidRealPlayer(player)) continue;
                    if (!AreTagsEnabled(player!)) continue;
                    if (!_activeTag.TryGetValue(player!.SteamID, out var entry)) continue;
                    if (entry != null && !string.IsNullOrEmpty(entry.ScoreTag))
                        ApplyScoreTag(player, entry);
                }

                ScheduleScoreTagRefreshLoop(ct);
            });
        });
    }

    // ── Cache management ──────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds eligible-tag lists for every connected real player.
    /// Called on plugin load/hot-reload and on config change.
    /// Must run on the game thread.
    /// </summary>
    private void RebuildAllPlayers()
    {
        _eligibleTags.Clear();
        _activeTag.Clear();

        foreach (var player in Core.PlayerManager.GetAllPlayers())
        {
            if (!IsValidRealPlayer(player)) continue;
            InitPlayer(player!);
        }
    }

    /// <summary>
    /// Computes eligible tags for one player and restores their saved cookie
    /// preference, or auto-selects the top-priority entry if none is saved.
    /// Must run on the game thread.
    /// </summary>
    private void InitPlayer(IPlayer player)
    {
        ulong sid      = player.SteamID;
        if (!AreTagsEnabled(player))
        {
            ApplyFullTag(player, null);
            return;
        }

        var eligible   = BuildEligibleList(player);

        if (eligible.Count == 0)
        {
            _eligibleTags.Remove(sid);
            _activeTag.Remove(sid);
            return;
        }

        _eligibleTags[sid] = eligible;

        // Try to restore from the player's saved preference.
        // MySQL takes precedence when configured; Cookies API is the fallback.
        string? savedValue = null;

        if (_dbReady && _savedTags.TryGetValue(sid, out var dbVal))
            savedValue = dbVal;
        else if (_cookiesApi != null && _cookiesApi.Has(player, CookieKey))
            savedValue = _cookiesApi.Get<string>(player, CookieKey);

        if (savedValue != null)
        {
            if (savedValue == NullTagSentinel)
            {
                // Player explicitly saved "no tag".
                _activeTag[sid] = null;
                // Use ApplyFullTag so chat/name attributes are also cleared.
                ApplyFullTag(player, null);
                return;
            }

            if (!string.IsNullOrEmpty(savedValue))
            {
                var found = FindEntryByCookieValue(savedValue, eligible);
                if (found != null)
                {
                    _activeTag[sid] = found;
                    // Apply all attributes (score + chat tag + colors) on restore.
                    ApplyFullTag(player, found);
                    return;
                }
                // Saved entry is no longer eligible (group/permission removed) —
                // fall through to auto-select.
            }
        }

        // Auto-select top-priority only if the player has no existing choice.
        if (!_activeTag.ContainsKey(sid))
        {
            _activeTag[sid] = eligible[0];
            // Apply all attributes so chat tags are active immediately on connect.
            ApplyFullTag(player, eligible[0]);
        }
    }

    private bool AreTagsEnabled(IPlayer player)
    {
        return _zplApi?.ZPL_GetUserPreference(player, ZPLUserPreferenceKeys.Tags, true) ?? true;
    }

    private void OnZplUserPreferenceChanged(ulong steamId, string key, bool enabled)
    {
        if (!string.Equals(key, ZPLUserPreferenceKeys.Tags, StringComparison.Ordinal))
            return;

        Core.Scheduler.NextWorldUpdate(() =>
        {
            var player = Core.PlayerManager.GetAllPlayers()
                .FirstOrDefault(p => p != null && p.IsValid && !p.IsFakeClient && p.SteamID == steamId);
            if (player == null)
                return;

            if (!enabled)
            {
                ApplyFullTag(player, null);
                Chat(player, "TagsDisabledByUser");
                return;
            }

            if (_activeTag.TryGetValue(player.SteamID, out var entry) && entry != null)
                ApplyFullTag(player, entry);
            else
                InitPlayer(player);
            Chat(player, "TagsEnabledByUser");
        });
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    /// <summary>
    /// Fired by the Admins plugin when an admin's record finishes loading.
    /// May come from a background thread; game-state writes deferred to
    /// NextWorldUpdate.
    /// </summary>
    private void OnAdminLoad(IPlayer player, IAdmin admin)
    {
        if (player == null || player.SteamID == 0) return;

        Core.Scheduler.NextWorldUpdate(() =>
        {
            if (!IsValidRealPlayer(player)) return;
            InitPlayer(player);
        });
    }

    private void OnClientConnected(IOnClientConnectedEvent @event)
    {
        int playerId = @event.PlayerId;

        // Early retries run unconditionally; they cover the common case where
        // permissions are available by 0.5 s (SwiftlyS2 native flags) or by
        // 2.0 s (Admins-plugin async load).
        Core.Scheduler.DelayBySeconds(0.5f, () =>
        {
            Core.Scheduler.NextWorldUpdate(() =>
            {
                var player = Core.PlayerManager.GetPlayer(playerId);
                if (IsValidRealPlayer(player)) InitPlayer(player!);
            });
        });

        Core.Scheduler.DelayBySeconds(2.0f, () =>
        {
            Core.Scheduler.NextWorldUpdate(() =>
            {
                var player = Core.PlayerManager.GetPlayer(playerId);
                if (IsValidRealPlayer(player)) InitPlayer(player!);
            });
        });



        // Extended retries cover external plugins (e.g. ShopCore) that grant
        // SwiftlyS2-native permissions asynchronously after the 2 s window.
        // Each attempt is guarded: if InitPlayer already populated _activeTag
        // for this player (including "no tag" sentinel), we skip to avoid
        // overriding a deliberate tag selection or spamming the permission API.
        // The window intentionally matches cs2-tags' 40 s PermissionWarmupWindow.
        foreach (float delay in ConnectRetryDelays)
        {
            Core.Scheduler.DelayBySeconds(delay, () =>
            {
                Core.Scheduler.NextWorldUpdate(() =>
                {
                    var player = Core.PlayerManager.GetPlayer(playerId);
                    if (!IsValidRealPlayer(player)) return;
                    if (!_activeTag.ContainsKey(player!.SteamID))
                        InitPlayer(player!);
                });
            });
        }
    }

    private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player == null) return;
        _eligibleTags.Remove(player.SteamID);
        _activeTag.Remove(player.SteamID);
    }

    // ── Chat interception ─────────────────────────────────────────────────────
    // Preferred path uses TagsBridge (Tags.Api event). This fallback path ensures
    // chat tags still apply when cs2-tags is missing or not yet available.

    [ServerNetMessageHandler]
    public HookResult OnMessageSayText2(CUserMessageSayText2 msg)
    {
        // _isLoaded is set to false at the top of Unload() so that a concurrent
        // FilterMessage call (network-receive thread) that races with plugin teardown
        // exits cleanly instead of touching freed state.
        if (!_isLoaded) return HookResult.Continue;
        if (_tagsBridge != null) return HookResult.Continue;
        if (msg.Entityindex <= 0) return HookResult.Continue;

        var player = Core.PlayerManager.GetPlayer(msg.Entityindex - 1);
        if (!IsValidRealPlayer(player)) return HookResult.Continue;
        if (!AreTagsEnabled(player!)) return HookResult.Continue;
        if (!_activeTag.TryGetValue(player!.SteamID, out var entry) || entry == null) return HookResult.Continue;

        var format = ResolveFallbackChatFormat(msg.Messagename);
        if (format == null) return HookResult.Continue;

        string nameColor = NormalizeFallbackColorTags(entry.NameColor, player);
        string chatTag = NormalizeFallbackColorTags(entry.ChatTag, player);
        string chatColor = NormalizeFallbackColorTags(entry.ChatColor, player);

        string nameToken = $"{chatTag}{nameColor}%s1[/]";
        string messageToken = string.IsNullOrEmpty(chatColor) ? "%s2" : $"{chatColor}%s2[/]";

        msg.Messagename = Helper.Colored(
            format
                .Replace("{NAME}", nameToken, StringComparison.Ordinal)
                .Replace("{MESSAGE}", messageToken, StringComparison.Ordinal)
                .Replace("{LOCATION}", "%s3", StringComparison.Ordinal));

        return HookResult.Continue;
    }

    private static string? ResolveFallbackChatFormat(string? messageName)
    {
        const string prefix = "Cstrike_Chat_";

        if (string.IsNullOrWhiteSpace(messageName) ||
            !messageName.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        string tail = messageName[prefix.Length..];

        bool isAll = false;
        bool isCt = false;
        bool isT = false;
        bool isSpec = false;
        bool isDead = false;
        bool isLoc = false;

        if (tail.StartsWith("All", StringComparison.Ordinal))
        {
            isAll = true;
            tail = tail[3..];
        }
        else if (tail.StartsWith("CT", StringComparison.Ordinal))
        {
            isCt = true;
            tail = tail[2..];
        }
        else if (tail.StartsWith("Spec", StringComparison.Ordinal))
        {
            isSpec = true;
            tail = tail[4..];
        }
        else if (tail.StartsWith("T", StringComparison.Ordinal))
        {
            isT = true;
            tail = tail[1..];
        }
        else
        {
            return null;
        }

        while (tail.Length > 0)
        {
            if (tail.StartsWith("Dead", StringComparison.Ordinal))
            {
                isDead = true;
                tail = tail[4..];
                continue;
            }

            if (tail.StartsWith("Spec", StringComparison.Ordinal))
            {
                isSpec = true;
                tail = tail[4..];
                continue;
            }

            if (tail.StartsWith("Loc", StringComparison.Ordinal))
            {
                isLoc = true;
                tail = tail[3..];
                continue;
            }

            return null;
        }

        string chatPrefix = isAll
                ? string.Empty
            : isCt
                ? "[blue][CT][/] "
                : isT
                    ? "[yellow][T][/] "
                    : string.Empty;

        string suffix = string.Empty;
        if (isSpec)
            suffix += " [white][SPEC][/]";

        if (isDead)
            suffix += " [grey][DEAD][/]";

        if (isLoc)
            suffix += " [green]@{LOCATION}[/]";

        return $"{chatPrefix}{{NAME}}{suffix}: {{MESSAGE}}";
    }

    private static string NormalizeFallbackColorTags(string? value, IPlayer player)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        return value
            .Replace("[default]", "[/]", StringComparison.OrdinalIgnoreCase)
            .Replace("[teamcolor]", ResolveTeamColorTag(player), StringComparison.OrdinalIgnoreCase)
            .Replace("[compcolor]", ResolveCompColorTag(player), StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveTeamColorTag(IPlayer player)
    {
        if (player.Controller == null || !player.Controller.IsValid)
            return "[white]";

        return player.Controller.TeamNum switch
        {
            (byte)Team.T => "[yellow]",
            (byte)Team.CT => "[blue]",
            (byte)Team.Spectator => "[white]",
            _ => "[white]"
        };
    }

    private static string ResolveCompColorTag(IPlayer player)
    {
        if (player.Controller == null || !player.Controller.IsValid)
            return "[white]";

        return player.Controller.CompTeammateColor switch
        {
            -2 => "[grey]",
            0 => "[blue]",
            1 => "[green]",
            2 => "[yellow]",
            3 => "[orange]",
            4 => "[purple]",
            _ => "[white]"
        };
    }

    // ── !sw_tags command & menu ───────────────────────────────────────────────

    private string T(IPlayer player, string key, params object[] args)
    {
        var loc = Core.Translation.GetPlayerLocalizer(player);
        return args.Length == 0 ? loc[key] : loc[key, args];
    }

    private void Chat(IPlayer player, string key, params object[] args)
        => player.SendMessage(MessageType.Chat, $" {_config.ChatPrefix} {T(player, key, args)}");

    private void CmdTags(ICommandContext context)
    {
        var player = context.Sender;
        if (!IsValidRealPlayer(player)) return;

        ulong sid = player!.SteamID;
        if (!AreTagsEnabled(player))
        {
            ApplyFullTag(player, null);
            Chat(player, "TagsDisabledByUser");
            return;
        }

        // Re-compute in case eligibility changed since connect (e.g. late admin load).
        if (!_eligibleTags.TryGetValue(sid, out var eligible) || eligible.Count == 0)
        {
            // One last attempt to populate.
            eligible = BuildEligibleList(player);
            if (eligible.Count == 0)
            {
                Chat(player, "NoTagsAvailable");
                return;
            }
            _eligibleTags[sid] = eligible;
        }

        OpenTagMenu(player, eligible);
    }

    private void OpenTagMenu(IPlayer player, List<GroupTagEntry> eligible)
    {
        ulong sid = player.SteamID;
        _activeTag.TryGetValue(sid, out var current);

        var menuCfg = ZPLMenuStyle.MenuConfig(_config.MenuTitle, playSound: true);

        IMenuAPI menu = Core.MenusAPI.CreateMenu(menuCfg, default, null, MenuOptionScrollStyle.LinearScroll);

        // ── One button per eligible tag ───────────────────────────────────────
        foreach (var entry in eligible)
        {
            var capturedEntry = entry;   // capture for async closure

            bool isActive = current != null &&
                            string.Equals(current.GroupName, entry.GroupName, StringComparison.Ordinal) &&
                            string.Equals(current.Permission, entry.Permission, StringComparison.Ordinal);

            string cleanLabel = CleanMenuLabel(entry.GetMenuLabel());
            string label = isActive ? $"* {cleanLabel}" : cleanLabel;

            var btn = ZPLMenuStyle.Button(label, isActive ? ZPLMenuStyle.ColSelected : ZPLMenuStyle.ColButton);
            btn.Tag = capturedEntry;

            btn.Click += async (_, args) =>
            {
                var clicker = args.Player;
                Core.Scheduler.NextTick(() =>
                {
                    if (!IsValidRealPlayer(clicker)) return;

                    _activeTag[clicker.SteamID] = capturedEntry;
                    if (_tagsBridge != null)
                        _tagsBridge.ApplyFull(clicker, capturedEntry);
                    else
                        SetScoreTagDirect(clicker, capturedEntry.ScoreTag);

                    SaveTagCookie(clicker, MakeCookieValue(capturedEntry));
                    Chat(clicker, "TagSelected", CleanMenuLabel(capturedEntry.GetMenuLabel()));
                });
            };

            menu.AddOption(btn);
        }

        // ── "Remove tag" button ───────────────────────────────────────────────
        // Shows ✓ when no tag is currently active.
        string cleanRemoveLabel = CleanMenuLabel(_config.NoTagLabel);
        string removeLabel = (current == null)
            ? $"* {cleanRemoveLabel}"
            : cleanRemoveLabel;

        var removeBtn = ZPLMenuStyle.Button(removeLabel, current == null ? ZPLMenuStyle.ColSelected : ZPLMenuStyle.ColButton);

        removeBtn.Click += async (_, args) =>
        {
            var clicker = args.Player;
            Core.Scheduler.NextTick(() =>
            {
                if (!IsValidRealPlayer(clicker)) return;

                _activeTag[clicker.SteamID] = null;   // null = "no tag"
                if (_tagsBridge != null)
                    _tagsBridge.ClearFull(clicker);
                else
                    SetScoreTagDirect(clicker, null);

                SaveTagCookie(clicker, NullTagSentinel);
                Chat(clicker, "TagRemoved");
            });
        };

        menu.AddOption(removeBtn);

        Core.MenusAPI.OpenMenuForPlayer(player, menu);
    }

    private static string CleanMenuLabel(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string cleaned = HtmlTagRegex.Replace(text, string.Empty);
        return WebUtility.HtmlDecode(cleaned).Trim();
    }
}

/// <summary>
/// Isolates every <c>TagsApi</c> type reference (<c>ITagApi</c>, <c>TagType</c>,
/// <c>MessageProcess</c>) inside a single sealed class whose methods are all
/// <see cref="MethodImplOptions.NoInlining"/>.
///
/// <para>
/// Because the .NET JIT compiles methods lazily (on first call), keeping TagsApi
/// types out of <c>ZPLTagsPlugin</c>'s method bodies means those methods can be
/// compiled even when <c>TagsApi.dll</c> is absent.  Methods on this class are
/// only ever called after <c>IInterfaceManager.HasSharedInterface("Tags.Api")</c>
/// has confirmed the DLL is present, so their JIT compilation always succeeds.
/// </para>
/// </summary>
internal sealed class TagsBridge
{
    private readonly ITagApi _api;
    private readonly Dictionary<ulong, GroupTagEntry?> _activeTag;
    private readonly Func<IPlayer, bool> _areTagsEnabled;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public TagsBridge(ITagApi api, Dictionary<ulong, GroupTagEntry?> activeTag, Func<IPlayer, bool> areTagsEnabled)
    {
        _api            = api;
        _activeTag      = activeTag;
        _areTagsEnabled = areTagsEnabled;
    }

    /// <summary>Subscribes the chat-interception hook to the Tags API event.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Subscribe()   => _api.OnMessageProcessPre += OnMessageProcessPre;

    /// <summary>Unsubscribes the chat-interception hook from the Tags API event.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Unsubscribe() => _api.OnMessageProcessPre -= OnMessageProcessPre;

    /// <summary>Updates the scoreboard clan-tag slot for <paramref name="player"/>.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ApplyScore(IPlayer player, GroupTagEntry? entry)
    {
        string score = entry?.ScoreTag ?? string.Empty;
        _api.SetAttribute(player, TagType.ScoreTag, score);
    }

    /// <summary>
    /// Applies all tag attributes (score, chat, colors, sound) from
    /// <paramref name="entry"/> to <paramref name="player"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ApplyFull(IPlayer player, GroupTagEntry entry)
    {
        _api.SetAttribute(player, TagType.ScoreTag,  entry.ScoreTag  ?? string.Empty);
        _api.SetAttribute(player, TagType.ChatTag,   entry.ChatTag   ?? string.Empty);
        _api.SetAttribute(player, TagType.ChatColor, entry.ChatColor ?? string.Empty);
        _api.SetAttribute(player, TagType.NameColor, entry.NameColor ?? string.Empty);
        _api.SetPlayerChatSound(player, entry.ChatSound);
    }

    /// <summary>Clears all tag attributes for <paramref name="player"/>.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ClearFull(IPlayer player)
    {
        _api.SetAttribute(player, TagType.ScoreTag,  string.Empty);
        _api.SetAttribute(player, TagType.ChatTag,   string.Empty);
        _api.SetAttribute(player, TagType.ChatColor, string.Empty);
        _api.SetAttribute(player, TagType.NameColor, string.Empty);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private HookResult OnMessageProcessPre(MessageProcess mp)
    {
        if (mp?.Player == null) return HookResult.Continue;
        if (!IsValidPlayer(mp.Player)) return HookResult.Continue;
        if (!_areTagsEnabled(mp.Player)) return HookResult.Continue;

        // key absent  → no tags
        // key present, null → player chose "no tag"
        if (!_activeTag.TryGetValue(mp.Player.SteamID, out var entry) || entry == null)
            return HookResult.Continue;

        var tag = mp.Tag;
        if (!string.IsNullOrEmpty(entry.ChatTag))   tag.ChatTag   = entry.ChatTag;
        if (!string.IsNullOrEmpty(entry.ChatColor)) tag.ChatColor = entry.ChatColor;
        if (!string.IsNullOrEmpty(entry.NameColor)) tag.NameColor = entry.NameColor;
        if (!string.IsNullOrEmpty(entry.ScoreTag))  tag.ScoreTag  = entry.ScoreTag;
        tag.ChatSound = entry.ChatSound;

        return HookResult.Continue;
    }

    private static bool IsValidPlayer(IPlayer? p)
        => p != null && p.IsValid && !p.IsFakeClient && p.SteamID != 0;
}
