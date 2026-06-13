using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using static ZombiePlagueLegacyCS2.ZPLZombieClassCFG;


namespace ZombiePlagueLegacyCS2;

public class ZPLGlobals
{
    public bool ServerIsEmpty = true;
    public bool GameStart { get; set; }
    public bool SafeRoundStart { get; set; }
    public bool GameInfiniteClipMode { get; set; }
    public bool IsheroSetup { get; set; }
    public int Countdown { get; set; }

    public bool[] InSwing { get; } = new bool[65];

    public ZPLVoxCFG.RoundVox? RoundVoxGroup = null;

    public Dictionary<int, bool> IsZombie = new Dictionary<int, bool>();

    public Dictionary<int, bool> IsMother = new Dictionary<int, bool>();
    public Dictionary<int, bool> IsSurvivor = new Dictionary<int, bool>();
    public Dictionary<int, bool> IsSniper = new Dictionary<int, bool>();
    public Dictionary<int, bool> IsNemesis = new Dictionary<int, bool>();
    public Dictionary<int, bool> IsAssassin = new Dictionary<int, bool>();
    public Dictionary<int, bool> IsHero = new Dictionary<int, bool>();

    public CancellationTokenSource? g_hRoundEndTimer { get; set; } = null;
    public CancellationTokenSource? g_hCountdown { get; set; } = null;

    // ── Round-state flags ─────────────────────────────────────────────────────
    /// <summary>True once SwitchMode() has been called this round (infection started).</summary>
    public bool InfectionStartedThisRound { get; set; } = false;
    /// <summary>True when an admin forced a custom mode this round.</summary>
    public bool AdminForcedModeThisRound { get; set; } = false;
    /// <summary>
    /// True when at least one mother zombie was successfully selected this round.
    /// Prevents CheckRoundWinConditions() from ending the round immediately when
    /// there are not enough players to run a proper infection (e.g. solo testing).
    /// </summary>
    public bool MotherZombieWasSelected { get; set; } = false;
    /// <summary>
    /// Number of consecutive Normal-Infection rounds played since the last custom round.
    /// Used together with <see cref="ZPLMainCFG.NormalRoundsInterval"/> to throttle custom rounds.
    /// </summary>
    public int NormalRoundsStreak { get; set; } = 0;

    public Dictionary<int, ZombieIdleState> g_ZombieIdleStates = new();
    public CancellationTokenSource? g_IdleTimer { get; set; } = null;

    public Dictionary<int, (int endTick, int fallEndTick, Vector originalVelocity)> jumpBoostState = new();

    public Dictionary<int, ZombieRegenState> g_ZombieRegenStates = new();

    public CancellationTokenSource? g_ZombieRegenTimer = null;
    public CancellationTokenSource? g_ActivePlayerRewardTimer = null;

    public CancellationTokenSource? g_hAmbMusic { get; set; } = null;

    public Dictionary<int, bool> g_IsInvisible = new();

    public Dictionary<int, GlowEntity> GlowEntity = new Dictionary<int, GlowEntity>();

    public CancellationTokenSource? AssassinTimer;

    public Dictionary<int, bool> ThrowerIsZombie = new();

    public Dictionary<int, (CHandle<CParticleSystem> particle, CancellationTokenSource timer)> ActiveBurns = new();

    public Dictionary<uint, CHandle<COmniLight>> activeLights = new Dictionary<uint, CHandle<COmniLight>>();
    public Dictionary<uint, CancellationTokenSource> lightTimers = new Dictionary<uint, CancellationTokenSource>();

    public readonly Dictionary<SpawnType, List<SpawnPointData>> spawnCache= new();

    public Dictionary<int, float> StopZombieTimers = new();

    /// <summary>
    /// Active freeze particle handles per zombie PlayerID.
    /// Killed and removed when the freeze wears off or is cleared manually.
    /// </summary>
    public Dictionary<int, List<CHandle<CParticleSystem>>> FreezeParticles = new();

    /// <summary>
    /// Active freeze beam ring handles per zombie PlayerID (3 rings × 16 segments each).
    /// Killed by KillFreezeParticles when the freeze ends.
    /// </summary>
    public Dictionary<int, List<List<CHandle<CBeam>>>> FreezeBeamHandles = new();

