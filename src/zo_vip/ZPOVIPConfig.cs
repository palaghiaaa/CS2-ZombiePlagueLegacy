namespace ZPOVIP;

/// <summary>
/// Configuration model for the ZombieOutstandingCS2 VIP plugin.
/// Loaded from configs/plugins/ZPOVIP/ZPOVIP.jsonc (key "ZPOVIP").
/// Hot-reload supported: changes apply without restarting the server.
/// </summary>
public class ZPOVIPConfig
{
    // ── VIP Detection ─────────────────────────────────────────────────────────

    /// <summary>
    /// Comma-separated Swiftly permission flags that grant VIP status.
    /// A player matching ANY flag is treated as VIP.
    /// Leave empty ("") to make every player VIP (useful for testing).
    /// </summary>
    public string VIPPermission { get; set; } = "@zpovip/vip";

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Chat command for the VIP benefits menu (without leading ! or /).</summary>
    public string VipMenuCommand { get; set; } = "vip";

    /// <summary>Chat command for the online VIPs list menu.</summary>
    public string VipsListCommand { get; set; } = "vips";

    // ── VIP Benefits Menu ─────────────────────────────────────────────────────

    /// <summary>Title shown at the top of the !vip benefits menu.</summary>
    public string VipMenuTitle { get; set; } = "VIP Benefits";

    /// <summary>
    /// Lines displayed as non-selectable items in the !vip benefits menu.
    /// Supports SwiftlyS2 HTML colour tags.
    /// Set to an empty list ([]) to auto-generate lines from active perk settings.
    /// </summary>
    public List<string> BenefitLines { get; set; } =
    [
        "★ Armor on spawn",
        "★ Double Jump (extra mid-air jumps)",
        "★ No Fall Damage",
        "★ ×1.5 Damage vs Zombies",
        "★ AP reward every 500 damage dealt",
        "★ +2 AP per Zombie Kill",
        "★ Happy Hour: bonus AP & frags",
    ];

    // ── Chat ──────────────────────────────────────────────────────────────────

    /// <summary>When true, a chat message is broadcast when a VIP spawns for the first time each round.</summary>
    public bool JoinAnnounceEnabled { get; set; } = true;

    /// <summary>Prefix prepended to all ZPOVIP chat messages.</summary>
    public string ChatPrefix { get; set; } = "[VIP]";

    // ── Economy ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Wallet kind name in the Economy plugin used for ammo-pack rewards.
    /// Must match the wallet kind configured in ZombieOutstandingCS2 (default "ammo").
    /// </summary>
    public string WalletKind { get; set; } = "ammo";

    // ── Armor on Spawn ────────────────────────────────────────────────────────

    /// <summary>Minimum armor guaranteed to a VIP human on spawn. 0 = disabled. (AMXX: zp_vip_armor)</summary>
    public int ArmorAmount { get; set; } = 100;

    // ── Multi-Jump ────────────────────────────────────────────────────────────

    /// <summary>Extra mid-air jumps for VIP humans. 1 = double-jump. 0 = disabled. (AMXX: zp_vip_extrajumps)</summary>
    public int ExtraJumps { get; set; } = 1;

    /// <summary>Upward velocity impulse per extra jump (units/s).</summary>
    public float JumpVelocity { get; set; } = 300f;

    // ── No Fall Damage ────────────────────────────────────────────────────────

    /// <summary>When true, VIP humans take no fall/world damage. (AMXX: zp_vip_falldamage)</summary>
    public bool NoFallDamage { get; set; } = true;

    // ── Damage Multiplier ─────────────────────────────────────────────────────

    /// <summary>Multiplier on damage VIP humans deal to zombies. 1.0 = no bonus. (AMXX: zp_vip_damage)</summary>
    public float DamageMultiplier { get; set; } = 1.5f;

    /// <summary>When true, the multiplier is NOT applied to HE grenade damage.</summary>
    public bool ExcludeHEGrenade { get; set; } = true;

    // ── Damage Reward ─────────────────────────────────────────────────────────

    /// <summary>Damage to zombies needed to earn one AP reward batch. 0 = disabled. (AMXX: zp_vip_dmgreward_threshold)</summary>
    public int DamageRewardThreshold { get; set; } = 500;

    /// <summary>AP awarded per threshold of damage dealt.</summary>
    public int DamageRewardAmount { get; set; } = 1;

    // ── Kill Reward ───────────────────────────────────────────────────────────

    /// <summary>AP awarded to a VIP human per zombie kill. 0 = disabled. (AMXX: zp_vip_killammo)</summary>
    public int KillRewardAmount { get; set; } = 2;

    /// <summary>Whether the happy-hour AP bonus also applies to kill rewards.</summary>
    public bool KillRewardHappyHourBonus { get; set; } = true;

    // ── Happy Hour ────────────────────────────────────────────────────────────

    /// <summary>Enable time-based bonus rewards. (AMXX: zp_vip_happyhour_enable)</summary>
    public bool HappyHourEnabled { get; set; } = true;

    /// <summary>Start hour (24-h, 0-23). Set Start &gt; End to wrap overnight.</summary>
    public int HappyHourStart { get; set; } = 19;

    /// <summary>End hour (24-h, 0-23).</summary>
    public int HappyHourEnd { get; set; } = 8;

    /// <summary>Extra AP per zombie kill during Happy Hour.</summary>
    public int HappyHourBonusAP { get; set; } = 2;

    /// <summary>Extra score/frags per zombie kill during Happy Hour.</summary>
    public int HappyHourBonusFrags { get; set; } = 1;

    // ── Infect Reward ─────────────────────────────────────────────────────────

    /// <summary>
    /// When true, a VIP player who infects a human (as zombie) earns AP and/or health.
    /// Requires IZombieOutstandingAPI to be present. Disabled by default.
    /// (AMXX: zp_vip_infectammo / zp_vip_infecthealth)
    /// </summary>
    public bool InfectRewardsEnabled { get; set; } = false;

    /// <summary>AP awarded to VIP infector on successful infection.</summary>
    public int InfectRewardAP { get; set; } = 1;

    /// <summary>Health bonus for VIP infector on infection. 0 = disabled.</summary>
    public int InfectRewardHealth { get; set; } = 500;
}
