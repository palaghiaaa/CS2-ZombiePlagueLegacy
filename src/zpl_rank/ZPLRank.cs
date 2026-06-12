using System.Drawing;
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
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;
using ZombiePlagueLegacyCS2;
using ZombiePlagueLegacyCS2.SharedUi;

namespace ZPLRank;

[PluginMetadata(
    Id = "ZPLRank",
    Version = "2.0",
    Name = "ZPL Rank & Top",
    Author = "ZombiePlagueLegacy",
    Description = "Rank and Top stats plugin for ZombiePlagueLegacy CS2. Tracks kills, deaths, infections, assists and damage.")]
public class ZPLRankPlugin(ISwiftlyCore core) : BasePlugin(core)
{
    // Per-session stats keyed by SteamID.  Populated from DB on load, merged
    // with live data during the session, and flushed back to DB on saves.
    private readonly Dictionary<ulong, PlayerStat> _stats = new();

    private ServiceProvider?   _sp;
    private IOptionsMonitor<ZPLRankCFG>? _cfg;
    private ZPLRankDatabase?    _db;

    // ZPL API — used for ZPL_IsZombie() and ZPL_OnPlayerInfect event.
    private IZombiePlagueLegacyAPI? _zplApi;

    public class PlayerStat
    {
        public string Name       { get; set; } = string.Empty;
        public int    Kills      { get; set; }
        public int    Deaths     { get; set; }
        public int    Infections { get; set; }
        public int    Assists    { get; set; }
        public long   Damage     { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Plugin lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private const string ConfigFile = "ZPLRankCFG.jsonc";

    public override void Load(bool hotReload)
    {
        // Bind config from configs/plugins/ZPLRank/ZPLRankCFG.jsonc
        // reloadOnChange: true ensures IOptionsMonitor reflects edits made to
        // the file at runtime (chat tag, weights, top list size, etc.).
        // Guard with !hotReload: SwiftlyS2's PluginConfigurationService.Manager is a
        // lazy singleton that is never reset between reloads.  Calling AddJsonFile on
        // it again on every Load() appends a new FileSystemWatcher thread to the same
        // ConfigurationManager, leaking one watcher thread per map change.
        if (!hotReload)
            Core.Configuration.InitializeJsonWithModel<ZPLRankCFG>(ConfigFile, "ZPLRankCFG")
                .Configure(builder =>
                {
                    builder.AddJsonFile(ConfigFile, false, true);
                    builder.SetFileLoadExceptionHandler(ctx =>
                    {
                        Core.Logger.LogError("[ZPLRank] Failed to load {File}: {Error}. Using last valid configuration.", ConfigFile, ctx.Exception.Message);
                        ctx.Ignore = true;
                    });
                });

        var collection = new ServiceCollection();
        collection.AddSwiftly(Core);
        collection.AddOptions<ZPLRankCFG>().BindConfiguration("ZPLRankCFG");
        collection.AddSingleton<ZPLRankDatabase>();
        _sp  = collection.BuildServiceProvider();
        _cfg = _sp.GetRequiredService<IOptionsMonitor<ZPLRankCFG>>();

        // ── MySQL database ────────────────────────────────────────────────────
        _db = _sp.GetRequiredService<ZPLRankDatabase>();
        _db.EnsureSchema(_cfg.CurrentValue.DatabaseConnection);
        _db.LoadAll(_stats);   // Pre-populate with historical stats.

        // ── Event hooks ───────────────────────────────────────────────────────
        Core.GameEvent.HookPre<EventPlayerDeath>(OnPlayerDeath);
        Core.GameEvent.HookPre<EventRoundEnd>(OnRoundEnd);
#pragma warning disable CS0618 // IOnEntityTakeDamageEvent: deprecated by SwiftlyS2 1.4, migration pending
        Core.Event.OnEntityTakeDamage  += OnEntityTakeDamage;
#pragma warning restore CS0618
        Core.Event.OnClientConnected   += OnClientConnected;
        Core.Event.OnClientDisconnected += OnClientDisconnected;

        RegisterCommands(_cfg.CurrentValue);
    }

    /// <summary>
    /// Connects to IZombiePlagueLegacyAPI for accurate zombie-state detection
    /// and the ZPL_OnPlayerInfect event.
    /// </summary>
    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        if (interfaceManager.HasSharedInterface("ZombiePlagueLegacy"))
        {
            try
            {
                _zplApi = interfaceManager.GetSharedInterface<IZombiePlagueLegacyAPI>("ZombiePlagueLegacy");
                if (_zplApi != null)
                {
                    _zplApi.ZPL_OnPlayerInfect += OnZPLPlayerInfect;
                }
            }
            catch (InvalidOperationException ex)
            {
                Core.Logger.LogWarning("[ZPLRank] Failed to acquire ZombiePlagueLegacyCS2 API: {Error} – infections will not be tracked.", ex.Message);
            }
        }
        else
        {
            Core.Logger.LogWarning("[ZPLRank] ZombiePlagueLegacyCS2 API not found – infections will not be tracked.");
        }
    }