    /// <summary>
    /// Tracks which zombie players currently have silent steps active.
    /// Set on posszombie for classes with SilentSteps=true, cleared on death/disconnect.
    /// </summary>
    public HashSet<int> SilentStepsActive = new();

    /// <summary>Pending infect-glow removal timers: playerID → CancellationTokenSource.</summary>
    public Dictionary<int, CancellationTokenSource> InfectGlowTimers = new();

    /// <summary>Nemesis frost charges remaining per player. Reset on round start.</summary>
    public Dictionary<int, int> NemesisFrostCharges = new();
    /// <summary>Next allowed frost use time (CurrentTime) per player.</summary>
    public Dictionary<int, float> NemesisFrostCooldown = new();

    /// <summary>Tracks which human players currently own a parachute.</summary>
    public HashSet<int> HasParachute = new();
    /// <summary>Original gravity scale captured while parachute slow-fall is active.</summary>
    public Dictionary<int, float> ParachuteRestoreGravity = new();

    public Dictionary<int, bool> ScbaSuit = new Dictionary<int, bool>();
    public Dictionary<int, bool> GodState = new Dictionary<int, bool>();
    public Dictionary<int, bool> InfiniteAmmoState = new Dictionary<int, bool>();

    public Dictionary<int, bool> CanBuyWeaponsThisRound = new Dictionary<int, bool>();
    public Dictionary<ulong, WeaponLoadoutPreference> WeaponLoadoutPreferences = new Dictionary<ulong, WeaponLoadoutPreference>();
    public Dictionary<ulong, UserPreferenceSettings> UserPreferences = new Dictionary<ulong, UserPreferenceSettings>();
    public HashSet<int> WeaponGrenadesGivenThisRound = new HashSet<int>();

    // ── Extra Items / Ammo Packs ──────────────────────────────────────────────
    /// <summary>
    /// Accumulated damage dealt by each human to zombies this round (keyed by PlayerID).
    /// Reset at round end and on disconnect. Used for damage-based AP reward.
    /// </summary>
    public Dictionary<int, int> DamageAccumulator = new Dictionary<int, int>();

    /// <summary>
    /// How many times each player has purchased each extra item this round.
    /// Outer key = PlayerID, inner key = item Key string.
    /// Used to enforce per-item PurchaseLimit (zp_extra_*_limit equivalent).
    /// Reset at round start and on disconnect.
    /// </summary>
    public Dictionary<int, Dictionary<string, int>> ItemPurchaseCount = new Dictionary<int, Dictionary<string, int>>();

    // ── Multijump ─────────────────────────────────────────────────────────────
    /// <summary>Number of extra jumps currently available to the player this round.</summary>
    public Dictionary<int, int> ExtraJumps = new Dictionary<int, int>();
    /// <summary>Jumps consumed since the player last touched the ground.</summary>
    public Dictionary<int, int> JumpsUsed = new Dictionary<int, int>();

    // ── Knife Blink ───────────────────────────────────────────────────────────
    /// <summary>Remaining knife-blink charges for the player.</summary>
    public Dictionary<int, int> KnifeBlinkCharges = new Dictionary<int, int>();
    /// <summary>Environment.TickCount64 (ms) at which the player's blink cooldown expires.</summary>
    public Dictionary<int, long> KnifeBlinkCooldownEnd = new Dictionary<int, long>();

    // ── Zombie Madness ────────────────────────────────────────────────────────
    /// <summary>True while a zombie has an active Madness (invulnerability) buff.</summary>
    public Dictionary<int, bool> ZombieMadnessActive = new Dictionary<int, bool>();

    // ── Multijump input tracking ──────────────────────────────────────────────
    /// <summary>True if the player had the jump (Space) button pressed in the previous tick.</summary>
    public Dictionary<int, bool> PrevJumpPressed = new Dictionary<int, bool>();
    /// <summary>True if the player was on the ground in the previous tick (used for leap detection).</summary>
    public Dictionary<int, bool> PrevOnGround = new Dictionary<int, bool>();

    // ── Zombie Leap ───────────────────────────────────────────────────────────
    /// <summary>Environment.TickCount64 (ms) at which the player's leap cooldown expires.</summary>
    public Dictionary<int, long> LeapCooldownEnd = new Dictionary<int, long>();

