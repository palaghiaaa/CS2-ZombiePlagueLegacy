namespace ZombiePlagueLegacyCS2;

/// <summary>Represents a single entry in the weapon selection menu.</summary>
/// <remarks>Name is the display label; Classname is the CS2 weapon entity classname (e.g. weapon_ak47).</remarks>
public class WeaponEntry
{
    public string Name { get; set; } = string.Empty;
    public string Classname { get; set; } = string.Empty;
}

/// <summary>Configuration for the round-start weapon selection menu system.</summary>
public class ZPLWeaponsCFG
{
    /// <summary>Master switch – set false to disable the entire weapon menu feature.</summary>
    public bool EnableWeaponsMenu { get; set; } = true;
    /// <summary>When true, the primary weapon menu is automatically shown to eligible humans at round start when no remembered loadout is available.</summary>
    public bool GiveMenuOnRoundStart { get; set; } = false;
    /// <summary>When true, players can toggle a remembered primary + secondary weapon loadout from the weapon menus.</summary>
    public bool EnableRememberChoice { get; set; } = true;
    /// <summary>When true, remembered loadouts are given automatically at round start without opening the weapon menu.</summary>
    public bool AutoGiveRememberedChoiceOnRoundStart { get; set; } = true;
    /// <summary>When true, players can re-open weapon selection via the sw_buyweapons command (once per round).</summary>
    public bool AllowOpenFromGameMenu { get; set; } = true;

    /// <summary>
    /// When true, humans can open the weapons menu at any time during the round
    /// (e.g. after using an Antidote or late-joining). Default true.
    /// </summary>
    public bool AllowWeaponsMenuDuringRound { get; set; } = true;
    /// <summary>Reserve ammo amount set on a weapon when given via the menu (default: 9999).</summary>
    public int ReserveAmmoAmount { get; set; } = 9999;
    /// <summary>Primary weapons available in the primary weapon menu.</summary>
    public List<WeaponEntry> PrimaryWeapons { get; set; } = new();
    /// <summary>Secondary weapons (pistols) available in the secondary weapon menu.</summary>
    public List<WeaponEntry> SecondaryWeapons { get; set; } = new();
    /// <summary>CS2 weapon classnames for default grenades given after secondary selection (e.g. weapon_hegrenade).</summary>
    public List<string> DefaultGrenades { get; set; } = new();
}
