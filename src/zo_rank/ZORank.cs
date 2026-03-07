using System.Drawing;
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
    // Per-session stats keyed by SteamID.
    private readonly Dictionary<ulong, PlayerStat> _stats = new();

    private ServiceProvider? _sp;
    private IOptionsMonitor<ZORankCFG>? _cfg;

    // ZO API — used for ZO_IsZombie() and ZO_OnPlayerInfect event.
    private IZombieOutstandingAPI? _zoApi;

    private class PlayerStat
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

    public override void Load(bool hotReload)
    {
        // Bind config from configs/plugins/ZORank/ZORankCFG.jsonc
        Core.Configuration.InitializeJsonWithModel<ZORankCFG>("ZORankCFG.jsonc", "ZORankCFG");

        var collection = new ServiceCollection();
        collection.AddSwiftly(Core);
        collection.AddOptions<ZORankCFG>().BindConfiguration("ZORankCFG");
        _sp  = collection.BuildServiceProvider();
        _cfg = _sp.GetRequiredService<IOptionsMonitor<ZORankCFG>>();

        Core.GameEvent.HookPre<EventPlayerDeath>(OnPlayerDeath);
        Core.Event.OnEntityTakeDamage += OnEntityTakeDamage;

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

    /// <summary>
    /// Sorts the stat dictionary according to the configured SortMode.
    /// Tiebreaker chain: kills → infections → assists → damage → deaths asc.
    /// </summary>
    private List<KeyValuePair<ulong, PlayerStat>> BuildSortedList()
    {
        var mode = (_cfg!.CurrentValue.SortMode ?? "kills").ToLowerInvariant();
        IOrderedEnumerable<KeyValuePair<ulong, PlayerStat>> ordered = mode switch
        {
            "infections" => _stats.OrderByDescending(x => x.Value.Infections)
                                  .ThenByDescending(x => x.Value.Kills),
            "damage"     => _stats.OrderByDescending(x => x.Value.Damage)
                                  .ThenByDescending(x => x.Value.Kills),
            _            => _stats.OrderByDescending(x => x.Value.Kills)
                                  .ThenByDescending(x => x.Value.Infections),
        };
        return ordered
            .ThenByDescending(x => x.Value.Assists)
            .ThenByDescending(x => x.Value.Damage)
            .ThenBy(x => x.Value.Deaths)
            .ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Stat tracking
    // ─────────────────────────────────────────────────────────────────────────

    private HookResult OnPlayerDeath(EventPlayerDeath @event)
    {
        // ── Victim: record death ──────────────────────────────────────────────
        var victim = @event.UserIdPlayer;
        if (victim != null && victim.IsValid && !victim.IsFakeClient)
        {
            ulong vSteam = victim.SteamID;
            if (vSteam != 0)
                GetOrCreate(vSteam, victim.Name).Deaths++;
        }

        // ── Attacker: record kill (human kills only; infections via ZO event) ─
        var attacker = @event.AttackerPlayer;
        if (attacker != null && attacker.IsValid && !attacker.IsFakeClient)
        {
            ulong aSteam = attacker.SteamID;
            ulong vSteam = victim?.SteamID ?? 0;
            // Only count as a "kill" when a human kills a human (PvP)
            // or when ZO API is absent (fall back to counting every kill).
            if (aSteam != 0 && aSteam != vSteam)
            {
                bool attackerIsZombie = IsZombie(attacker.PlayerID);
                // Infections are handled by ZO_OnPlayerInfect; don't double-count.
                if (!attackerIsZombie)
                    GetOrCreate(aSteam, attacker.Name).Kills++;
            }
        }

        // ── Assister ─────────────────────────────────────────────────────────
        var assister = @event.AssisterPlayer;
        if (assister != null && assister.IsValid && !assister.IsFakeClient)
        {
            ulong sSteam = assister.SteamID;
            if (sSteam != 0)
                GetOrCreate(sSteam, assister.Name).Assists++;
        }

        return HookResult.Continue;
    }

    /// <summary>
    /// Fired by IZombieOutstandingAPI when a zombie player infects a human.
    /// Counts one infection for the attacker.
    /// </summary>
    private void OnZOPlayerInfect(IPlayer attacker, IPlayer victim, bool grenade, string zombieClass)
    {
        if (attacker == null || !attacker.IsValid || attacker.IsFakeClient) return;
        ulong aSteam = attacker.SteamID;
        if (aSteam == 0) return;
        GetOrCreate(aSteam, attacker.Name).Infections++;
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
        if (attackerPlayer == null || !attackerPlayer.IsValid || attackerPlayer.IsFakeClient) return;

        ulong aSteam = attackerPlayer.SteamID;
        if (aSteam == 0) return;

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
        if (player == null || !player.IsValid || player.IsFakeClient) return;

        ulong steamId = player.SteamID;
        if (steamId == 0) return;

        var myStat = GetOrCreate(steamId, player.Name);
        var sorted = BuildSortedList();
        int rank  = sorted.FindIndex(kvp => kvp.Key == steamId) + 1;
        int total = sorted.Count;

        var cfg  = _cfg!.CurrentValue;
        string body = T(player, "RankMessage",
            myStat.Name, rank, total,
            myStat.Kills, myStat.Deaths,
            myStat.Infections, myStat.Assists, myStat.Damage);
        player.SendMessage(MessageType.Chat, $" \x04{cfg.ChatTag}\x01 {body}");
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
        if (player == null || !player.IsValid || player.IsFakeClient) return;

        var cfg    = _cfg!.CurrentValue;
        var sorted = BuildSortedList().Take(limit).ToList();

        var menuConfig = new MenuConfiguration
        {
            Title           = HtmlGradient.GenerateGradientText(T(player, "TopMenuTitle", limit), Color.Gold),
            FreezePlayer    = false,
            MaxVisibleItems = cfg.TopMenuVisibleRows,
            PlaySound       = true,
            AutoIncreaseVisibleItems = false,
            HideFooter      = false
        };

        IMenuAPI menu = Core.MenusAPI.CreateMenu(menuConfig, default);

        if (sorted.Count == 0)
        {
            menu.AddOption(new TextMenuOption(T(player, "TopMenuNoStats")));
        }
        else
        {
            for (int i = 0; i < sorted.Count; i++)
            {
                var s     = sorted[i].Value;
                string lbl = T(player, "TopMenuEntry",
                    i + 1, s.Name,
                    s.Kills, s.Deaths,
                    s.Infections, s.Assists, s.Damage);
                menu.AddOption(new TextMenuOption(lbl));
            }
        }

        Core.MenusAPI.OpenMenuForPlayer(player, menu);
    }
}

