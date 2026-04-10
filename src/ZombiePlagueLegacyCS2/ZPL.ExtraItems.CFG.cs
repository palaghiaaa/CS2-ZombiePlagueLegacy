namespace ZombiePlagueLegacyCS2;

/// <summary>Team restriction for an extra item.</summary>
public enum ExtraItemTeam
{
    /// <summary>Purchasable by humans only.</summary>
    Human,
    /// <summary>Purchasable by zombies only.</summary>
    Zombie,
    /// <summary>Purchasable by everyone.</summary>
    Both
}

/// <summary>A single extra item entry loaded from ZPLExtraItemsCFG.jsonc.</summary>
public class ExtraItemEntry
{
    /// <summary>Internal unique key (e.g. "armor", "he_grenade").</summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>Display name shown in the menu.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Price in ammo packs.</summary>
    public int Price { get; set; } = 0;
    /// <summary>Whether the item is enabled and shown in the menu.</summary>
    public bool Enable { get; set; } = true;
    /// <summary>Which team can purchase this item.</summary>
    public ExtraItemTeam Team { get; set; } = ExtraItemTeam.Human;
    /// <summary>
    /// Maximum number of times this item can be purchased per round.
    /// 0 (default) means unlimited.
    /// Mirrors zp_extra_antidote_limit / zp_extra_madness_limit / zp_extra_infbomb_limit
    /// from ZombiePlague CS 1.6.
    /// </summary>
    public int PurchaseLimit { get; set; } = 0;
}

/// <summary>Root configuration model for the extra items system.</summary>
public class ZPLExtraItemsCFG
{
    // ── Armor ──────────────────────────────────────────────────────────────────
    /// <summary>Armor given to the player (0–100).</summary>
    public int ArmorAmount { get; set; } = 100;

    // ── Multijump ─────────────────────────────────────────────────────────────
    /// <summary>Extra jumps added per Multijump purchase.</summary>
    public int MultijumpIncrement { get; set; } = 1;
    /// <summary>Maximum total extra jumps a player can accumulate.</summary>
    public int MultijumpMax { get; set; } = 3;

    // ── Zombie Madness ────────────────────────────────────────────────────────
    /// <summary>Duration in seconds of the Zombie Madness invulnerability.</summary>
    public float MadnessDuration { get; set; } = 10f;
    /// <summary>Glow red channel (0–255) applied while Zombie Madness is active.</summary>
    public byte MadnessGlowR { get; set; } = 255;
    /// <summary>Glow green channel (0–255) applied while Zombie Madness is active.</summary>
    public byte MadnessGlowG { get; set; } = 0;
    /// <summary>Glow blue channel (0–255) applied while Zombie Madness is active.</summary>
    public byte MadnessGlowB { get; set; } = 0;

    // ── Antidote ──────────────────────────────────────────────────────────────
    // (uses the existing TVaccine / MakeHuman logic; no extra scalar needed)

    // ── Knife Blink ───────────────────────────────────────────────────────────
    /// <summary>Number of blink charges given per purchase.</summary>
    public int KnifeBlinkCharges { get; set; } = 3;
    /// <summary>Maximum forward distance for a knife-blink teleport (units).</summary>
    public float KnifeBlinkDistance { get; set; } = 300f;
    /// <summary>Cooldown in seconds between blinks.</summary>
    public float KnifeBlinkCooldown { get; set; } = 2f;

    // ── Jetpack ───────────────────────────────────────────────────────────────
    /// <summary>Jetpack max fuel capacity (depletes while flying; default 250).</summary>
    public float JetpackMaxFuel { get; set; } = 250f;
    /// <summary>Upward thrust velocity applied each second while flying (units/s).</summary>
    public float JetpackThrustForce { get; set; } = 350f;
    /// <summary>Horizontal thrust velocity applied in the WASD movement direction while flying (units/s; default 300).</summary>
    public float JetpackHorizontalForce { get; set; } = 300f;
    /// <summary>Fuel units consumed per second while flying.</summary>
    public float JetpackFuelConsumeRate { get; set; } = 30f;

    // ── Revive Token ──────────────────────────────────────────────────────────
    /// <summary>Seconds after death before the revive token respawns the player.</summary>
    public float ReviveTokenRespawnDelay { get; set; } = 1.5f;

    // ── Tryder ────────────────────────────────────────────────────────────────
    /// <summary>Health given to a Tryder player.</summary>
    public int TryderHealth { get; set; } = 1000;
    /// <summary>Armor given to a Tryder player.</summary>
    public int TryderArmor { get; set; } = 500;
    /// <summary>Glow red channel (0–255) for the Tryder player.</summary>
    public byte TryderGlowR { get; set; } = 0;
    /// <summary>Glow green channel (0–255) for the Tryder player.</summary>
    public byte TryderGlowG { get; set; } = 127;
    /// <summary>Glow blue channel (0–255) for the Tryder player.</summary>
    public byte TryderGlowB { get; set; } = 255;

    // ── Ammo Packs ───────────────────────────────────────────────────────────
    /// <summary>Starting ammo packs given to a player when they connect.</summary>
    public int StartingAmmoPacks { get; set; } = 0;
    /// <summary>Ammo packs awarded to a human that survives the round.</summary>
    public int RoundSurviveReward { get; set; } = 3;
    /// <summary>Ammo packs awarded to a zombie that kills a human.</summary>
    public int ZombieKillReward { get; set; } = 2;
    /// <summary>
    /// Total damage a human must deal to zombies to earn one Ammo Pack reward.
    /// Set to 0 to disable the damage-based reward. (CS 1.6 reference: 500)
    /// </summary>
    public int HumanDamageRewardThreshold { get; set; } = 500;
    /// <summary>Ammo packs awarded each time a human crosses the damage threshold.</summary>
    public int HumanDamageReward { get; set; } = 1;

    // ── Item list ─────────────────────────────────────────────────────────────
    /// <summary>List of extra items shown in the menu.</summary>
    public List<ExtraItemEntry> Items { get; set; } = new();
}
