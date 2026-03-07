using System.Drawing;
using Microsoft.Extensions.Logging;
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
        Core.GameEvent.HookPre<EventPlayerDeath>(OnPlayerDeath);

        // !rank  → chat rank message
        Core.Command.RegisterCommand("rank", OnRankCommand, true);

        // !top / !top15 → top-15 menu
        Core.Command.RegisterCommand("top", OnTopCommand, true);
        Core.Command.RegisterCommand("top15", OnTopCommand, true);

        // !top10 → top-10 menu
        Core.Command.RegisterCommand("top10", OnTop10Command, true);

        Core.Logger.LogInformation("[ZORank] Loaded. Commands: !rank, !top, !top15, !top10");
    }

    public override void Unload()
    {
        _stats.Clear();
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

        // Build ranking: sort by kills desc, then deaths asc.
        var sorted = _stats
            .OrderByDescending(kvp => kvp.Value.Kills)
            .ThenBy(kvp => kvp.Value.Deaths)
            .ToList();

        int rank = sorted.FindIndex(kvp => kvp.Key == steamId) + 1;
        int total = sorted.Count;

        player.SendMessage(MessageType.Chat,
            $" \x04[ZO Rank]\x01 Player\x03 {myStat.Name}\x01's rank is" +
            $"\x03 {rank}/{total}\x01 with\x03 {myStat.Kills}\x01 Kills and" +
            $"\x03 {myStat.Deaths}\x01 Deaths");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  !top / !top15 / !top10 commands
    // ─────────────────────────────────────────────────────────────────────────

    private void OnTopCommand(ICommandContext context) => OpenTopMenu(context.Sender, 15);
    private void OnTop10Command(ICommandContext context) => OpenTopMenu(context.Sender, 10);

    private void OpenTopMenu(IPlayer? player, int maxEntries)
    {
        if (player == null || !player.IsValid || player.IsFakeClient)
            return;

        var sorted = _stats
            .OrderByDescending(kvp => kvp.Value.Kills)
            .ThenBy(kvp => kvp.Value.Deaths)
            .Take(maxEntries)
            .ToList();

        var menuConfig = new MenuConfiguration
        {
            Title = HtmlGradient.GenerateGradientText($"TOP {maxEntries}", Color.Gold),
            FreezePlayer = false,
            MaxVisibleItems = 5,
            PlaySound = true,
            AutoIncreaseVisibleItems = false,
            HideFooter = false
        };

        IMenuAPI menu = Core.MenusAPI.CreateMenu(menuConfig, default);

        if (sorted.Count == 0)
        {
            menu.AddOption(new TextMenuOption("No stats available yet."));
        }
        else
        {
            for (int i = 0; i < sorted.Count; i++)
            {
                var stat = sorted[i].Value;
                string label = $"#{i + 1}  {stat.Name}  [{stat.Kills} Kills / {stat.Deaths} Deaths]";
                menu.AddOption(new TextMenuOption(label));
            }
        }

        Core.MenusAPI.OpenMenuForPlayer(player, menu);
    }
}
