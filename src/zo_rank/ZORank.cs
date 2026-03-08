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
using ZombieOutstandingCS2;

namespace ZORank;

[PluginMetadata(
    Id = "ZORank",
    Version = "2.0",
    Name = "ZO Rank & Top",
    Author = "ZombieOutstanding",
    Description = "Rank and Top stats plugin for ZombieOutstanding CS2. Tracks kills, deaths, infections, assists and damage.")]
public class ZORankPlugin(ISwiftlyCore core) : BasePlugin(core)
{
    // Per-session stats keyed by SteamID.  Populated from DB on load, merged
    // with live data during the session, and flushed back to DB on saves.
    private readonly Dictionary<ulong, PlayerStat> _stats = new();

    private ServiceProvider?   _sp;
    private IOptionsMonitor<ZORankCFG>? _cfg;
    private ZORankDatabase?    _db;

    // ZO API — used for ZO_IsZombie() and ZO_OnPlayerInfect event.
    private IZombieOutstandingAPI? _zoApi;

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

    private const string ConfigFile = "ZORankCFG.jsonc";

    public override void Load(bool hotReload)
    {
        // Bind config from configs/plugins/ZORank/ZORankCFG.jsonc
        // reloadOnChange: true ensures IOptionsMonitor reflects edits made to
        // the file at runtime (chat tag, weights, top list size, etc.).
        Core.Configuration.InitializeJsonWithModel<ZORankCFG>(ConfigFile, "ZORankCFG")
            .Configure(builder =>
            {
                builder.AddJsonFile(ConfigFile, false, true);
            });

        var collection = new ServiceCollection();
        collection.AddSwiftly(Core);
        collection.AddOptions<ZORankCFG>().BindConfiguration("ZORankCFG");
        collection.AddSingleton<ZORankDatabase>();
        _sp  = collection.BuildServiceProvider();
        _cfg = _sp.GetRequiredService<IOptionsMonitor<ZORankCFG>>();

        // ── SQLite database ───────────────────────────────────────────────────
        _db = _sp.GetRequiredService<ZORankDatabase>();
        _db.EnsureSchema(_cfg.CurrentValue.DatabasePath);
        _db.LoadAll(_stats);   // Pre-populate with historical stats.

        // ── Event hooks ───────────────────────────────────────────────────────
        Core.GameEvent.HookPre<EventPlayerDeath>(OnPlayerDeath);
        Core.GameEvent.HookPre<EventRoundEnd>(OnRoundEnd);
        Core.Event.OnEntityTakeDamage  += OnEntityTakeDamage;
        Core.Event.OnClientConnected   += OnClientConnected;
        Core.Event.OnClientDisconnected += OnClientDisconnected;

        RegisterCommands(_cfg.CurrentValue);

        _cfg.OnChange(_ => Core.Logger.LogInformation("[ZORank] Configuration hot-reloaded."));

        Core.Logger.LogInformation("[ZORank] Loaded. Commands: !{Rank}, !{Top}, !{Top15}, !{Top10}",
            _cfg.CurrentValue.RankCommand,
            _cfg.CurrentValue.TopCommand,
            _cfg.CurrentValue.Top15Command,
            _cfg.CurrentValue.Top10Command);
    }

    /// <summary>
    /// Connects to IZombieOutstandingAPI for accurate zombie-state detection
    /// and the ZO_OnPlayerInfect event.
    /// </summary>
    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        if (interfaceManager.HasSharedInterface("ZombieOutstanding"))
        {
            _zoApi = interfaceManager.GetSharedInterface<IZombieOutstandingAPI>("ZombieOutstanding");
            if (_zoApi != null)
            {
                _zoApi.ZO_OnPlayerInfect += OnZOPlayerInfect;
                Core.Logger.LogInformation("[ZORank] ZombieOutstandingCS2 API connected.");
            }
        }
        else
        {
            Core.Logger.LogWarning("[ZORank] ZombieOutstandingCS2 API not found – infections will not be tracked.");
        }
    }

    public override void Unload()
    {
        if (_zoApi != null)
            _zoApi.ZO_OnPlayerInfect -= OnZOPlayerInfect;

        // Flush all in-memory stats to DB before shutdown.
        _db?.SaveAll(_stats);

        _stats.Clear();
        _sp?.Dispose();
        _sp = null;
    }

    private void RegisterCommands(ZORankCFG cfg)
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
        if (_zoApi != null)
            return _zoApi.ZO_IsZombie(playerId);
        // Fallback: T-side = zombie in a standard ZO server.
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
    private static double ComputeScore(PlayerStat s, ZORankCFG cfg)
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

        // ── Attacker: record kill (human kills only; infections via ZO event) ─
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
    /// Fired by IZombieOutstandingAPI when a zombie player infects a human.
    /// Counts one infection for the attacker.
    /// </summary>
    private void OnZOPlayerInfect(IPlayer attacker, IPlayer _victim, bool _grenade, string _zombieClass)
    {
        if (!IsValidRealPlayer(attacker)) return;
        GetOrCreate(attacker.SteamID, attacker.Name).Infections++;
    }

    /// <summary>
    /// Accumulates damage dealt by any player to any other player.
    /// Both zombie-to-human and human-to-zombie damage is counted.
    /// </summary>
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
        p.SendMessage(MessageType.Chat, $" \x04{cfg.ChatTag}\x01 {body}");
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

        var menuConfig = new MenuConfiguration
        {
            Title           = HtmlGradient.GenerateGradientText(T(p, "TopMenuTitle", limit), Color.Gold),
            FreezePlayer    = false,
            MaxVisibleItems = Math.Clamp(cfg.TopMenuVisibleRows, 1, 5),
            PlaySound       = true,
            AutoIncreaseVisibleItems = false,
            HideFooter      = false
        };

        IMenuAPI menu = Core.MenusAPI.CreateMenu(menuConfig, default, null, MenuOptionScrollStyle.LinearScroll);

        if (sorted.Count == 0)
        {
            menu.AddOption(new TextMenuOption(T(p, "TopMenuNoStats"),
                updateIntervalMs: 600, pauseIntervalMs: 100)
                { TextStyle = MenuOptionTextStyle.ScrollLeftLoop });
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

                menu.AddOption(new TextMenuOption(lbl,
                    updateIntervalMs: 600, pauseIntervalMs: 100)
                    { TextStyle = MenuOptionTextStyle.ScrollLeftLoop });
            }
        }

        Core.MenusAPI.OpenMenuForPlayer(p, menu);
    }
}

