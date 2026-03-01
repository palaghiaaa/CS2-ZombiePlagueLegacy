using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using static ZombieOutstandingCS2.ZOZombieClassCFG;


namespace ZombieOutstandingCS2;

public class ZOGlobals
{
    public bool ServerIsEmpty = true;
    public bool GameStart { get; set; }
    public bool SafeRoundStart { get; set; }
    public bool GameInfiniteClipMode { get; set; }
    public bool IsheroSetup { get; set; }
    public int Countdown { get; set; }

    public bool[] InSwing { get; } = new bool[65];

    public ZOVoxCFG.RoundVox? RoundVoxGroup = null;

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
    /// Number of consecutive Normal-Infection rounds played since the last custom round.
    /// Used together with <see cref="ZOMainCFG.NormalRoundsInterval"/> to throttle custom rounds.
    /// </summary>
    public int NormalRoundsStreak { get; set; } = 0;

    public Dictionary<int, ZombieIdleState> g_ZombieIdleStates = new();
    public CancellationTokenSource? g_IdleTimer { get; set; } = null;

    public Dictionary<IPlayer, (int endTick, int fallEndTick, Vector originalVelocity)> jumpBoostState = new();

    public Dictionary<int, ZombieRegenState> g_ZombieRegenStates = new();

    public CancellationTokenSource? g_ZombieRegenTimer = null;

    public CancellationTokenSource? g_hAmbMusic { get; set; } = null;

    public Dictionary<int, bool> g_IsInvisible = new();

    public Dictionary<CCSPlayerController, GlowEntity> GlowEntity = new Dictionary<CCSPlayerController, GlowEntity>();

    public CancellationTokenSource? AssassinTimer;

    public Dictionary<int, bool> ThrowerIsZombie = new();

    public Dictionary<int, (CHandle<CParticleSystem> particle, CancellationTokenSource timer)> ActiveBurns = new();

    public Dictionary<uint, CHandle<COmniLight>> activeLights = new Dictionary<uint, CHandle<COmniLight>>();
    public Dictionary<uint, CancellationTokenSource> lightTimers = new Dictionary<uint, CancellationTokenSource>();

    public readonly Dictionary<SpawnType, List<SpawnPointData>> spawnCache= new();

    public Dictionary<int, float> StopZombieTimers = new();

    public Dictionary<int, bool> ScbaSuit = new Dictionary<int, bool>();
    public Dictionary<int, bool> GodState = new Dictionary<int, bool>();
    public Dictionary<int, bool> InfiniteAmmoState = new Dictionary<int, bool>();

    public Dictionary<int, bool> CanBuyWeaponsThisRound = new Dictionary<int, bool>();

    // ── Extra Items / Ammo Packs ──────────────────────────────────────────────
    /// <summary>
    /// Accumulated damage dealt by each human to zombies this round (keyed by PlayerID).
    /// Reset at round end and on disconnect. Used for damage-based AP reward.
    /// </summary>
    public Dictionary<int, int> DamageAccumulator = new Dictionary<int, int>();

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

    // ── Jetpack ───────────────────────────────────────────────────────────────
    /// <summary>True if the player currently owns a jetpack.</summary>
    public Dictionary<int, bool> HasJetpack = new Dictionary<int, bool>();
    /// <summary>Remaining fuel for the player's jetpack.</summary>
    public Dictionary<int, float> JetpackFuel = new Dictionary<int, float>();
    /// <summary>Server time (CurrentTime) at which fuel was last consumed.</summary>
    public Dictionary<int, float> JetpackLastFuelTime = new Dictionary<int, float>();

    // ── Laser Trip Mines (HLT-style) ──────────────────────────────────────────
    /// <summary>Per-mine think (RepeatBySeconds) cancellation tokens, keyed by entity handle Raw.</summary>
    public Dictionary<uint, CancellationTokenSource> MineThink = new();
    /// <summary>Mine configuration snapshot keyed by entity handle Raw.</summary>
    public Dictionary<uint, MineData> MineData = new();
    /// <summary>Sets of active mine entity handles per player, per mine-type name, keyed by SteamID.</summary>
    public Dictionary<ulong, Dictionary<string, HashSet<uint>>> PlayerMineCounts = new();
    /// <summary>Beam entity handle Raw for each mine, keyed by mine entity handle Raw.</summary>
    public Dictionary<uint, uint> MineBeam = new();

    // ── Revive Token ──────────────────────────────────────────────────────────
    /// <summary>True if the player has an active revive token that will trigger on death.</summary>
    public Dictionary<int, bool> HasReviveToken = new Dictionary<int, bool>();

}
public class ZombieRegenState
{
    public int PlayerID;
    public int RegenAmount;       // 每次回血量
    public float RegenInterval;   // 间隔秒数
    public float NextRegenTime;   // 下一次回血时间戳（秒）
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
/// Mirrors the field set from ZOMineCFG.LaserMine.
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
}

