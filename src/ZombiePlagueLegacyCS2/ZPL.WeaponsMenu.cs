using System.Drawing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace ZombiePlagueLegacyCS2;

public class ZPLWeaponsMenu
{
    private readonly ILogger<ZPLWeaponsMenu> _logger;
    private readonly ISwiftlyCore _core;
    private readonly ZPLGlobals _globals;
    private readonly ZPLHelpers _helpers;
    private readonly ZPLMenuHelper _menuHelper;
    private readonly IOptionsMonitor<ZPLWeaponsCFG> _weaponsCFG;

    public ZPLWeaponsMenu(
        ISwiftlyCore core,
        ILogger<ZPLWeaponsMenu> logger,
        ZPLGlobals globals,
        ZPLHelpers helpers,
        ZPLMenuHelper menuHelper,
        IOptionsMonitor<ZPLWeaponsCFG> weaponsCFG)
    {
        _core = core;
        _logger = logger;
        _globals = globals;
        _helpers = helpers;
        _menuHelper = menuHelper;
        _weaponsCFG = weaponsCFG;
    }

    private bool IsWeaponSelectionWindowOpen(IPlayer player, bool sendReason = false)
    {

        // Once infection/custom start begins, late selections are no longer allowed.
        if (_globals.InfectionStartedThisRound || _globals.AdminForcedModeThisRound)
        {
            if (sendReason)
                _helpers.SendChatT(player, "WeaponsMenuInfectionStarted");
            return false;
        }

        return true;
    }

    public bool IsEligibleHuman(IPlayer player)
    {
        if (!player.IsValid)
            return false;

        var controller = player.Controller;
        if (controller == null || !controller.IsValid || !controller.PawnIsAlive)
            return false;

        // Only CT team players are eligible
        if (controller.Team != Team.CT)
            return false;

        return true;
    }

    public void OpenWeaponsMenuIfAllowed(IPlayer player)
    {
        var CFG = _weaponsCFG.CurrentValue;
        if (!CFG.EnableWeaponsMenu || !CFG.AllowOpenFromGameMenu)
            return;

        if (!IsWeaponSelectionWindowOpen(player, sendReason: true))
            return;

        if (!IsEligibleHuman(player))
        {
            _helpers.SendChatT(player, "WeaponsMenuNotEligible");
            return;
        }

        var id = player.PlayerID;
        if (!_globals.CanBuyWeaponsThisRound.TryGetValue(id, out bool canBuy) || !canBuy)
        {
            _helpers.SendChatT(player, "WeaponsMenuAlreadyUsed");
            return;
        }

        ShowPrimaryMenu(player);
    }

    public void ShowPrimaryMenuToAllEligible()
    {
        var CFG = _weaponsCFG.CurrentValue;
        if (!CFG.EnableWeaponsMenu || !CFG.GiveMenuOnRoundStart)
            return;


        var allPlayers = _core.PlayerManager.GetAllPlayers();
        foreach (var player in allPlayers)
        {
            if (!IsEligibleHuman(player))
                continue;

            var id = player.PlayerID;

            // Avoid duplicate auto-open within the same round (e.g. reliability pass).
            // Presence in this dictionary means this player has already been prompted.
            if (_globals.CanBuyWeaponsThisRound.ContainsKey(id))
                continue;

            _globals.CanBuyWeaponsThisRound[id] = true;
            ShowPrimaryMenu(player);
        }
    }

