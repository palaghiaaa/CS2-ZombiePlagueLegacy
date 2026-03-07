namespace ZombieOutstandingCS2;

public enum GameModeType
{
    Normal,
    NormalInfection,
    MultiInfection,
    Nemesis,
    Survivor,
    Swarm,
    Plague,
    Assassin,
    Sniper,
    AVS,
    Hero
}
public class GameModeConfig
{
    public bool Enable { get; set; } = true;
    public string Name { get; set; } = string.Empty;
    public int Weight { get; set; } = 100;
    public bool EnableInfiniteClipMode { get; set; } = true;
    public bool ZombieCanReborn { get; set; } = true;
    
}

public class NemesisModeConfig : GameModeConfig
{
    public string NemesisNames { get; set; } = string.Empty;
    /// <summary>
    /// HP assigned to the Nemesis at round start.
    /// Balanced for CS2: tanky enough that a single player cannot solo it,
    /// but a coordinated team of 5+ players can bring it down in a round.
    /// Set to 0 to leave the Nemesis with its special-class default HP.
    /// </summary>
    public int NemesisHealth { get; set; } = 120000;
}

public class NormalInfectionModeConfig : GameModeConfig
{
    public string MotherZombieNames { get; set; } = string.Empty;
    public int MotherZombieCount { get; set; } = 1;
}
public class MultiInfectionModeConfig : GameModeConfig
{
    public string MotherZombieNames { get; set; } = string.Empty;
    public int MotherZombieCount { get; set; } = 5;
}

public class SurvivorModeConfig : GameModeConfig
{
    public string SurvivorNames { get; set; } = string.Empty;
    /// <summary>
    /// HP assigned to the Survivor at round start.
    /// 8 000 HP lets one human endure sustained zombie attacks long enough
    /// to be a real threat without being unkillable.
    /// Set to 0 to leave the Survivor with human base HP.
    /// </summary>
    public int SurvivorHealth { get; set; } = 8000;
    public float SurvivorSpeed { get; set; } = 3.0f;
    public float SurvivorGravity { get; set; } = 3.0f;
    public float SurvivorDamage { get; set; } = 5.0f;
    public string SurvivorWeapon{ get; set; } = string.Empty;
    public string CustomWeaponName { get; set; } = string.Empty;
    public string ModelsPath { get; set; } = string.Empty;
}

public class AssassinModeConfig : GameModeConfig
{
    public string AssassinNames { get; set; } = string.Empty;
    /// <summary>
    /// HP assigned to the Assassin at round start.
    /// 24 000 HP makes the Assassin a mid-tier threat: much harder to kill than a
    /// normal zombie but not as tanky as the Nemesis.
    /// Set to 0 to leave the Assassin with its special-class default HP.
    /// </summary>
    public int AssassinHealth { get; set; } = 24000;
    /// <summary>Distance (units) within which the Assassin becomes visible to humans.</summary>
    public float InvisibilityDist { get; set; } = 200.0f;
}

public class SniperModeConfig : GameModeConfig
{
    public string SniperNames { get; set; } = string.Empty;
    /// <summary>
    /// HP assigned to the Sniper at round start.
    /// 5 000 HP is more fragile than the Survivor, which fits the glass-cannon
    /// sniper role: extreme damage output but dies quickly if zombies get close.
    /// Set to 0 to leave the Sniper with human base HP.
    /// </summary>
    public int SniperHealth { get; set; } = 5000;
    public float SniperSpeed { get; set; } = 3.0f;
    public float SniperGravity { get; set; } = 3.0f;
    public float SniperDamage { get; set; } = 10.0f;
    /// <summary>
    /// When true, a single shot from the sniper's weapon instantly kills any zombie,
    /// regardless of the zombie's current HP (replicating CS 1.6 sniper mode behavior).
    /// When false, damage is multiplied by SniperDamage instead.
    /// </summary>
    public bool OneShotKill { get; set; } = true;
    public string SniperWeapon { get; set; } = string.Empty;
    public string CustomWeaponName { get; set; } = string.Empty;
    public string ModelsPath { get; set; } = string.Empty;
}

public class PlagueModeConfig : GameModeConfig
{
    public string NemesisNames { get; set; } = string.Empty;
    public string SurvivorNames { get; set; } = string.Empty;
}

public class AVSConfig : GameModeConfig
{
    public string AssassinNames { get; set; } = string.Empty;
    public string SniperNames { get; set; } = string.Empty;
}