    // ── Spawn Protection ─────────────────────────────────────────────────────
    /// <summary>Environment.TickCount64 (ms) until which a freshly-spawned player is invulnerable.</summary>
    public Dictionary<int, long> SpawnProtectionEndTime = new Dictionary<int, long>();

    // ── Anti-repeat special-role tracking ────────────────────────────────────
    /// <summary>
    /// SteamIDs of players who held a special role (mother zombie, nemesis, survivor,
    /// sniper, assassin) in the PREVIOUS round.  These players are deprioritised when
    /// picking special roles this round so the same player is not selected twice in a row.
    /// </summary>
    public HashSet<ulong> SpecialRoleLastRound = new HashSet<ulong>();
    /// <summary>
    /// SteamIDs of players who have been assigned a special role THIS round.
    /// Copied into <see cref="SpecialRoleLastRound"/> at the start of the next round.
    /// </summary>
    public HashSet<ulong> SpecialRoleThisRound = new HashSet<ulong>();

    // ── Jetpack ───────────────────────────────────────────────────────────────
    /// <summary>True if the player currently owns a jetpack.</summary>
    public Dictionary<int, bool> HasJetpack = new Dictionary<int, bool>();
    /// <summary>Remaining fuel for the player's jetpack.</summary>
    public Dictionary<int, float> JetpackFuel = new Dictionary<int, float>();
    /// <summary>Server time (CurrentTime) at which fuel was last consumed.</summary>
    public Dictionary<int, float> JetpackLastFuelTime = new Dictionary<int, float>();
    /// <summary>Server time at which the player last actively used thrust. Used for recharge delay.</summary>
    public Dictionary<int, float> JetpackLastThrustTime = new Dictionary<int, float>();

    // ── Laser Trip Mines (HLT-style) ──────────────────────────────────────────
    /// <summary>Per-mine think (RepeatBySeconds) cancellation tokens, keyed by entity handle Raw.</summary>
    public Dictionary<uint, CancellationTokenSource> MineThink = new();
    /// <summary>Mine configuration snapshot keyed by entity handle Raw.</summary>
    public Dictionary<uint, MineData> MineData = new();
    /// <summary>Sets of active mine entity handles per player, per mine-type name, keyed by SteamID.</summary>
    public Dictionary<ulong, Dictionary<string, HashSet<uint>>> PlayerMineCounts = new();
    /// <summary>Beam entity handle Raw for each mine, keyed by mine entity handle Raw.</summary>
    public Dictionary<uint, uint> MineBeam = new();
    /// <summary>Current HP for mines that have a MineHealth > 0, keyed by mine entity handle Raw.</summary>
    public Dictionary<uint, int> MineCurrentHP = new();
    /// <summary>PlayerID of the mine's owner, keyed by mine entity handle Raw.</summary>
    public Dictionary<uint, int> MineOwnerPlayerID = new();

    // ── Revive Token ──────────────────────────────────────────────────────────
    /// <summary>True if the player has an active revive token that will trigger on death.</summary>
    public Dictionary<int, bool> HasReviveToken = new Dictionary<int, bool>();

    // ── Extra Items – per-player single-round states ──────────────────────────
    /// <summary>True while a player has purchased the Unlimited Clip extra item this round.</summary>
    public Dictionary<int, bool> InfiniteClipState = new Dictionary<int, bool>();
    /// <summary>True while a player has purchased the No Recoil extra item this round.</summary>
    public Dictionary<int, bool> ExtraNoRecoilState = new Dictionary<int, bool>();
    /// <summary>True while a player has purchased the Tryder extra item this round.</summary>
    public Dictionary<int, bool> TryderState = new Dictionary<int, bool>();

    // ── Fog ──────────────────────────────────────────────────────────────────
    /// <summary>Handle to the global env_fog_controller entity, reused across rounds.</summary>
    public CHandle<CFogController> GlobalFogController;
    /// <summary>Per-player clear fog controller used when a user disables fog locally.</summary>
    public CHandle<CFogController> ClearFogController;

}
public class ZombieRegenState
{
    public int PlayerID;
    public int RegenAmount;       // 每次回血量
    public float RegenInterval;   // 间隔秒数
    public float NextRegenTime;   // 下一次回血时间戳（秒）
}

