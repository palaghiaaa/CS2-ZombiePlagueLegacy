using System.Numerics;
using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SteamAPI;

namespace ZombieOutstandingCS2;

public class ZOCommands
{
    private readonly ILogger<ZOCommands> _logger;
    private readonly ISwiftlyCore _core;
    private readonly ZOServices _services;
    private readonly IOptionsMonitor<ZOMainCFG> _mainCFG;
    private readonly ZOGlobals _globals;
    private readonly ZOZombieClassMenu _hZPZombieClassMenu;
    private readonly ZOAdminItemMenu _hZPAdminItemMenu;
    private readonly ZOHelpers _helpers;
    private readonly ZOWeaponsMenu _weaponsMenu;
    private readonly ZOGameMenu _gameMenu;
    private readonly ZOExtraItemsMenu _extraItemsMenu;
    private readonly ZOMineMenu _mineMenu;

    public ZOCommands(ISwiftlyCore core, ILogger<ZOCommands> logger,
        ZOServices services, IOptionsMonitor<ZOMainCFG> mainCFG,
        ZOGlobals globals, ZOAdminItemMenu hZPAdminItemMenu,
        ZOZombieClassMenu hZPZombieClassMenu, ZOHelpers helpers,
        ZOWeaponsMenu weaponsMenu, ZOGameMenu gameMenu,
        ZOExtraItemsMenu extraItemsMenu,
        ZOMineMenu mineMenu)
    {
        _core = core;
        _logger = logger;
        _services = services;
        _mainCFG = mainCFG;
        _globals = globals;
        _hZPAdminItemMenu = hZPAdminItemMenu;
        _hZPZombieClassMenu = hZPZombieClassMenu;
        _helpers = helpers;
        _weaponsMenu = weaponsMenu;
        _gameMenu = gameMenu;
        _extraItemsMenu = extraItemsMenu;
        _mineMenu = mineMenu;
    }

    public void MenuCommands()
    {
        var CFG = _mainCFG.CurrentValue;
        _core.Command.RegisterCommand(CFG.ZombieClassCommand, SelectZombieClass, true);
        _logger.LogInformation("[ZO] Registered zombie class command: {Cmd}", CFG.ZombieClassCommand);

        _core.Command.RegisterCommand(CFG.AdminMenuItemCommand, UseItemMenu, true);
        _logger.LogInformation("[ZO] Registered admin menu command: {Cmd}", CFG.AdminMenuItemCommand);

        _core.Command.RegisterCommand(CFG.BuyWeaponsCommand, BuyWeapons, true);
        _logger.LogInformation("[ZO] Registered buy weapons command: {Cmd}", CFG.BuyWeaponsCommand);

        _core.Command.RegisterCommand(CFG.MainMenuCommand, OpenGameMenu, true);
        _logger.LogInformation("[ZO] Registered main menu command: {Cmd}", CFG.MainMenuCommand);

        _core.Command.RegisterCommand(CFG.ExtraItemsCommand, OpenExtraItemsMenu, true);
        _logger.LogInformation("[ZO] Registered extra items command: {Cmd}", CFG.ExtraItemsCommand);

        _core.Command.RegisterCommand(CFG.MineMenuCommand, OpenMineMenu, true);
        _logger.LogInformation("[ZO] Registered mine menu command: {Cmd}", CFG.MineMenuCommand);
    }
    public void SelectZombieClass(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null || !player.IsValid)
            return;

