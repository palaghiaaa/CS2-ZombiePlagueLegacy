using System.Drawing;
using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SteamAPI;
using static ZombiePlagueLegacyCS2.ZPLZombieClassCFG;

namespace ZombiePlagueLegacyCS2;

public class ZPLZombieClassMenu
{
    private readonly ILogger<ZPLZombieClassMenu> _logger;
    private readonly ISwiftlyCore _core;
    private readonly ZPLMenuHelper _menuhelper;
    private readonly IOptionsMonitor<ZPLZombieClassCFG> _zombieClassCFG;
    private readonly PlayerZombieState _zombieState;
    private readonly ZPLHelpers _helpers;
    private readonly ZombiePlagueLegacyAPI _api;

    public ZPLZombieClassMenu(ISwiftlyCore core, ILogger<ZPLZombieClassMenu> logger,
        ZPLMenuHelper menuHelper, IOptionsMonitor<ZPLZombieClassCFG> zombieClassCFG,
        PlayerZombieState zombieState, ZPLHelpers helpers, ZombiePlagueLegacyAPI api)
    {
        _core = core;
        _logger = logger;
        _menuhelper = menuHelper;
        _zombieClassCFG = zombieClassCFG;
        _zombieState = zombieState;
        _helpers = helpers;
        _api = api;
    }

    public IMenuAPI OpenZombieClassMenu(IPlayer player)
    {
        var main = _core.MenusAPI.CreateBuilder();
        IMenuAPI menu = _menuhelper.CreateMenu(_helpers.T(player, "ZClassMenu"));

        var Id = player.PlayerID;
        var steamId = player.SteamID;
        var currentPreference = _zombieState.GetPlayerPreference(Id, steamId);

        menu.AddOption(new TextMenuOption(HtmlGradient.GenerateGradientText(
            _helpers.T(player, "ZClassMenuSelect"),
            Color.Red, Color.LightBlue, Color.Red),
            updateIntervalMs: 500, pauseIntervalMs: 100)
        {
            TextStyle = MenuOptionTextStyle.ScrollLeftLoop
        });

        string randomButtonText = $"{_helpers.T(player, "ZClassMenuRandomSelect")} {(currentPreference?.Preference == ZombiePreference.Random ? "✓" : "")}";
        var RandomButton = new ButtonMenuOption(randomButtonText)
        {
            TextStyle = MenuOptionTextStyle.ScrollLeftLoop,
            CloseAfterClick = true
        };
        RandomButton.Tag = "extend";

        RandomButton.Click += async (_, args) =>
        {
            var clicker = args.Player;
            var clickerPlayerId = clicker.PlayerID;
            var clickerSteamId = clicker.SteamID;

            _core.Scheduler.NextTick(() =>
            {
                if (!clicker.IsValid)
                    return;

                if (_api != null)
                    _api.NotifyUpdatePreferenceFromMenu(clickerPlayerId, clickerSteamId, null);

                _helpers.SendChatT(clicker, "ZClassMenuRandomSelectInfo");
            });
        };

        menu.AddOption(RandomButton);

        var zombieConfig = _zombieClassCFG.CurrentValue;
        var zombieClasses = zombieConfig.ZombieClassList;

        if (zombieClasses != null && zombieClasses.Count > 0)
        {
            foreach (var Cfg in zombieClasses)
            {
                string buttonText = $"{Cfg.Name} {(currentPreference?.Preference == ZombiePreference.Fixed && currentPreference.FixedZombieName == Cfg.Name ? "✓" : "")}";

                var Button = new ButtonMenuOption(buttonText)
                {
                    TextStyle = MenuOptionTextStyle.ScrollLeftLoop,
                    CloseAfterClick = true
                };
                Button.Tag = "extend";

                Button.Click += async (_, args) =>
                {
                    var clicker = args.Player;
                    var clickerPlayerId = clicker.PlayerID;
                    var clickerSteamId = clicker.SteamID;

                    _core.Scheduler.NextTick(() =>
                    {
                        if (!clicker.IsValid)
                            return;

                        if (_api != null)
                            _api.NotifyUpdatePreferenceFromMenu(clickerPlayerId, clickerSteamId, Cfg.Name);

                        clicker.SendMessage(MessageType.Chat, _helpers.ChatMsg($"{_helpers.T(clicker, "ZClassMenuSelectInfo")} {Cfg.Name}"));

                        
                    });
                };

                menu.AddOption(Button);
            }
        }

        _core.MenusAPI.OpenMenuForPlayer(player, menu);
        return menu;
    }

    

}
