namespace ZPLMegaEvents;

/// <summary>
/// Root configuration for the ZPL Mega Events auto-scheduler plugin.
/// Lives in configs/plugins/ZPLMegaEvents/ZPLMegaEventsCFG.jsonc.
/// All settings support hot-reload.
/// </summary>
public class ZPLMegaEventsCFG
{
    // ── General ───────────────────────────────────────────────────────────────

    /// <summary>Prefix prepended to every chat message sent by this plugin.</summary>
    public string ChatPrefix { get; set; } = "[gold][MegaEvents][default]";

    /// <summary>
    /// Named connection key from SwiftlyS2's configs/database.jsonc.
    /// Used for persisting per-player mega-event stats (completed events and total AP earned).
    /// Set to "" to disable MySQL persistence.
    /// </summary>
    public string DatabaseConnection { get; set; } = "host";

    /// <summary>Economy wallet kind used for ammo-pack rewards (must match Economy plugin config).</summary>
    public string WalletKind { get; set; } = "ammo";

    /// <summary>When true, mega-event announcements are shown in chat.</summary>
    public bool EnableAnnouncements { get; set; } = true;

    /// <summary>
    /// When true, a progress reminder is broadcast to all players mid-round
    /// (at <see cref="ProgressReminderSeconds"/> seconds after event start).
    /// </summary>
    public bool EnableProgressReminder { get; set; } = true;

    /// <summary>Seconds after round freeze-end before the progress reminder is sent.</summary>
    public float ProgressReminderSeconds { get; set; } = 60f;

    // ── Event definitions ─────────────────────────────────────────────────────

    /// <summary>
    /// First zombie to infect this many humans in one round wins AP.
    /// Also awards a smaller consolation AP to every zombie who infects at least one human.
    /// </summary>
    public InfectionRushConfig InfectionRush { get; set; } = new();

    /// <summary>
    /// First human to kill this many zombies in one round wins AP.
    /// </summary>
    public KillFrenzyConfig KillFrenzy { get; set; } = new();

    /// <summary>
    /// All humans who survive until the round-end timer wins AP (no early zombie wipe).
    /// </summary>
    public FortressDefenseConfig FortressDefense { get; set; } = new();

    /// <summary>
    /// If zombies wipe out every human before the timer, every alive zombie wins AP.
    /// </summary>
    public ZombieArmadaConfig ZombieArmada { get; set; } = new();

    /// <summary>
    /// First player to deal this many total damage points in one round wins AP.
    /// </summary>
    public DamageMarathonConfig DamageMarathon { get; set; } = new();

    /// <summary>
    /// Bonus AP rewarded to special-class players (Nemesis, Assassin, Survivor, Sniper)
    /// when they achieve their mode's win condition.
    /// </summary>
    public SpecialClassBonusConfig SpecialClassBonus { get; set; } = new();

    /// <summary>
    /// Double-AP multiplier window: during happy-hour rounds every AP reward
    /// from any active mega event is multiplied.
    /// Rounds are counted since plugin load; set RoundInterval to 0 to disable.
    /// </summary>
    public HappyHourConfig HappyHour { get; set; } = new();
}

// ── Per-event config classes ──────────────────────────────────────────────────

public class InfectionRushConfig
{
    /// <summary>Allow this event to be selected.</summary>
    public bool Enable { get; set; } = true;

    /// <summary>Relative selection weight (higher = more likely to be chosen).</summary>
    public int Weight { get; set; } = 20;

    /// <summary>Number of infections the winner must achieve.</summary>
    public int TargetInfections { get; set; } = 3;

    /// <summary>AP awarded to the first zombie who reaches TargetInfections.</summary>
    public int WinnerRewardAP { get; set; } = 50;

    /// <summary>AP awarded to every zombie who infects at least 1 human (consolation).</summary>
    public int ParticipantRewardAP { get; set; } = 5;
}

public class KillFrenzyConfig
{
    public bool Enable { get; set; } = true;
    public int Weight { get; set; } = 20;

    /// <summary>Number of zombie kills the winner must achieve.</summary>
    public int TargetKills { get; set; } = 5;

    /// <summary>AP awarded to the first human who reaches TargetKills.</summary>
    public int WinnerRewardAP { get; set; } = 50;

    /// <summary>AP awarded to every human who kills at least 1 zombie (consolation).</summary>
    public int ParticipantRewardAP { get; set; } = 5;
}

public class FortressDefenseConfig
{
    public bool Enable { get; set; } = true;
    public int Weight { get; set; } = 15;

    /// <summary>AP awarded to every human who survives until round-end timer.</summary>
    public int RewardAP { get; set; } = 30;
}

public class ZombieArmadaConfig
{
    public bool Enable { get; set; } = true;
    public int Weight { get; set; } = 15;

    /// <summary>AP awarded to every alive zombie when all humans are wiped before timer.</summary>
    public int RewardAP { get; set; } = 30;
}

public class DamageMarathonConfig
{
    public bool Enable { get; set; } = true;
    public int Weight { get; set; } = 15;

    /// <summary>Total damage (HP) a player must deal to win.</summary>
    public int TargetDamage { get; set; } = 5000;

    /// <summary>AP awarded to the first player who reaches TargetDamage.</summary>
    public int WinnerRewardAP { get; set; } = 60;
}

public class SpecialClassBonusConfig
{
    public bool Enable { get; set; } = true;
    public int Weight { get; set; } = 15;

    /// <summary>AP given to the Nemesis if they survive the round (humans don't wipe nemesis).</summary>
    public int NemesisWinAP { get; set; } = 100;

    /// <summary>AP given to the Assassin if they survive the round.</summary>
    public int AssassinWinAP { get; set; } = 100;

    /// <summary>AP given to the Survivor if they survive the round.</summary>
    public int SurvivorWinAP { get; set; } = 100;

    /// <summary>AP given to the Sniper if they survive the round.</summary>
    public int SniperWinAP { get; set; } = 100;
}

public class HappyHourConfig
{
    /// <summary>
    /// Every N rounds a "happy hour" mega round is triggered where all event AP
    /// rewards are multiplied by Multiplier.  Set to 0 to disable.
    /// </summary>
    public int RoundInterval { get; set; } = 10;

    /// <summary>Multiplier applied to all AP rewards during a happy-hour round.</summary>
    public float Multiplier { get; set; } = 2.0f;
}