        _hZPZombieClassMenu.OpenZombieClassMenu(player);

    }

    public void UseItemMenu(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null || !player.IsValid)
            return;


        if (!HasAdminMenuPermission(player))
        {
            _helpers.SendChatT(player, "NoPermission");
            return;
        }
            

        _hZPAdminItemMenu.OpenAdminItemMenu(player);
    }

    public void BuyWeapons(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null || !player.IsValid)
            return;

        _weaponsMenu.OpenWeaponsMenuIfAllowed(player);
    }

    public void OpenGameMenu(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null || !player.IsValid) return;
        _gameMenu.OpenGameMenu(player);
    }

    public void OpenExtraItemsMenu(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null || !player.IsValid) return;
        _extraItemsMenu.OpenExtraItemsMenu(player);
    }

    public void OpenMineMenu(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null || !player.IsValid) return;
        _mineMenu.OpenMineMenu(player);
    }

    private bool HasAdminMenuPermission(IPlayer player)
    {
        if (!player.IsValid)
            return false;

        ulong steamId = player.SteamID;
        if (steamId == 0)
            return false;

        var permString = _mainCFG.CurrentValue.AdminMenuPermission;

        if (string.IsNullOrWhiteSpace(permString))
            return true;

        foreach (var perm in permString.Split(','))
        {
            var p = perm.Trim();
            if (p.Length == 0)
                continue;

            if (_core.Permission.PlayerHasPermission(steamId, p))
                return true;
        }

        return false;
    }

    public void RoundCvar()
    {
        var CFG = _mainCFG.CurrentValue;
        _core.Engine.ExecuteCommand("mp_randomspawn 1");
        _core.Engine.ExecuteCommand($"mp_roundtime_hostage {CFG.RoundTime}");
        _core.Engine.ExecuteCommand($"mp_roundtime_defuse {CFG.RoundTime}");
        _core.Engine.ExecuteCommand($"mp_roundtime {CFG.RoundTime}");
        _core.Engine.ExecuteCommand("mp_give_player_c4 0");

    }

    public void ServerCvar()
    {
        
        _core.Engine.ExecuteCommand("mp_randomspawn 1");
        _core.Engine.ExecuteCommand("mp_roundtime_hostage 3");
        _core.Engine.ExecuteCommand("mp_roundtime_defuse 3");
        _core.Engine.ExecuteCommand("mp_roundtime 3");
        _core.Engine.ExecuteCommand("bot_quota_mode fill");
        _core.Engine.ExecuteCommand("bot_quota 20");
        _core.Engine.ExecuteCommand("mp_ignore_round_win_conditions 1");
        _core.Engine.ExecuteCommand("bot_join_after_player 1");
        _core.Engine.ExecuteCommand("bot_chatter off");
        _core.Engine.ExecuteCommand("mp_autokick 0");
        _core.Engine.ExecuteCommand("mp_round_restart_delay 0");
        _core.Engine.ExecuteCommand("mp_autoteambalance 0");
        _core.Engine.ExecuteCommand("mp_startmoney 16000");
    }
    public void Command()
    {
        _core.Command.RegisterCommand("jointeam", RegisterJoin, true);
        _core.Command.HookClientCommand(OnJoinTeam);

    }

    public void RegisterJoin(ICommandContext context){
    }


    public HookResult OnJoinTeam(int playerId, string commandLine)
    {
        IPlayer? player = _core.PlayerManager.GetPlayer(playerId);
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        if (!player.IsFakeClient)
        {
            if (commandLine.StartsWith("jointeam 2"))
            {
                player.SwitchTeam(Team.CT);
                _core.Scheduler.DelayBySeconds(1.0f, () =>
                {
                    if (!player.IsValid) return;
                    _services.JoinTeamCheck(player);
                });
            }
            else if (commandLine.StartsWith("jointeam 3"))
            {
                player.SwitchTeam(Team.CT);
                _core.Scheduler.DelayBySeconds(1.0f, () =>
                {
                    if (!player.IsValid) return;
                    _services.JoinTeamCheck(player);
                });

            }
            else if (commandLine.StartsWith("jointeam 1"))
            {
                player.SwitchTeam(Team.CT);
                _core.Scheduler.DelayBySeconds(1.0f, () =>
                {
                    if (!player.IsValid) return;
                    _services.JoinTeamCheck(player);
                });
                return HookResult.Stop;
            }

        }
        return HookResult.Continue;
    }


}

