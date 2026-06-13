namespace ZombiePlagueLegacyCS2;

/// <summary>
/// Root configuration for the laser trip-mine system, loaded from ZOMine.jsonc.
/// Supports multiple mine types, each with its own model, laser, explosion and sound settings.
/// </summary>
public class ZPLMineCFG
{
    public List<LaserMine> MineList { get; set; } = new();

    /// <summary>
    /// Radius in engine units within which a zombie's knife swing damages a mine.
    /// Increase this if zombies have trouble hitting mines in gameplay.
    /// </summary>
    public float ZombieAttackRange { get; set; } = 80f;

    public class LaserMine
    {
        /// <summary>Whether this mine type is enabled and visible in the mine menu.</summary>
        public bool Enable { get; set; } = true;
        /// <summary>Unique display name shown in the mine selection menu.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Workshop model path for the mine body (e.g. "models/stk_sentry_guns/lasermine/stk_lasermines_one.vmdl").</summary>
        public string Model { get; set; } = string.Empty;
        /// <summary>
        /// true  → Explosive laser tripmine: explodes when the laser beam is touched.
        /// false → Laser beam trap: continuously deals damage to players crossing the beam.
        /// </summary>
        public bool CanExplorer { get; set; } = false;
        /// <summary>
        /// true  → Players on the owner's team can trigger the mine.
        /// false → Only enemy players trigger the mine.
        /// </summary>
        public bool CanOwnerTeamTrigger { get; set; } = false;
        /// <summary>Beam-trap only: seconds between each damage/knockback tick (e.g. 0.1 = 10 times/s).</summary>
        public float LaserRate { get; set; } = 0.1f;
        /// <summary>Beam-trap only: damage per tick applied to players inside the beam.</summary>
        public float LaserDamage { get; set; } = 10f;
        /// <summary>Beam-trap only: knockback impulse applied to players inside the beam.</summary>
        public float LaserKnockBack { get; set; } = 100f;
        /// <summary>Beam-trap only: temporary movement modifier applied to targets hit by the beam.</summary>
        public float LaserSlowModifier { get; set; } = 0.45f;
        /// <summary>Beam-trap only: seconds before the temporary movement modifier is restored.</summary>
        public float LaserSlowDuration { get; set; } = 0.35f;
        /// <summary>Explosive mine only: explosion radius (engine units).</summary>
        public int ExplorerRadius { get; set; } = 360;
        /// <summary>Explosive mine only: maximum explosion damage at the mine centre.</summary>
        public int ExplorerDamage { get; set; } = 2600;
        /// <summary>Team restriction: "all" = everyone, "ct" = CT only, "t" = T only.</summary>
        public string Team { get; set; } = "ct";
        /// <summary>Purchase price in cash ($). 0 = free.</summary>
        public int Price { get; set; } = 0;
        /// <summary>Maximum simultaneously planted mines for this type per player. 0 = unlimited.</summary>
        public int Limit { get; set; } = 2;
        /// <summary>Required permission flag to place this mine. Empty = no restriction.</summary>
        public string Permissions { get; set; } = string.Empty;
        /// <summary>Glow outline color "R,G,B,A" (0–255). Empty = no glow.</summary>
        public string GlowColor { get; set; } = "0,255,0,255";
        /// <summary>Laser beam color "R,G,B,A" (0–255).</summary>
        public string LaserColor { get; set; } = "0,255,0,255";
        /// <summary>Laser beam visual width (engine units).</summary>
        public float LaserSize { get; set; } = 1f;
        /// <summary>Sound event played when the mine is planted.</summary>
        public string MineOpenSound { get; set; } = string.Empty;
        /// <summary>Sound event played when the laser beam activates.</summary>
        public string LaserOpenSound { get; set; } = string.Empty;
        /// <summary>Sound event played when the beam hits a player.</summary>
        public string LaserTouchSound { get; set; } = string.Empty;
        /// <summary>Sound event file to precache (e.g. "soundevents/n4a_csdm_sentry.vsndevts").</summary>
        public string PrecacheSoundEvent { get; set; } = string.Empty;
        /// <summary>Yaw angle correction in degrees applied to the mine model to fix orientation.</summary>
        public float ModelAngleFix { get; set; } = 90f;
        /// <summary>
        /// HP pool for this mine. Zombies can melee-attack the mine to damage it; when HP reaches 0 it explodes.
        /// 0 (default) = invincible mine (cannot be destroyed by zombies).
        /// </summary>
        public int MineHealth { get; set; } = 0;

        /// <summary>
        /// Damage dealt to this mine per zombie knife swing that lands within ZombieAttackRange.
        /// Only applies when MineHealth > 0.
        /// </summary>
        public int ZombieAttackDamage { get; set; } = 150;
    }
}
