using System.Drawing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;

namespace ZORank;

[PluginMetadata(
    Id = "ZORank",
    Version = "1.0",
    Name = "ZO Rank & Top",
    Author = "ZombieOutstanding",
    Description = "Rank and Top stats plugin for ZombieOutstanding CS2.")]
public class ZORankPlugin(ISwiftlyCore core) : BasePlugin(core)
{
    // Per-session kill/death stats keyed by SteamID.
    private readonly Dictionary<ulong, PlayerStat> _stats = new();

    private ServiceProvider? _sp;
    private IOptionsMonitor<ZORankCFG>? _cfg;

    private class PlayerStat
    {
        public string Name { get; set; } = string.Empty;
        public int Kills { get; set; }
        public int Deaths { get; set; }
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
        _sp = collection.BuildServiceProvider();
        _cfg = _sp.GetRequiredService<IOptionsMonitor<ZORankCFG>>();

        Core.GameEvent.HookPre<EventPlayerDeath>(OnPlayerDeath);

        RegisterCommands(_cfg.CurrentValue);

        _cfg.OnChange(newCfg =>
        {
            Core.Logger.LogInformation("[ZORank] Configuration hot-reloaded.");
        });

        Core.Logger.LogInformation("[ZORank] Loaded. Commands: !{Rank}, !{Top}, !{Top15}, !{Top10}",
            _cfg.CurrentValue.RankCommand,
            _cfg.CurrentValue.TopCommand,
            _cfg.CurrentValue.Top15Command,
            _cfg.CurrentValue.Top10Command);
    }

    public override void Unload()
    {
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
            Core.Command.RegisterCommand(cfg.TopCommand, OnTopCommand, true);
            Core.Command.RegisterCommand(cfg.Top15Command, OnTop15Command, true);
            Core.Command.RegisterCommand(cfg.Top10Command, OnTop10Command, true);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Translation helper
    // ─────────────────────────────────────────────────────────────────────────

    private string T(IPlayer player, string key, params object[] args)
    {
        var localizer = Core.Translation.GetPlayerLocalizer(player);
        return args.Length == 0 ? localizer[key] : localizer[key, args];
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Stat tracking
    // ─────────────────────────────────────────────────────────────────────────

    private HookResult OnPlayerDeath(EventPlayerDeath @event)
    {
        var victim = @event.UserIdPlayer;
        if (victim == null || !victim.IsValid || victim.IsFakeClient)
            return HookResult.Continue;

        // Record death for the victim.
        ulong vSteamId = victim.SteamID;
        if (vSteamId != 0)
        {
            if (!_stats.TryGetValue(vSteamId, out var vStat))
                _stats[vSteamId] = vStat = new PlayerStat();
            vStat.Name = victim.Name;
            vStat.Deaths++;
        }

        // Record kill for the attacker (skip bots, suicides, and world kills).
        var attacker = @event.AttackerPlayer;
        if (attacker != null && attacker.IsValid && !attacker.IsFakeClient)
        {
            ulong aSteamId = attacker.SteamID;
            if (aSteamId != 0 && aSteamId != vSteamId)
            {
                if (!_stats.TryGetValue(aSteamId, out var aStat))
                    _stats[aSteamId] = aStat = new PlayerStat();
                aStat.Name = attacker.Name;
                aStat.Kills++;
            }
        }

        return HookResult.Continue;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  !rank command
    // ─────────────────────────────────────────────────────────────────────────

    private void OnRankCommand(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null || !player.IsValid || player.IsFakeClient)
            return;

        ulong steamId = player.SteamID;
        if (steamId == 0)
            return;

        // Ensure the requesting player has a stat entry (even if 0/0).
        if (!_stats.TryGetValue(steamId, out var myStat))
            _stats[steamId] = myStat = new PlayerStat { Name = player.Name };
        else
            myStat.Name = player.Name;

        // Sort by kills desc, then deaths asc.
        var sorted = _stats
            .OrderByDescending(kvp => kvp.Value.Kills)
            .ThenBy(kvp => kvp.Value.Deaths)
            .ToList();

        int rank = sorted.FindIndex(kvp => kvp.Key == steamId) + 1;
        int total = sorted.Count;

        var cfg = _cfg!.CurrentValue;
        string body = T(player, "RankMessage", myStat.Name, rank, total, myStat.Kills, myStat.Deaths);
        player.SendMessage(MessageType.Chat, $" \x04{cfg.ChatTag}\x01 {body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  !top / !top15 / !top10 commands
    // ─────────────────────────────────────────────────────────────────────────

    private void OnTopCommand(ICommandContext context)
        => OpenTopMenu(context.Sender, _cfg!.CurrentValue.TopListSize);

    private void OnTop15Command(ICommandContext context)
        => OpenTopMenu(context.Sender, 15);

    private void OnTop10Command(ICommandContext context)
        => OpenTopMenu(context.Sender, 10);

    private void OpenTopMenu(IPlayer? player, int limit)
    {
        if (player == null || !player.IsValid || player.IsFakeClient)
            return;

        var cfg = _cfg!.CurrentValue;

        var sorted = _stats
            .OrderByDescending(kvp => kvp.Value.Kills)
            .ThenBy(kvp => kvp.Value.Deaths)
            .Take(limit)
            .ToList();

        var menuConfig = new MenuConfiguration
        {
            Title = HtmlGradient.GenerateGradientText(T(player, "TopMenuTitle", limit), Color.Gold),
            FreezePlayer = false,
            MaxVisibleItems = cfg.TopMenuVisibleRows,
            PlaySound = true,
            AutoIncreaseVisibleItems = false,
            HideFooter = false
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
                var stat = sorted[i].Value;
                string label = T(player, "TopMenuEntry", i + 1, stat.Name, stat.Kills, stat.Deaths);
                menu.AddOption(new TextMenuOption(label));
            }
        }

        Core.MenusAPI.OpenMenuForPlayer(player, menu);
    }
}