    public override void Unload()
    {
        if (_zplApi != null)
            _zplApi.ZPL_OnPlayerInfect -= OnZPLPlayerInfect;

        // Close all open menus before unloading so that SwiftlyS2's per-player
        // render timers cannot fire on freed objects after hot-reload.
        foreach (var player in Core.PlayerManager.GetAllPlayers())
        {
            if (player != null && player.IsValid)
                Core.MenusAPI.CloseActiveMenu(player);
        }

        // Unregister all event hooks so that stale delegates from the previous
        // load do not accumulate on hot-reload (map change) and cause double
        // event processing or memory leaks.
        Core.GameEvent.UnhookPre<EventPlayerDeath>();
        Core.GameEvent.UnhookPre<EventRoundEnd>();
#pragma warning disable CS0618
        Core.Event.OnEntityTakeDamage   -= OnEntityTakeDamage;
#pragma warning restore CS0618
        Core.Event.OnClientConnected    -= OnClientConnected;
        Core.Event.OnClientDisconnected -= OnClientDisconnected;

        // Flush all in-memory stats to DB before shutdown.
        _db?.SaveAll(_stats);

        _stats.Clear();
        _sp?.Dispose();
        _sp = null;
    }

    private void RegisterCommands(ZPLRankCFG cfg)
    {
        if (cfg.EnableRankCommand)
            Core.Command.RegisterCommand(cfg.RankCommand, OnRankCommand, true);

        if (cfg.EnableTopCommands)
        {
            Core.Command.RegisterCommand(cfg.TopCommand,   OnTopCommand,   true);
            Core.Command.RegisterCommand(cfg.Top15Command, OnTop15Command, true);
            Core.Command.RegisterCommand(cfg.Top10Command, OnTop10Command, true);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private string T(IPlayer player, string key, params object[] args)
    {
        var loc = Core.Translation.GetPlayerLocalizer(player);
        return args.Length == 0 ? loc[key] : loc[key, args];
    }

    /// <summary>Returns true when the player is a valid non-bot player with a non-zero SteamID.</summary>
    private static bool IsValidRealPlayer(IPlayer? player)
        => player != null && player.IsValid && !player.IsFakeClient && player.SteamID != 0;

    /// <summary>Returns true when the player is on the zombie side.</summary>
    private bool IsZombie(int playerId)
    {
        if (_zplApi != null)
            return _zplApi.ZPL_IsZombie(playerId);
        // Fallback: T-side = zombie in a standard ZPL server.
        var p = Core.PlayerManager.GetPlayer(playerId);
        return p?.Controller?.Team == Team.T;
    }

    private PlayerStat GetOrCreate(ulong steamId, string name)
    {
        if (!_stats.TryGetValue(steamId, out var s))
            _stats[steamId] = s = new PlayerStat();
        s.Name = name;
        return s;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Score formula
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes the KDA-style score for a player:
    /// <c>(Kills×KW + Infections×IW + Assists×AW + Damage/DD) / max(Deaths,1)</c>
    /// </summary>
    private static double ComputeScore(PlayerStat s, ZPLRankCFG cfg)
    {
        double positive = s.Kills      * cfg.KillWeight
                        + s.Infections * cfg.InfectionWeight
                        + s.Assists    * cfg.AssistWeight
                        + (cfg.DamageDivisor > 0 ? (double)s.Damage / cfg.DamageDivisor : 0.0);
        return positive / Math.Max(s.Deaths, 1);
    }

    /// <summary>Returns the score formatted to 2 decimal places for display.</summary>
    private static string FormatScore(double score) => score.ToString("F2");

    /// <summary>Returns all stats sorted by score descending.</summary>
    private List<KeyValuePair<ulong, PlayerStat>> BuildSortedList()
    {
        var cfg = _cfg!.CurrentValue;
        return _stats
            .OrderByDescending(x => ComputeScore(x.Value, cfg))
            .ThenByDescending(x => x.Value.Kills)
            .ThenByDescending(x => x.Value.Infections)
            .ThenByDescending(x => x.Value.Assists)
            .ThenByDescending(x => x.Value.Damage)
            .ThenBy(x => x.Value.Deaths)
            .ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Stat tracking
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When a player connects, merge their stored DB row into <c>_stats</c>
    /// (additive: adds DB values on top of any in-memory values that might
    /// already exist from a late reconnect within the same session).
    /// </summary>
    private void OnClientConnected(IOnClientConnectedEvent @event)
    {
        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (!IsValidRealPlayer(player)) return;

        ulong sid = player!.SteamID;
        var stored = _db?.Load(sid);
        if (stored == null) return;

        // Merge strategy: take Math.Max of each stat so that in-session kills
        // earned before a reconnect are not discarded, and a manual DB correction
        // (zeroing stats) takes effect only when the in-memory value is also zero.
        // This is intentional: the in-memory session is always the most up-to-date
        // source; the DB is the floor, not the ceiling.
        var s = GetOrCreate(sid, stored.Name);
        s.Kills      = Math.Max(s.Kills,      stored.Kills);
        s.Deaths     = Math.Max(s.Deaths,     stored.Deaths);
        s.Infections = Math.Max(s.Infections, stored.Infections);
        s.Assists    = Math.Max(s.Assists,     stored.Assists);
        s.Damage     = Math.Max(s.Damage,     stored.Damage);
    }

    /// <summary>Persists the disconnecting player's stats to the database.</summary>
    private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        // Use the PlayerId → look up SteamID from our stats dictionary by iterating,
        // since the player object may already be invalid at this point.
        // The cleanest approach is to look up by PlayerId from PlayerManager first.
        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        bool isValid = player != null && player.IsValid;

        // Close any open menu immediately so SwiftlyS2's per-player render timer
        // cannot fire on an already-freed native player controller and crash the
        // server with SIGSEGV (BuildMenuHtml null-dereference).
        if (isValid)
            Core.MenusAPI.CloseActiveMenu(player!);

        ulong? sid = isValid ? player!.SteamID : null;

        // Fall back: scan _stats for the first entry whose Name matches if needed.
        // This is not needed when the PlayerManager still holds the reference.
        if (sid == null || sid == 0) return;

        if (_stats.TryGetValue(sid.Value, out var stat))
            _db?.Save(sid.Value, stat);
    }

    /// <summary>Round-end safety flush: persists all in-memory stats.</summary>
    private HookResult OnRoundEnd(EventRoundEnd @event)
    {
        _db?.SaveAll(_stats);
        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event)
    {
        // ── Victim: record death ──────────────────────────────────────────────
        var victim = @event.UserIdPlayer;
        if (IsValidRealPlayer(victim))
            GetOrCreate(victim!.SteamID, victim.Name).Deaths++;

        // ── Attacker: record kill (human kills only; infections via ZPL event) ─
        var attacker = @event.AttackerPlayer;
        if (IsValidRealPlayer(attacker))
        {
            ulong aSteam = attacker!.SteamID;
            ulong vSteam = victim?.SteamID ?? 0;
            if (aSteam != vSteam && !IsZombie(attacker.PlayerID))
                GetOrCreate(aSteam, attacker.Name).Kills++;
        }

        // ── Assister ─────────────────────────────────────────────────────────
        var assister = @event.AssisterPlayer;
        if (IsValidRealPlayer(assister))
            GetOrCreate(assister!.SteamID, assister.Name).Assists++;

        return HookResult.Continue;
    }

    /// <summary>
    /// Fired by IZombiePlagueLegacyAPI when a zombie player infects a human.
    /// Counts one infection for the attacker.
    /// </summary>
    private void OnZPLPlayerInfect(IPlayer attacker, IPlayer _victim, bool _grenade, string _zombieClass)
    {
        if (!IsValidRealPlayer(attacker)) return;
        GetOrCreate(attacker.SteamID, attacker.Name).Infections++;
    }

    /// <summary>
    /// Accumulates damage dealt by any player to any other player.
    /// Both zombie-to-human and human-to-zombie damage is counted.
    /// </summary>
#pragma warning disable CS0618
    private void OnEntityTakeDamage(IOnEntityTakeDamageEvent @event)
    {
        // Victim must be a valid player pawn.
        var victimPawn = @event.Entity?.As<CCSPlayerPawn>();
        if (victimPawn == null || !victimPawn.IsValid) return;

        // Attacker must also be a valid player pawn (skip world/fall damage).
        var attackerPawn = @event.Info.Attacker.Value?.As<CCSPlayerPawn>();
        if (attackerPawn == null || !attackerPawn.IsValid) return;

        var attackerCtrl = attackerPawn.Controller.Value?.As<CCSPlayerController>();
        if (attackerCtrl == null || !attackerCtrl.IsValid) return;

        var attackerPlayer = Core.PlayerManager.GetPlayerFromController(attackerCtrl);
        if (!IsValidRealPlayer(attackerPlayer)) return;

        ulong aSteam = attackerPlayer!.SteamID;

        // Skip self-damage.
        var victimCtrl = victimPawn.Controller.Value?.As<CCSPlayerController>();
        if (victimCtrl != null && victimCtrl.IsValid)
        {
            var victimPlayer = Core.PlayerManager.GetPlayerFromController(victimCtrl);
            if (victimPlayer != null && victimPlayer.SteamID == aSteam) return;
        }

        int dmg = (int)@event.Info.Damage;
        if (dmg <= 0) return;

        GetOrCreate(aSteam, attackerPlayer.Name).Damage += dmg;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  !rank command
    // ─────────────────────────────────────────────────────────────────────────

    private void OnRankCommand(ICommandContext context)
    {
        var player = context.Sender;
        if (!IsValidRealPlayer(player)) return;
        var p = player!;

        ulong steamId = p.SteamID;
        var myStat = GetOrCreate(steamId, p.Name);
        var sorted = BuildSortedList();
        int rank  = sorted.FindIndex(kvp => kvp.Key == steamId) + 1;
        int total = sorted.Count;

        var cfg   = _cfg!.CurrentValue;
        double score = ComputeScore(myStat, cfg);
        string body = T(p, "RankMessage",
            myStat.Name, rank, total,
            myStat.Kills, myStat.Deaths,
            myStat.Infections, myStat.Assists, myStat.Damage,
            FormatScore(score));
        p.SendMessage(MessageType.Chat, $" {cfg.ChatTag} {body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  !top / !top15 / !top10 commands
    // ─────────────────────────────────────────────────────────────────────────

    private void OnTopCommand(ICommandContext context)
        => OpenTopMenu(context.Sender, _cfg!.CurrentValue.TopListSize);

    private void OnTop15Command(ICommandContext context) => OpenTopMenu(context.Sender, 15);
    private void OnTop10Command(ICommandContext context)  => OpenTopMenu(context.Sender, 10);

    private void OpenTopMenu(IPlayer? player, int limit)
    {
        if (!IsValidRealPlayer(player)) return;
        var p = player!;

        var cfg    = _cfg!.CurrentValue;
        var sorted = BuildSortedList().Take(limit).ToList();

        var menuConfig = ZPLMenuStyle.MenuConfig(T(p, "TopMenuTitle", limit), playSound: true);

        IMenuAPI menu = Core.MenusAPI.CreateMenu(menuConfig, default, null, MenuOptionScrollStyle.LinearScroll);

        if (sorted.Count == 0)
        {
            menu.AddOption(ZPLMenuStyle.Text(T(p, "TopMenuNoStats")));
        }
        else
        {
            for (int i = 0; i < sorted.Count; i++)
            {
                var s      = sorted[i].Value;
                double sc  = ComputeScore(s, cfg);
                string lbl = T(p, "TopMenuEntry",
                    i + 1, s.Name,
                    FormatScore(sc),
                    s.Kills, s.Deaths,
                    s.Infections, s.Assists, s.Damage);

                menu.AddOption(ZPLMenuStyle.Text(lbl, ZPLMenuStyle.ColButton, bold: true));
            }
        }

        Core.MenusAPI.OpenMenuForPlayer(p, menu);
    }
}
