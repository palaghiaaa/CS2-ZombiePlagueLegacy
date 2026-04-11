using Admins.Core.Contract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using TagsApi;
using static TagsApi.Tags;

namespace ZPLTags;

[PluginMetadata(
    Id          = "ZPLTags",
    Version     = "1.0.0",
    Name        = "ZPL Tags",
    Author      = "DeadPoolCS2",
    Description = "Bridges the Admins plugin and cs2-tags: applies ChatTag/ChatColor/NameColor/ScoreTag " +
                  "overrides for each Admins-plugin group without requiring SwiftlyS2 native permissions.")]
public class ZPLTagsPlugin(ISwiftlyCore core) : BasePlugin(core)
{
    private ILogger<ZPLTagsPlugin>?        _logger;
    private IOptionsMonitor<ZPLTagsCFG>?   _cfgMonitor;
    private ZPLTagsCFG                     _config = new();
    private ServiceProvider?               _sp;

    // Shared interfaces from other plugins (acquired in UseSharedInterface).
    private ITagApi?       _tagsApi;
    private IAdminsManager? _adminsManager;

    // SteamID64 → best-matching GroupTagEntry for that admin.
    // Populated on admin load / player connect.  Cleared on disconnect.
    // All reads and writes happen on the game (world-update) thread.
    private readonly Dictionary<ulong, GroupTagEntry> _adminTagCache = new();

    // Cancellation for the periodic ScoreTag re-apply loop.
    private CancellationTokenSource? _refreshCts;

    // How often to re-stamp admin ScoreTags.  Must be shorter than the cs2-tags
    // revalidation interval (1 s) so the scoreboard never shows the wrong tag.
    private const float ScoreTagRefreshInterval = 0.8f;

    private const string ConfigFile = "ZPLTagsCFG.jsonc";

    // ── Plugin lifecycle ──────────────────────────────────────────────────────

    public override void Load(bool hotReload)
    {
        // Guard with !hotReload: SwiftlyS2's PluginConfigurationService.Manager is a
        // lazy singleton that is never reset between reloads.  Calling AddJsonFile on
        // it again on every Load() appends a new FileSystemWatcher thread to the same
        // ConfigurationManager, leaking one watcher thread per map change.
        if (!hotReload)
            Core.Configuration.InitializeJsonWithModel<ZPLTagsCFG>(ConfigFile, "ZPLTags")
                .Configure(builder =>
                {
                    builder.AddJsonFile(ConfigFile, false, true);
                    builder.SetFileLoadExceptionHandler(ctx =>
                    {
                        Core.Logger.LogError(
                            "[ZPLTags] Failed to load {File}: {Error}. Using last valid configuration.",
                            ConfigFile, ctx.Exception.Message);
                        ctx.Ignore = true;
                    });
                });

        var services = new ServiceCollection();
        services.AddSwiftly(Core);
        services.AddSingleton<ISwiftlyCore>(Core);
        services.AddOptions<ZPLTagsCFG>().BindConfiguration("ZPLTags");

        _sp         = services.BuildServiceProvider();
        _logger     = _sp.GetRequiredService<ILogger<ZPLTagsPlugin>>();
        _cfgMonitor = _sp.GetRequiredService<IOptionsMonitor<ZPLTagsCFG>>();
        _config     = _cfgMonitor.CurrentValue;
        _cfgMonitor.OnChange(cfg =>
        {
            _config = cfg;
            // Rebuild the cache so priority changes / new groups take effect immediately.
            Core.Scheduler.NextWorldUpdate(RebuildCacheForAllConnectedPlayers);
        });

        Core.Event.OnClientConnected    += OnClientConnected;
        Core.Event.OnClientDisconnected += OnClientDisconnected;

        // Start the ScoreTag refresh loop.
        _refreshCts = new CancellationTokenSource();
        ScheduleScoreTagRefreshLoop(_refreshCts.Token);

        if (hotReload)
            Core.Scheduler.NextWorldUpdate(RebuildCacheForAllConnectedPlayers);
    }

