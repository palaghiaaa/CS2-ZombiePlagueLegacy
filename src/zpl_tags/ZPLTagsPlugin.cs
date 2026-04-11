using Admins.Core.Contract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using System.Drawing;
using TagsApi;
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
    private ILogger<ZPLTagsPlugin>?       _logger;
    private IOptionsMonitor<ZPLTagsCFG>?  _cfgMonitor;
    private ZPLTagsCFG                    _config = new();
    private ServiceProvider?              _sp;

    // Shared interfaces — both are optional; the plugin degrades gracefully.
    private ITagApi?        _tagsApi;
    private IAdminsManager? _adminsManager;

    // SteamID64 → ALL eligible GroupTagEntry items, sorted desc by Priority.
    private readonly Dictionary<ulong, List<GroupTagEntry>> _eligibleTags = new();

    // SteamID64 → currently active entry.
    //   key present + non-null  → a specific tag is active
    //   key present + null      → player explicitly chose "no tag"
    //   key absent              → no eligible tags found yet
    private readonly Dictionary<ulong, GroupTagEntry?> _activeTag = new();

    private CancellationTokenSource? _refreshCts;

    // Must be shorter than cs2-tags' 1 s revalidation to avoid score-tag flicker.
    private const float ScoreTagRefreshInterval = 0.8f;
    private const string ConfigFile = "ZPLTagsCFG.jsonc";

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void Load(bool hotReload)
    {
        if (!hotReload)
            Core.Configuration.InitializeJsonWithModel<ZPLTagsCFG>(ConfigFile, "ZPLTags")
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
            // Rebuild eligibility for all connected players on config change.
            Core.Scheduler.NextWorldUpdate(RebuildAllPlayers);
        });

        Core.Event.OnClientConnected    += OnClientConnected;
        Core.Event.OnClientDisconnected += OnClientDisconnected;

        Core.Command.RegisterCommand(_config.MenuCommand, CmdTags, true);

        _refreshCts = new CancellationTokenSource();
        ScheduleScoreTagRefreshLoop(_refreshCts.Token);

        if (hotReload)
            Core.Scheduler.NextWorldUpdate(RebuildAllPlayers);
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        // ── cs2-tags ──────────────────────────────────────────────────────────
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
                _logger?.LogWarning("[ZPLTags] Tags API unavailable: {Error}", ex.Message);
            }
        }
        else
        {
            _logger?.LogWarning("[ZPLTags] Tags API not found – is cs2-tags loaded?");
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
            _logger?.LogInformation("[ZPLTags] Admins API not found – group-based tag matching disabled (permission-only mode).");
        }

        Core.Scheduler.NextWorldUpdate(RebuildAllPlayers);
    }

    public override void Unload()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;

        foreach (var player in Core.PlayerManager.GetAllPlayers())
        {
            if (player != null && player.IsValid)
                Core.MenusAPI.CloseActiveMenu(player);
        }

        if (_tagsApi != null)
        {
            _tagsApi.OnMessageProcessPre -= OnMessageProcessPre;
            _tagsApi = null;
        }

        if (_adminsManager != null)
        {
            _adminsManager.OnAdminLoad -= OnAdminLoad;
            _adminsManager = null;
        }

        Core.Event.OnClientConnected    -= OnClientConnected;
        Core.Event.OnClientDisconnected -= OnClientDisconnected;

        _eligibleTags.Clear();
        _activeTag.Clear();
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
            if (Core.Permission.PlayerHasPermission(player.SteamID, entry.Permission))
                return true;
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
        if (_tagsApi == null || !IsValidRealPlayer(player)) return;
        string score = entry != null ? (entry.ScoreTag ?? string.Empty) : string.Empty;
        _tagsApi.SetAttribute(player, TagType.ScoreTag, score);
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
    /// Computes eligible tags for one player and auto-selects the top-priority
    /// entry if the player has no active choice yet.
    /// Must run on the game thread.
    /// </summary>
    private void InitPlayer(IPlayer player)
    {
        ulong sid      = player.SteamID;
        var eligible   = BuildEligibleList(player);

        if (eligible.Count == 0)
        {
            _eligibleTags.Remove(sid);
            _activeTag.Remove(sid);
            return;
        }

        _eligibleTags[sid] = eligible;

        // Auto-select top-priority only if the player has no existing choice.
        if (!_activeTag.ContainsKey(sid))
        {
            _activeTag[sid] = eligible[0];
            ApplyScoreTag(player, eligible[0]);
        }
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

        // Two retries: one early (permissions already available) and one later
        // (waits for the Admins plugin to finish async data loading).
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
    }

    private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player == null) return;
        _eligibleTags.Remove(player.SteamID);
        _activeTag.Remove(player.SteamID);
    }

    // ── Chat interception ─────────────────────────────────────────────────────

    private HookResult OnMessageProcessPre(MessageProcess mp)
    {
        if (mp?.Player == null) return HookResult.Continue;
        if (!IsValidRealPlayer(mp.Player)) return HookResult.Continue;

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

        var menuCfg = new MenuConfiguration
        {
            Title                    = HtmlGradient.GenerateGradientText(_config.MenuTitle, Color.Gold, Color.Orange),
            FreezePlayer             = false,
            MaxVisibleItems          = 6,
            PlaySound                = true,
            AutoIncreaseVisibleItems = false,
            HideFooter               = false
        };

        IMenuAPI menu = Core.MenusAPI.CreateMenu(menuCfg, default, null, MenuOptionScrollStyle.LinearScroll);

        // ── One button per eligible tag ───────────────────────────────────────
        foreach (var entry in eligible)
        {
            var capturedEntry = entry;   // capture for async closure

            bool isActive = current != null &&
                            string.Equals(current.GroupName, entry.GroupName, StringComparison.Ordinal) &&
                            string.Equals(current.Permission, entry.Permission, StringComparison.Ordinal);

            string label = isActive ? $"✓ {entry.GetMenuLabel()}" : entry.GetMenuLabel();

            var btn = new ButtonMenuOption(label)
            {
                TextStyle       = MenuOptionTextStyle.ScrollLeftLoop,
                CloseAfterClick = true,
                Tag             = capturedEntry
            };

            btn.Click += async (_, args) =>
            {
                var clicker = args.Player;
                Core.Scheduler.NextTick(() =>
                {
                    if (!IsValidRealPlayer(clicker)) return;

                    _activeTag[clicker.SteamID] = capturedEntry;

                    if (_tagsApi != null)
                    {
                        _tagsApi.SetAttribute(clicker, TagType.ScoreTag,  capturedEntry.ScoreTag  ?? string.Empty);
                        _tagsApi.SetAttribute(clicker, TagType.ChatTag,   capturedEntry.ChatTag   ?? string.Empty);
                        _tagsApi.SetAttribute(clicker, TagType.ChatColor, capturedEntry.ChatColor ?? string.Empty);
                        _tagsApi.SetAttribute(clicker, TagType.NameColor, capturedEntry.NameColor ?? string.Empty);
                        _tagsApi.SetPlayerChatSound(clicker, capturedEntry.ChatSound);
                    }

                    Chat(clicker, "TagSelected", capturedEntry.GetMenuLabel());
                });
            };

            menu.AddOption(btn);
        }

        // ── "Remove tag" button ───────────────────────────────────────────────
        // Shows ✓ when no tag is currently active.
        string removeLabel = (current == null)
            ? $"✓ {_config.NoTagLabel}"
            : _config.NoTagLabel;

        var removeBtn = new ButtonMenuOption(removeLabel)
        {
            TextStyle       = MenuOptionTextStyle.ScrollLeftLoop,
            CloseAfterClick = true
        };

        removeBtn.Click += async (_, args) =>
        {
            var clicker = args.Player;
            Core.Scheduler.NextTick(() =>
            {
                if (!IsValidRealPlayer(clicker)) return;

                _activeTag[clicker.SteamID] = null;   // null = "no tag"

                if (_tagsApi != null)
                {
                    _tagsApi.SetAttribute(clicker, TagType.ScoreTag,  string.Empty);
                    _tagsApi.SetAttribute(clicker, TagType.ChatTag,   string.Empty);
                    _tagsApi.SetAttribute(clicker, TagType.ChatColor, string.Empty);
                    _tagsApi.SetAttribute(clicker, TagType.NameColor, string.Empty);
                }

                Chat(clicker, "TagRemoved");
            });
        };

        menu.AddOption(removeBtn);

        Core.MenusAPI.OpenMenuForPlayer(player, menu);
    }
}
