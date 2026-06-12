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
    private readonly ZPLPlayerPrefsService _prefsService;

    public ZPLWeaponsMenu(
        ISwiftlyCore core,
        ILogger<ZPLWeaponsMenu> logger,
        ZPLGlobals globals,
        ZPLHelpers helpers,
        ZPLMenuHelper menuHelper,
        IOptionsMonitor<ZPLWeaponsCFG> weaponsCFG,
        ZPLPlayerPrefsService prefsService)
    {
        _core = core;
        _logger = logger;
        _globals = globals;
        _helpers = helpers;
        _menuHelper = menuHelper;
        _weaponsCFG = weaponsCFG;
        _prefsService = prefsService;
    }

    private bool IsWeaponSelectionWindowOpen(IPlayer player, bool sendReason = false)
    {
        // Before infection starts — always open
        if (!_globals.InfectionStartedThisRound && !_globals.AdminForcedModeThisRound)
            return true;

        // After infection — allow if config permits mid-round access for live humans
        var CFG = _weaponsCFG.CurrentValue;
        if (CFG.AllowWeaponsMenuDuringRound && IsEligibleHuman(player))
            return true;

        if (sendReason)
            _helpers.SendChatT(player, "WeaponsMenuInfectionStarted");
        return false;
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

    private static ulong GetPreferenceKey(IPlayer player)
        => player.SteamID != 0 ? player.SteamID : (ulong)player.PlayerID;

    private WeaponLoadoutPreference GetLoadoutPreference(IPlayer player)
    {
        ulong key = GetPreferenceKey(player);
        if (!_globals.WeaponLoadoutPreferences.TryGetValue(key, out var preference))
        {
            preference = new WeaponLoadoutPreference();
            _globals.WeaponLoadoutPreferences[key] = preference;
        }

        return preference;
    }

    private static bool TryFindWeapon(IEnumerable<WeaponEntry> weapons, string classname, out WeaponEntry weapon)
    {
        weapon = weapons.FirstOrDefault(entry =>
            !string.IsNullOrWhiteSpace(entry.Classname) &&
            entry.Classname.Equals(classname, StringComparison.OrdinalIgnoreCase))!;

        return weapon != null;
    }

    private void SavePrimaryChoice(IPlayer player, string name, string classname)
    {
        var preference = GetLoadoutPreference(player);
        preference.PrimaryName = name;
        preference.PrimaryClassname = classname;
        PersistLoadoutPreference(player, preference);
    }

    private void SaveSecondaryChoice(IPlayer player, string name, string classname)
    {
        var preference = GetLoadoutPreference(player);
        preference.SecondaryName = name;
        preference.SecondaryClassname = classname;
        PersistLoadoutPreference(player, preference);
    }

    private void PersistLoadoutPreference(IPlayer player, WeaponLoadoutPreference preference)
    {
        if (player.SteamID == 0)
            return;

        _prefsService.SaveWeaponPreference(player.SteamID, preference);
    }

    private void AddRememberChoiceOption(IMenuAPI menu, IPlayer player, Action<IPlayer> reopenMenu)
    {
        var CFG = _weaponsCFG.CurrentValue;
        if (!CFG.EnableRememberChoice)
            return;

        var preference = GetLoadoutPreference(player);
        string state = preference.RememberChoice
            ? _helpers.T(player, "CommonOn")
            : _helpers.T(player, "CommonOff");

        string color = preference.RememberChoice ? ZPLMenuHelper.ColSelected : ZPLMenuHelper.ColHint;
        var rememberBtn = ZPLMenuHelper.LargeButton(_helpers.T(player, "WeaponMenuRememberChoice", state), color);

        rememberBtn.Click += async (_, args) =>
        {
            var clicker = args.Player;
            _core.Scheduler.NextTick(() =>
            {
                if (!clicker.IsValid)
                    return;

                var clickerPreference = GetLoadoutPreference(clicker);
                clickerPreference.RememberChoice = !clickerPreference.RememberChoice;
                PersistLoadoutPreference(clicker, clickerPreference);
                _helpers.SendChatT(clicker, clickerPreference.RememberChoice
                    ? "WeaponMenuRememberEnabled"
                    : "WeaponMenuRememberDisabled");

                reopenMenu(clicker);
            });
        };

        menu.AddOption(rememberBtn);
    }

    public bool TryGiveRememberedLoadout(IPlayer player)
    {
        var CFG = _weaponsCFG.CurrentValue;
        if (!CFG.EnableWeaponsMenu || !CFG.EnableRememberChoice || !CFG.AutoGiveRememberedChoiceOnRoundStart)
            return false;

        if (!IsEligibleHuman(player))
            return false;

        var preference = GetLoadoutPreference(player);
        if (!preference.RememberChoice)
            return false;

        if (!TryFindWeapon(CFG.PrimaryWeapons, preference.PrimaryClassname, out var primary) ||
            !TryFindWeapon(CFG.SecondaryWeapons, preference.SecondaryClassname, out var secondary))
            return false;

        GiveWeaponBySlot(player, primary.Classname, gear_slot_t.GEAR_SLOT_RIFLE);
        GiveWeaponBySlot(player, secondary.Classname, gear_slot_t.GEAR_SLOT_PISTOL);
        GiveDefaultGrenadesOnce(player);
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
        if (_globals.CanBuyWeaponsThisRound.TryGetValue(id, out bool canBuy))
        {
            if (!canBuy)
            {
                _helpers.SendChatT(player, "WeaponsMenuAlreadyUsed");
                return;
            }
        }
        else
        {
            _globals.CanBuyWeaponsThisRound[id] = true;
        }

        ShowPrimaryMenu(player);
    }

    public void ShowPrimaryMenuToAllEligible()
    {
        var CFG = _weaponsCFG.CurrentValue;
        if (!CFG.EnableWeaponsMenu ||
            (!CFG.GiveMenuOnRoundStart && !CFG.AutoGiveRememberedChoiceOnRoundStart))
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
            if (TryGiveRememberedLoadout(player))
                continue;

            if (CFG.GiveMenuOnRoundStart)
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

        menu.AddOption(ZPLMenuHelper.LargeText(_helpers.T(player, "WeaponMenuPrimarySelect")));

        AddRememberChoiceOption(menu, player, ShowPrimaryMenu);

        foreach (var weapon in CFG.PrimaryWeapons)
        {
            string weaponName = weapon.Name;
            string classname = weapon.Classname;

            var btn = ZPLMenuHelper.LargeButton(weaponName, "#5E98D9");

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
                    SavePrimaryChoice(clicker, weaponName, classname);
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

        menu.AddOption(ZPLMenuHelper.LargeText(_helpers.T(player, "WeaponMenuSecondarySelect")));

        AddRememberChoiceOption(menu, player, ShowSecondaryMenu);

        foreach (var weapon in CFG.SecondaryWeapons)
        {
            string weaponName = weapon.Name;
            string classname = weapon.Classname;

            var btn = ZPLMenuHelper.LargeButton(weaponName, "#5E98D9");

            btn.Click += async (_, args) =>
            {
                var clicker = args.Player;
                _core.Scheduler.NextTick(() =>
                {
                    if (!clicker.IsValid) return;

                    if (!IsWeaponSelectionWindowOpen(clicker, sendReason: true))
                        return;

                    SaveSecondaryChoice(clicker, weaponName, classname);
                    GiveWeaponBySlot(clicker, classname, gear_slot_t.GEAR_SLOT_PISTOL);
                    GiveDefaultGrenadesOnce(clicker);
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

    private void GiveDefaultGrenadesOnce(IPlayer player)
    {
        if (!_globals.WeaponGrenadesGivenThisRound.Add(player.PlayerID))
            return;

        GiveDefaultGrenades(player);
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