public class WeaponLoadoutPreference
{
    public bool RememberChoice { get; set; }
    public string PrimaryName { get; set; } = string.Empty;
    public string PrimaryClassname { get; set; } = string.Empty;
    public string SecondaryName { get; set; } = string.Empty;
    public string SecondaryClassname { get; set; } = string.Empty;
}

public class UserPreferenceSettings
{
    public bool VoxSounds { get; set; } = true;
    public bool Fog { get; set; } = true;
    public bool Flashlight { get; set; } = true;
    public bool Tags { get; set; } = true;
    public bool Ads { get; set; } = true;
    public bool HidePlayers { get; set; }
    public bool VipRewardMessages { get; set; } = true;

    public bool Get(string key, bool defaultValue = true)
    {
        return key switch
        {
            ZPLUserPreferenceKeys.VoxSounds => VoxSounds,
            ZPLUserPreferenceKeys.Fog => Fog,
            ZPLUserPreferenceKeys.Flashlight => Flashlight,
            ZPLUserPreferenceKeys.Tags => Tags,
            ZPLUserPreferenceKeys.Ads => Ads,
            ZPLUserPreferenceKeys.HidePlayers => HidePlayers,
            ZPLUserPreferenceKeys.VipRewardMessages => VipRewardMessages,
            _ => defaultValue
        };
    }

    public bool Set(string key, bool enabled)
    {
        switch (key)
        {
            case ZPLUserPreferenceKeys.VoxSounds:
                VoxSounds = enabled;
                return true;
            case ZPLUserPreferenceKeys.Fog:
                Fog = enabled;
                return true;
            case ZPLUserPreferenceKeys.Flashlight:
                Flashlight = enabled;
                return true;
            case ZPLUserPreferenceKeys.Tags:
                Tags = enabled;
                return true;
            case ZPLUserPreferenceKeys.Ads:
                Ads = enabled;
                return true;
            case ZPLUserPreferenceKeys.HidePlayers:
                HidePlayers = enabled;
                return true;
            case ZPLUserPreferenceKeys.VipRewardMessages:
                VipRewardMessages = enabled;
                return true;
            default:
                return false;
        }
    }
}

public class ZombieIdleState
{
    public int PlayerID;
    public float IdleInterval;   // 间隔秒数
    public float NextIdleTime;   // 下一次Idle时间
}

public enum SpawnType
{
    CT,
    T,
    DM
}
public struct SpawnPointData
{
    public Vector Position;
    public QAngle Angle;
}

public class GlowEntity
{
    public CHandle<CBaseModelEntity> Relay { get; set; }
    public CHandle<CBaseModelEntity> Glow { get; set; }
}

/// <summary>
/// Snapshot of a mine type's configuration stored alongside the live entity,
/// so the think loop can access the data without re-reading the config monitor.
/// Mirrors the field set from ZPLMineCFG.LaserMine.
/// </summary>
public class MineData
{
    public string Name                { get; set; } = string.Empty;
    public string Model               { get; set; } = string.Empty;
    public bool   CanExplorer         { get; set; }
    public bool   CanOwnerTeamTrigger { get; set; }
    public float  LaserRate           { get; set; }
    public float  LaserDamage         { get; set; }
    public float  LaserKnockBack      { get; set; }
    public float  LaserSlowModifier   { get; set; }
    public float  LaserSlowDuration   { get; set; }
    public int    ExplorerRadius      { get; set; }
    public int    ExplorerDamage      { get; set; }
    public string Team                { get; set; } = string.Empty;
    public int    Price               { get; set; }
    public int    Limit               { get; set; }
    public string Permissions         { get; set; } = string.Empty;
    public string GlowColor           { get; set; } = string.Empty;
    public string LaserColor          { get; set; } = string.Empty;
    public float  LaserSize           { get; set; }
    public string MineOpenSound       { get; set; } = string.Empty;
    public string LaserOpenSound      { get; set; } = string.Empty;
    public string LaserTouchSound     { get; set; } = string.Empty;
    public float  ModelAngleFix       { get; set; }
    /// <summary>Maximum HP for this mine (0 = invincible).</summary>
    public int    MineHealth          { get; set; }
    /// <summary>Damage per zombie knife swing that lands within ZombieAttackRange.</summary>
    public int    ZombieAttackDamage  { get; set; }
    /// <summary>World position where this mine was planted (set after NextWorldUpdate teleport).</summary>
    public Vector SpawnOrigin         { get; set; }
}