public class HeroConfig : GameModeConfig
{
    public int HeroCount { get; set; }
    public string HeroNames { get; set; } = string.Empty;
    public int HeroHealth { get; set; } = 500;
    public float HeroSpeed { get; set; } = 3.0f;
    public float HeroGravity { get; set; } = 3.0f;
    public float HeroDamage { get; set; } = 10.0f;
    public string ModelsPath { get; set; } = string.Empty;

}
public class FogConfig
{
    /// <summary>Set to true to enable server-wide fog on every map load.</summary>
    public bool Enable { get; set; } = true;
    /// <summary>Primary fog colour – red component (0-255). Dark grey-green for zombie horror.</summary>
    public int ColorR { get; set; } = 100;
    /// <summary>Primary fog colour – green component (0-255).</summary>
    public int ColorG { get; set; } = 110;
    /// <summary>Primary fog colour – blue component (0-255).</summary>
    public int ColorB { get; set; } = 100;
    /// <summary>Distance at which the fog begins (game units).</summary>
    public float StartDist { get; set; } = 128f;
    /// <summary>Distance at which the fog reaches maximum density (game units).</summary>
    public float EndDist { get; set; } = 2048f;
    /// <summary>Maximum fog opacity (0.0 = invisible, 1.0 = fully opaque).</summary>
    public float MaxDensity { get; set; } = 0.9f;
    /// <summary>Fog density fall-off exponent.</summary>
    public float Exponent { get; set; } = 1.0f;
}

public class ZOMainCFG
{
    public float RoundReadyTime { get; set; } = 25f;
    public float RoundTime { get; set; } = 3;

    public NormalInfectionModeConfig NormalInfection { get; set; } = new();
    public MultiInfectionModeConfig MultiInfection { get; set; } = new();
    public NemesisModeConfig Nemesis { get; set; } = new();
    public SurvivorModeConfig Survivor { get; set; } = new();
    public GameModeConfig Swarm { get; set; } = new();
    public PlagueModeConfig Plague { get; set; } = new();
    public AssassinModeConfig Assassin { get; set; } = new();
    public SniperModeConfig Sniper { get; set; } = new();
    public AVSConfig AVS { get; set; } = new();
    public HeroConfig Hero { get; set; } = new();

    public string HumandefaultModel { get; set; } = string.Empty;
    public int HumanMaxHealth { get; set; } = 225;
    public bool EnableDamageHud { get; set; } = true;
    public bool EnableStatusHud { get; set; } = true;
    public float HumanInitialSpeed { get; set; } = 1.0f;
    public float HumanInitialGravity { get; set; } = 0.8f;
    public float HumanKnockBackHeadMultiply { get; set; } = 2.0f;
    public float HumanKnockBackBodyMultiply { get; set; } = 1.0f;
    public float HumanKnockBackGroundMultiply { get; set; } = 1.0f;
    public float HumanKnockBackAirMultiply { get; set; } = 0.5f;
    public float HumanHeroKnockBackMultiply { get; set; } = 1.0f;
    public bool EnableInfiniteReserveAmmo { get; set; } = true;
    public bool EnableWeaponNoRecoil { get; set; } = true;
    public string HumanSpawnPoints { get; set; } = string.Empty;
    public string ZombieSpawnPoints { get; set; } = string.Empty;
    public float KnockZombieForce { get; set; } = 250f;
    public float StunZombieTime { get; set; } = 0.1f;
    public string TVaccineSound { get; set; } = string.Empty;
    public float TVirusGrenadeRange { get; set; } = 300.0f;
    public bool TVirusCanInfectHero { get; set; } = true;
    public string TVirusGrenadeSound { get; set; } = string.Empty;
    public string AddHealthSound { get; set; } = string.Empty;
    public bool FireGrenade { get; set; } = true;
    public bool SpawnGiveFireGrenade { get; set; } = true;
    public float FireGrenadeRange { get; set; } = 300.0f;
    public float FireGrenadeDmg { get; set; } = 500f;
    public float FireDmg { get; set; } = 5f;
    public float FireGrenadeDuration { get; set; } = 8f;
    public string FireGrenadeSound { get; set; } = string.Empty;
    public bool SpawnGiveIncGrenade { get; set; } = true;
    public bool LightGrenade { get; set; } = true;
    public bool SpawnGiveLightGrenade { get; set; } = true;
    public float LightGrenadeRange { get; set; } = 1000f;
    public float LightGrenadeDuration { get; set; } = 30f;
    public string LightGrenadeSound { get; set; } = string.Empty;
    public bool FreezeGrenade { get; set; } = true;
    public bool SpawnGiveFreezeGrenade { get; set; } = true;
    public float FreezeGrenadeRange { get; set; } = 300;
    public float FreezeGrenadeDuration { get; set; } = 6f;
    public string FreezeGrenadeSound { get; set; } = string.Empty;
    public bool TelportGrenade { get; set; } = true;
    public bool SpawnGiveTelportGrenade { get; set; } = true;
    public bool CanUseScbaSuit { get; set; } = true;
    public string ScbaSuitGetSound { get; set; } = string.Empty;
    public string ScbaSuitBrokenSound { get; set; } = string.Empty;
    public string ZombieClassCommand { get; set; } = "sw_zclass";
    public string AdminMenuItemCommand { get; set; } = "sw_zadmin";
    public string MainMenuCommand { get; set; } = "sw_zmenu";
    public string ExtraItemsCommand { get; set; } = "sw_zextra";
    public string BuyWeaponsCommand { get; set; } = "sw_buyweapons";
    public string MineMenuCommand { get; set; } = "sw_mine";
    public string AdminMenuPermission { get; set; } = "";
    public string AmbSound { get; set; } = string.Empty;
    public float AmbSoundLoopTime { get; set; } = 60.0f;
    public float AmbSoundVolume { get; set; } = 0.6f;
    public string PrecacheAmbSound { get; set; } = string.Empty;

