using System.Drawing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;

namespace ZombiePlagueLegacyCS2;

public class ZPLAdminItemMenu
{
    private readonly ILogger<ZPLAdminItemMenu> _logger;
    private readonly ISwiftlyCore _core;
    private readonly ZPLMenuHelper _menuhelper;
    private readonly IOptionsMonitor<ZPLMainCFG> _mainCFG;
    private readonly ZPLHelpers _helpers;
    private readonly ZPLServices _services;
    private readonly ZPLGlobals _globals;
    private readonly ZPLGameMode _gameMode;

    public ZPLAdminItemMenu(ISwiftlyCore core, ILogger<ZPLAdminItemMenu> logger,
        ZPLMenuHelper menuHelper, IOptionsMonitor<ZPLMainCFG> mainCFG,
        ZPLHelpers helpers, ZPLServices service, ZPLGlobals globals,
        ZPLGameMode gameMode)
    {
        _core = core;
        _logger = logger;
        _menuhelper = menuHelper;
        _mainCFG = mainCFG;
        _helpers = helpers;
        _services = service;
        _globals = globals;
        _gameMode = gameMode;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Main admin menu  (7 fixed items)
    // ─────────────────────────────────────────────────────────────────────────

    public IMenuAPI OpenAdminItemMenu(IPlayer admin)
    {
        IMenuAPI menu = _menuhelper.CreateMenu(_helpers.T(admin, "AdminItemMenu"));

        menu.AddOption(new TextMenuOption(HtmlGradient.GenerateGradientText(
            _helpers.T(admin, "AdminMenuSelect"),
            Color.Red, Color.LightBlue, Color.Red),
            updateIntervalMs: 500, pauseIntervalMs: 100)
        {
            TextStyle = MenuOptionTextStyle.ScrollLeftLoop
        });

        // 1 – Make Zombie / Human (toggle)
        AddMenuButton(menu, _helpers.T(admin, "AdminMenuMakeZombieHuman"), clicker =>
            OpenPlayerListMenu(clicker, "AdminMenuMakeZombieHuman",
                p =>
                {
                    var ctrl = p.Controller;
                    return ctrl != null && ctrl.IsValid && ctrl.PawnIsAlive;
                },
                target =>
                {
                    _globals.IsZombie.TryGetValue(target.PlayerID, out bool isZ);
                    if (isZ)
                    {
                        _services.SetPlayerHuman(target);
                        _helpers.SendChatToAllT("AdminMenuToggledHumanToAll", target.Name);
                    }
                    else
                    {
                        _services.SetPlayerZombie(target);
                        _helpers.SendChatToAllT("AdminMenuToggledZombieToAll", target.Name);
                    }
                },
                showStateTag: true));

        // 2 – Make Nemesis
        AddMenuButton(menu, _helpers.T(admin, "AdminMenuMakeNemesis"), clicker =>
            OpenPlayerListMenu(clicker, "AdminMenuMakeNemesis",
                p =>
                {
                    var ctrl = p.Controller;
                    return ctrl != null && ctrl.IsValid && ctrl.PawnIsAlive;
                },
                target =>
                {
                    _services.SetupNemesis(target);
                    _helpers.SendChatToAllT("AdminMenuMadeNemesisToAll", target.Name);
                }));

        // 3 – Make Assassin
        AddMenuButton(menu, _helpers.T(admin, "AdminMenuMakeAssassin"), clicker =>
            OpenPlayerListMenu(clicker, "AdminMenuMakeAssassin",
                p =>
                {
                    var ctrl = p.Controller;
                    return ctrl != null && ctrl.IsValid && ctrl.PawnIsAlive;
                },
                target =>
                {
                    _services.SetupAssassin(target);
                    _helpers.SendChatToAllT("AdminMenuMadeAssassinToAll", target.Name);
                }));

        // 4 – Make Survivor
        AddMenuButton(menu, _helpers.T(admin, "AdminMenuMakeSurvivor"), clicker =>
            OpenPlayerListMenu(clicker, "AdminMenuMakeSurvivor",
                p =>
                {
                    var ctrl = p.Controller;
                    return ctrl != null && ctrl.IsValid && ctrl.PawnIsAlive;
                },
                target =>
                {
                    _services.SetupSurvivor(target);
                    _helpers.SendChatToAllT("AdminMenuMadeSurvivorToAll", target.Name);
                }));

        // 5 – Make Sniper
        AddMenuButton(menu, _helpers.T(admin, "AdminMenuMakeSniper"), clicker =>
            OpenPlayerListMenu(clicker, "AdminMenuMakeSniper",
                p =>
                {
                    var ctrl = p.Controller;
                    return ctrl != null && ctrl.IsValid && ctrl.PawnIsAlive;
                },
                target =>
                {
                    _services.SetupSniper(target);
                    _helpers.SendChatToAllT("AdminMenuMadeSniperToAll", target.Name);
                }));

        // 6 – Respawn Someone
        AddMenuButton(menu, _helpers.T(admin, "AdminMenuRespawnSomeone"), clicker =>
            OpenPlayerListMenu(clicker, "AdminMenuRespawnSomeone",
                p =>
                {
                    var ctrl = p.Controller;
                    return ctrl != null && ctrl.IsValid && !ctrl.PawnIsAlive;
                },
                target =>
                {
                    _globals.IsZombie[target.PlayerID] = false;
                    target.Respawn();
                    _helpers.SendChatToAllT("AdminMenuRespawnedToAll", target.Name);
                }));

        // 7 – Start Game Mode
        AddMenuButton(menu, _helpers.T(admin, "AdminMenuStartGameMode"), clicker =>
            OpenModeSelectionMenu(clicker));

        _core.MenusAPI.OpenMenuForPlayer(admin, menu);
        return menu;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helper: add a button that opens a submenu on the next tick
    // ─────────────────────────────────────────────────────────────────────────

    private void AddMenuButton(IMenuAPI menu, string label, Action<IPlayer> onClick)
    {
        var btn = new ButtonMenuOption(label)
        {
            TextStyle = MenuOptionTextStyle.ScrollLeftLoop,
            CloseAfterClick = true
        };
        btn.Click += async (_, args) =>
        {
            var clicker = args.Player;
            _core.Scheduler.NextTick(() => { if (clicker.IsValid) onClick(clicker); });
        };
        menu.AddOption(btn);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Mode selection submenu  (item 7)
    // ─────────────────────────────────────────────────────────────────────────

    private void OpenModeSelectionMenu(IPlayer admin)
    {
        if (admin == null || !admin.IsValid) return;

        // Block mode selection after infection has already started this round.
        if (_globals.InfectionStartedThisRound)
        {
            _helpers.SendChatT(admin, "AdminMenuModeInfectionStarted");
            return;
        }

        IMenuAPI menu = _menuhelper.CreateMenu(_helpers.T(admin, "AdminMenuStartGameMode"));

        var modes = new (GameModeType type, string labelKey)[]
        {
            (GameModeType.NormalInfection, "NormalInfection"),
            (GameModeType.MultiInfection,  "MultiInfection"),
            (GameModeType.Nemesis,          "NemesisMode"),
            (GameModeType.Survivor,         "SurvivorMode"),
            (GameModeType.Swarm,            "SwarmMode"),
            (GameModeType.Plague,           "PlagueMode"),
            (GameModeType.Assassin,         "AssassinMode"),
            (GameModeType.Sniper,           "SniperMode"),
            (GameModeType.AVS,              "AssassinVSSniper"),
            (GameModeType.Hero,             "HeroMode"),
        };

        foreach (var (modeType, labelKey) in modes)
        {
            var capturedType = modeType;
            var capturedKey = labelKey;

            var btn = new ButtonMenuOption(_helpers.T(admin, labelKey))
            {
                TextStyle = MenuOptionTextStyle.ScrollLeftLoop,
                CloseAfterClick = true
            };
            btn.Click += async (_, args) =>
            {
                var clicker = args.Player;
                _core.Scheduler.NextTick(() =>
                {
                    if (!clicker.IsValid) return;
                    _globals.AdminForcedModeThisRound = true;
                    _gameMode.SetMode(capturedType);
                    _globals.GameStart = true;
                    _services.SwitchMode();
                    _helpers.SendChatToAllT("AdminMenuModeActivated",
                        _helpers.T(clicker, capturedKey));
                });
            };
            menu.AddOption(btn);
        }

        _core.MenusAPI.OpenMenuForPlayer(admin, menu);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Generic player-picker submenu
    // ─────────────────────────────────────────────────────────────────────────

    private void OpenPlayerListMenu(IPlayer admin, string titleKey,
        Func<IPlayer, bool> filter, Action<IPlayer> action,
        bool showStateTag = false)
    {
        if (admin == null || !admin.IsValid) return;

        IMenuAPI menu = _menuhelper.CreateMenu(_helpers.T(admin, titleKey));

        var allPlayers = _core.PlayerManager.GetAllPlayers();
        bool anyTarget = false;

        foreach (var target in allPlayers)
        {
            if (target == null || !target.IsValid || target.IsFakeClient)
                continue;

            if (!filter(target))
                continue;

            anyTarget = true;
            var capturedTarget = target;

            string label = target.Name;
            if (showStateTag)
            {
                _globals.IsZombie.TryGetValue(target.PlayerID, out bool isZ);
                label = isZ
                    ? $"[Z] {target.Name}"
                    : $"[H] {target.Name}";
            }

            var btn = new ButtonMenuOption(label)
            {
                TextStyle = MenuOptionTextStyle.ScrollLeftLoop,
                CloseAfterClick = true
            };
            btn.Click += async (_, args) =>
            {
                _core.Scheduler.NextTick(() =>
                {
                    if (!capturedTarget.IsValid) return;
                    action(capturedTarget);
                });
            };
            menu.AddOption(btn);
        }

        if (!anyTarget)
        {
            menu.AddOption(new TextMenuOption(_helpers.T(admin, "AdminMenuNoTargets")));
        }

        _core.MenusAPI.OpenMenuForPlayer(admin, menu);
    }
}