    public void ShowPrimaryMenu(IPlayer player)
    {
        var CFG = _weaponsCFG.CurrentValue;
        if (!CFG.EnableWeaponsMenu)
            return;

        if (!IsEligibleHuman(player))
            return;

        if (!IsWeaponSelectionWindowOpen(player, sendReason: false))
            return;

        IMenuAPI menu = _menuHelper.CreateMenu(_helpers.T(player, "WeaponMenuPrimaryTitle"));

        menu.AddOption(new TextMenuOption(HtmlGradient.GenerateGradientText(
            _helpers.T(player, "WeaponMenuPrimarySelect"),
            Color.Red, Color.LightBlue, Color.Red),
            updateIntervalMs: 500, pauseIntervalMs: 100)
        {
            TextStyle = MenuOptionTextStyle.ScrollLeftLoop
        });

        foreach (var weapon in CFG.PrimaryWeapons)
        {
            string weaponName = weapon.Name;
            string classname = weapon.Classname;

            var btn = new ButtonMenuOption(weaponName)
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

                    if (!IsWeaponSelectionWindowOpen(clicker, sendReason: true))
                        return;

                    var id = clicker.PlayerID;
                    _globals.CanBuyWeaponsThisRound[id] = false;
                    GiveWeaponBySlot(clicker, classname, gear_slot_t.GEAR_SLOT_RIFLE);
                    ShowSecondaryMenu(clicker);
                });
            };

            menu.AddOption(btn);
        }

        _core.MenusAPI.OpenMenuForPlayer(player, menu);
    }

    public void ShowSecondaryMenu(IPlayer player)
    {
        var CFG = _weaponsCFG.CurrentValue;
        if (!CFG.EnableWeaponsMenu)
            return;

        if (!player.IsValid)
            return;

        if (!IsWeaponSelectionWindowOpen(player, sendReason: false))
            return;

        IMenuAPI menu = _menuHelper.CreateMenu(_helpers.T(player, "WeaponMenuSecondaryTitle"));

        menu.AddOption(new TextMenuOption(HtmlGradient.GenerateGradientText(
            _helpers.T(player, "WeaponMenuSecondarySelect"),
            Color.Red, Color.LightBlue, Color.Red),
            updateIntervalMs: 500, pauseIntervalMs: 100)
        {
            TextStyle = MenuOptionTextStyle.ScrollLeftLoop
        });

        foreach (var weapon in CFG.SecondaryWeapons)
        {
            string weaponName = weapon.Name;
            string classname = weapon.Classname;

            var btn = new ButtonMenuOption(weaponName)
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

                    if (!IsWeaponSelectionWindowOpen(clicker, sendReason: true))
                        return;

                    GiveWeaponBySlot(clicker, classname, gear_slot_t.GEAR_SLOT_PISTOL);
                    GiveDefaultGrenades(clicker);
                });
            };

            menu.AddOption(btn);
        }

        _core.MenusAPI.OpenMenuForPlayer(player, menu);
    }

    private void GiveWeaponBySlot(IPlayer player, string classname, gear_slot_t slot)
    {
        if (!player.IsValid)
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        var ws = pawn.WeaponServices;
        if (ws == null || !ws.IsValid)
            return;

        ws.DropWeaponBySlot(slot);

        var Is = pawn.ItemServices;
        if (Is == null || !Is.IsValid)
            return;

        var weapon = Is.GiveItem<CCSWeaponBase>(classname);
        if (weapon == null || !weapon.IsValid)
            return;

        int reserveAmmo = _weaponsCFG.CurrentValue.ReserveAmmoAmount;
        weapon.ReserveAmmo[0] = reserveAmmo;
    }

    private void GiveDefaultGrenades(IPlayer player)
    {
        if (!player.IsValid)
            return;

        var CFG = _weaponsCFG.CurrentValue;
        foreach (var classname in CFG.DefaultGrenades)
        {
            switch (classname.ToLowerInvariant())
            {
                case "weapon_hegrenade":
                    _helpers.GiveFireGrenade(player);
                    break;
                case "weapon_flashbang":
                    _helpers.GiveLightGrenade(player);
                    break;
                case "weapon_smokegrenade":
                    _helpers.GiveFreezeGrenade(player);
                    break;
                case "weapon_incgrenade":
                    _helpers.GiveIncGrenade(player);
                    break;
                case "weapon_decoy":
                    _helpers.GiveTeleprotGrenade(player);
                    break;
            }
        }
    }
}