    // ── Custom round interval ─────────────────────────────────────────────────
    /// <summary>
    /// Minimum number of consecutive Normal-Infection rounds that must be played
    /// before a custom game mode (Nemesis, Survivor, etc.) is allowed to be chosen.
    /// Set to 0 (default) to use weights without any restriction.
    /// Example: 2 means at least 2 normal rounds between each custom round.
    /// </summary>
    public int NormalRoundsInterval { get; set; } = 0;

    // ── Solo / low-population guard ───────────────────────────────────────────
    /// <summary>
    /// Minimum number of alive human players required before
    /// <see cref="SelectMotherZombie"/> will select a mother zombie.
    /// When the human candidate count is strictly less than this value,
    /// infection is skipped entirely and the round runs to its natural timer end.
    ///
    /// WHY 2 IS THE CORRECT VALUE FOR SOLO PROTECTION:
    ///   The check is <c>candidates.Count &lt; MinPlayersForInfection</c>.
    ///   With 1 human and MinPlayersForInfection = 1:  1 &lt; 1 = false → infection
    ///   proceeds → solo player becomes zombie → humanCount == 0 → FakeZombieWins
    ///   → round ends immediately.  Setting it to 1 is effectively the same as
    ///   having no guard at all.
    ///   With 1 human and MinPlayersForInfection = 2:  1 &lt; 2 = true → infection
    ///   is skipped → MotherZombieWasSelected stays false → CheckRoundWinConditions
    ///   returns early → round runs to the timer.
    ///
    /// Recommended values:
    ///   2 — solo / test servers (default): infection only starts with ≥ 2 humans.
    ///   1 — effectively disables the guard (same as the old behaviour).
    /// </summary>
    public int MinPlayersForInfection { get; set; } = 2;

    // ── Debug / chat settings ────────────────────────────────────────────────
    /// <summary>Prefix prepended to chat messages sent by the plugin.</summary>
    public string ChatPrefix { get; set; } = "[ZO]";
    /// <summary>When true, command invocations are logged to the server console.</summary>
    public bool EnableCommandDebugLogs { get; set; } = false;
    /// <summary>When true, command invocations produce a chat reply visible to the invoking player.</summary>
    public bool EnableCommandDebugChatReply { get; set; } = false;

    // ── Economy backend settings ──────────────────────────────────────────────
    /// <summary>
    /// Wallet kind name registered in the Economy plugin.
    /// Defaults to "ammo". Must match a wallet kind configured in Economy's config.
    /// </summary>
    public string EconomyWalletKind { get; set; } = "ammo";

    // ── Fog ──────────────────────────────────────────────────────────────────
    /// <summary>
    /// Server-wide fog applied automatically on every map load and to every
    /// player that spawns. Set Enable to false to disable completely.
    /// </summary>
    public FogConfig Fog { get; set; } = new();

    // ── Skybox ───────────────────────────────────────────────────────────────
    /// <summary>
    /// Override the map's skybox on every map load.
    /// Set to a sky material path, e.g. "skybox/sky_dust_hdr".
    /// Leave empty ("") to keep the map's default skybox.
    /// </summary>
    public string Skybox { get; set; } = string.Empty;

}
