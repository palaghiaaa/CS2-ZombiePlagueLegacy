using System.Drawing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;

namespace ZombieOutstandingCS2;

/// <summary>
/// Provides the mine-selection menu that players use to choose and place laser
/// trip-mines.  Mirrors the HanLaserTripmineS2 (HLTMenu) menu logic, integrated
/// into ZombieOutstandingCS2.
/// </summary>
public class ZOMineMenu
{
    private readonly ILogger<ZOMineMenu> _logger;
    private readonly ISwiftlyCore _core;
    private readonly IOptionsMonitor<ZOMineCFG> _mineCFG;
    private readonly ZOMenuHelper _menuHelper;
    private readonly ZOMineService _mineService;
    private readonly ZOGlobals _globals;

    public ZOMineMenu(
        ISwiftlyCore core,
        ILogger<ZOMineMenu> logger,
        IOptionsMonitor<ZOMineCFG> mineCFG,
        ZOMenuHelper menuHelper,
        ZOMineService mineService,
        ZOGlobals globals)
    {
        _core        = core;
        _logger      = logger;
        _mineCFG     = mineCFG;
        _menuHelper  = menuHelper;
        _mineService = mineService;
        _globals     = globals;
    }

    public void OpenMineMenu(IPlayer player)
    {
        if (player == null || !player.IsValid) return;

        var controller = player.Controller;
        if (controller == null || !controller.IsValid) return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid) return;

        IMenuAPI menu = _menuHelper.CreateMenu(
            _core.Translation.GetPlayerLocalizer(player)["MineMenuTitle"] ?? "Laser Tripmine");

        menu.AddOption(new TextMenuOption(HtmlGradient.GenerateGradientText(
            _core.Translation.GetPlayerLocalizer(player)["MineMenuSelectPrompt"] ?? "Select a mine type",
            Color.Red, Color.LightBlue, Color.Red),
            updateIntervalMs: 500, pauseIntervalMs: 100)
        {
            TextStyle = MenuOptionTextStyle.ScrollLeftLoop
        });

        var mineList = _mineCFG.CurrentValue.MineList;
        if (mineList == null || mineList.Count == 0)
        {
            menu.AddOption(new TextMenuOption(
                _core.Translation.GetPlayerLocalizer(player)["MineMenuEmpty"] ?? "No mines configured."));
            _core.MenusAPI.OpenMenuForPlayer(player, menu);
            return;
        }

        int playerTeam = pawn.TeamNum;
        var steamId    = player.SteamID;

        foreach (var mineCfg in mineList)
        {
            if (!mineCfg.Enable) continue;

            // Team filter
            string teamStr = string.IsNullOrEmpty(mineCfg.Team) ? "all" : mineCfg.Team.ToLower();
            if (teamStr != "all")
            {
                if (teamStr == "t"  && playerTeam != 2) continue;
                if (teamStr == "ct" && playerTeam != 3) continue;
            }

            // Permission check
            if (!string.IsNullOrEmpty(mineCfg.Permissions) &&
                (steamId == 0 || !_core.Permission.PlayerHasPermission(steamId, mineCfg.Permissions)))
                continue;

            string priceText = mineCfg.Price > 0
                ? $"${mineCfg.Price}"
                : (_core.Translation.GetPlayerLocalizer(player)["FreeText"] ?? "Free");

            string limitText = mineCfg.Limit > 0
                ? mineCfg.Limit.ToString()
                : (_core.Translation.GetPlayerLocalizer(player)["LimitText"] ?? "∞");

            string buttonText = $"{mineCfg.Name} [{priceText} | Limit: {limitText}]";

            var btn = new ButtonMenuOption(buttonText)
            {
                TextStyle       = MenuOptionTextStyle.ScrollLeftLoop,
                CloseAfterClick = false
            };
            btn.Tag = "extend";

            var capturedName = mineCfg.Name;
            btn.Click += async (_, args) =>
            {
                var clicker = args.Player;
                _core.Scheduler.NextTick(() =>
                {
                    if (!clicker.IsValid) return;
                    _globals.IsZombie.TryGetValue(clicker.PlayerID, out bool isZombie);
                    if (isZombie)
                    {
                        _core.MenusAPI.CloseActiveMenu(clicker);
                        return;
                    }
                    _mineService.CreateMineEnt(clicker, capturedName);
                });
            };

            menu.AddOption(btn);
        }

        _core.MenusAPI.OpenMenuForPlayer(player, menu);
    }
}