    /// <summary>
    /// Called after all plugins have published their shared interfaces.
    /// Connects to ITagApi (cs2-tags) and IAdminsManager (Admins plugin).
    /// </summary>
    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        // ── cs2-tags API ──────────────────────────────────────────────────────
        if (interfaceManager.HasSharedInterface("Tags.Api"))
        {
            try
            {
                _tagsApi = interfaceManager.GetSharedInterface<ITagApi>("Tags.Api");
                if (_tagsApi != null)
                {
                    _tagsApi.OnMessageProcessPre += OnMessageProcessPre;
                    _logger?.LogInformation("[ZPLTags] Connected to Tags API (cs2-tags).");
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger?.LogWarning("[ZPLTags] Failed to acquire Tags API: {Error} – chat tag overrides disabled.", ex.Message);
            }
        }
        else
        {
            _logger?.LogWarning("[ZPLTags] Tags API (\"Tags.Api\") not found – chat tag overrides disabled. Is cs2-tags loaded?");
        }

        // ── Admins plugin API ─────────────────────────────────────────────────
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
                _logger?.LogWarning("[ZPLTags] Failed to acquire Admins API: {Error} – admin tag detection disabled.", ex.Message);
            }
        }
        else
        {
            _logger?.LogWarning("[ZPLTags] Admins API (\"Admins.Admins.V1\") not found – admin tag detection disabled. Is the Admins plugin loaded?");
        }

        // Populate cache now that both APIs are wired up.
        Core.Scheduler.NextWorldUpdate(RebuildCacheForAllConnectedPlayers);
    }

    public override void Unload()
    {
        // Cancel the ScoreTag refresh loop before any other cleanup.
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;

        // Unsubscribe from ITagApi events.
        if (_tagsApi != null)
        {
            _tagsApi.OnMessageProcessPre -= OnMessageProcessPre;
            _tagsApi = null;
        }

        // Unsubscribe from Admins events.
        if (_adminsManager != null)
        {
            _adminsManager.OnAdminLoad -= OnAdminLoad;
            _adminsManager = null;
        }

        Core.Event.OnClientConnected    -= OnClientConnected;
        Core.Event.OnClientDisconnected -= OnClientDisconnected;

        _adminTagCache.Clear();
        _sp?.Dispose();
        _sp = null;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool IsValidRealPlayer(IPlayer? player)
        => player != null && player.IsValid && !player.IsFakeClient && player.SteamID != 0;

    /// <summary>
    /// Returns the highest-priority <see cref="GroupTagEntry"/> from the current
    /// config that matches any of <paramref name="admin"/>'s group names, or
    /// <c>null</c> when no match exists.
    /// </summary>
    private GroupTagEntry? FindBestEntry(IAdmin admin)
    {
        var cfg = _config;
        GroupTagEntry? best = null;

        foreach (var groupName in admin.Groups)
        {
            foreach (var entry in cfg.GroupTags)
            {
                if (!string.Equals(entry.GroupName, groupName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (best == null || entry.Priority > best.Priority)
                    best = entry;
            }
        }

        return best;
    }

    /// <summary>
    /// Applies a ScoreTag to <paramref name="player"/> via the Tags API.
    /// Must be called on the game (world-update) thread.
    /// </summary>
    private void ApplyScoreTag(IPlayer player, GroupTagEntry entry)
    {
        if (_tagsApi == null) return;
        if (!IsValidRealPlayer(player)) return;

        // SetAttribute writes player.Controller.Clan (and fires ClanUpdated +
        // EventNextlevelChanged to refresh the scoreboard).
        // Note: cs2-tags' 1-second revalidation loop will reset this value for
        // players without native SwiftlyS2 permissions.  The ScoreTagRefreshLoop
        // re-applies it at a shorter interval (ScoreTagRefreshInterval) to ensure
        // the scoreboard always shows the correct admin tag.
        _tagsApi.SetAttribute(player, TagType.ScoreTag, entry.ScoreTag ?? string.Empty);
    }

    // ── ScoreTag periodic re-apply loop ──────────────────────────────────────

    private void ScheduleScoreTagRefreshLoop(CancellationToken ct)
    {
        Core.Scheduler.DelayBySeconds(ScoreTagRefreshInterval, () =>
        {
            if (ct.IsCancellationRequested) return;

            Core.Scheduler.NextWorldUpdate(() =>
            {
                if (ct.IsCancellationRequested) return;

                // Re-apply the ScoreTag for every cached admin so the cs2-tags
                // revalidation loop cannot permanently strip admin score tags.
                foreach (var player in Core.PlayerManager.GetAllPlayers())
                {
                    if (!IsValidRealPlayer(player)) continue;
                    if (!_adminTagCache.TryGetValue(player!.SteamID, out var entry)) continue;
                    if (!string.IsNullOrEmpty(entry.ScoreTag))
                        ApplyScoreTag(player, entry);
                }

                ScheduleScoreTagRefreshLoop(ct);
            });
        });
    }

    // ── Cache management ──────────────────────────────────────────────────────

    private void RebuildCacheForAllConnectedPlayers()
    {
        if (_adminsManager == null) return;

        _adminTagCache.Clear();

        foreach (var player in Core.PlayerManager.GetAllPlayers())
        {
            if (!IsValidRealPlayer(player)) continue;

            var admin = _adminsManager.GetAdmin(player);
            if (admin == null) continue;

            var entry = FindBestEntry(admin);
            if (entry == null) continue;

            _adminTagCache[player!.SteamID] = entry;
            ApplyScoreTag(player, entry);
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    /// <summary>
    /// Fired by the Admins plugin when an admin's data has been loaded for a
    /// connected player.  May be invoked from a background thread; all mutable
    /// state is touched only inside a NextWorldUpdate callback.
    /// </summary>
    private void OnAdminLoad(IPlayer player, IAdmin admin)
    {
        if (player == null || player.SteamID == 0) return;

        ulong steamId = player.SteamID;

        // FindBestEntry reads only cfg and admin.Groups (both safe from any thread).
        var entry = FindBestEntry(admin);

        Core.Scheduler.NextWorldUpdate(() =>
        {
            if (!IsValidRealPlayer(player)) return;

            if (entry == null)
            {
                _adminTagCache.Remove(steamId);
                return;
            }

            _adminTagCache[steamId] = entry;
            ApplyScoreTag(player, entry);
        });
    }

    /// <summary>
    /// Fallback path for players who already have an admin record when they
    /// connect (e.g. cached from a previous map).  We retry after a short delay
    /// to allow the Admins plugin to finish its async data load.
    /// </summary>
    private void OnClientConnected(IOnClientConnectedEvent @event)
    {
        int playerId = @event.PlayerId;

        // Retry after a short delay so the Admins plugin can finish loading
        // the player's admin record asynchronously.
        Core.Scheduler.DelayBySeconds(1.5f, () =>
        {
            Core.Scheduler.NextWorldUpdate(() =>
            {
                if (_adminsManager == null) return;

                var player = Core.PlayerManager.GetPlayer(playerId);
                if (!IsValidRealPlayer(player)) return;

                var admin = _adminsManager.GetAdmin(player!);
                if (admin == null) return;

                var entry = FindBestEntry(admin);
                if (entry == null) return;

                _adminTagCache[player!.SteamID] = entry;
                ApplyScoreTag(player, entry);
            });
        });
    }

    private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player != null)
            _adminTagCache.Remove(player.SteamID);
    }

    // ── Tags API: chat-message interception ───────────────────────────────────

    /// <summary>
    /// Intercepts every chat message before cs2-tags renders it.
    /// Overrides the Tag in <paramref name="mp"/> with the admin's configured
    /// tag so the correct ChatTag/ChatColor/NameColor/ScoreTag appears in chat
    /// regardless of the cs2-tags revalidation state.
    /// </summary>
    private HookResult OnMessageProcessPre(MessageProcess mp)
    {
        if (mp?.Player == null) return HookResult.Continue;
        if (!IsValidRealPlayer(mp.Player)) return HookResult.Continue;

        if (!_adminTagCache.TryGetValue(mp.Player.SteamID, out var entry))
            return HookResult.Continue;

        // Modify in place – cs2-tags reads mp.Tag after all OnMessageProcessPre
        // handlers have returned.
        var tag = mp.Tag;

        if (!string.IsNullOrEmpty(entry.ChatTag))
            tag.ChatTag = entry.ChatTag;

        if (!string.IsNullOrEmpty(entry.ChatColor))
            tag.ChatColor = entry.ChatColor;

        if (!string.IsNullOrEmpty(entry.NameColor))
            tag.NameColor = entry.NameColor;

        if (!string.IsNullOrEmpty(entry.ScoreTag))
            tag.ScoreTag = entry.ScoreTag;

        tag.ChatSound = entry.ChatSound;

        return HookResult.Continue;
    }
}
