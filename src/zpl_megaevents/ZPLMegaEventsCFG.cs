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
    /// Minimum number of connected real players required before mega events can start.
    /// Set to 0 or 1 to effectively disable the gate.
    /// </summary>
    public int MinimumPlayersToStart { get; set; } = 2;

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
    /// When true, event start and minimum-player skip notices are also shown
    /// in the center HUD.
    /// </summary>
    public bool EnableCenterAnnouncements { get; set; } = true;

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

    // ── New event definitions ──────────────────────────────────────────────────

    /// <summary>
    /// A random online player is selected at the start of the round and
    /// immediately rewarded.  All other players receive a consolation reward.
    /// Inspired by the Fairside GiveAway mechanic.
    /// </summary>
    public GiveAwayConfig GiveAway { get; set; } = new();

    /// <summary>
    /// First human to achieve N headshot kills on zombies wins AP.
    /// Consolation AP is given to every human with at least 1 headshot kill.
    /// </summary>
    public HeadshotKingConfig HeadshotKing { get; set; } = new();

    /// <summary>
    /// First human to knife-kill a zombie wins AP.
    /// Rare high-value event that rewards skill/risk.
    /// </summary>
    public KnifeKillConfig KnifeKill { get; set; } = new();

    /// <summary>
    /// First human to get N kills with any grenade wins AP.
    /// Consolation AP for every human with at least 1 grenade kill.
    /// </summary>
    public GrenadeKingConfig GrenadeKing { get; set; } = new();

    /// <summary>
    /// Player with the most total kills at round end wins AP.
    /// Consolation AP to every player who got at least 1 kill.
    /// Resolved at round end (not first-to-complete).
    /// </summary>
    public MVPRoundConfig MVPRound { get; set; } = new();

    /// <summary>
    /// Zombie who deals the most damage to humans in a round wins AP.
    /// Consolation AP for every zombie who dealt any damage.
    /// Resolved at round end.
    /// </summary>
    public ZombieKingpinConfig ZombieKingpin { get; set; } = new();

    /// <summary>
    /// Flat AP bonus given to every online player at the start of the round.
    /// Functions as a server-wide treat with no winner/loser.
    /// </summary>
    public DoubleDownConfig DoubleDown { get; set; } = new();

    // ── Map-level event ───────────────────────────────────────────────────────

    /// <summary>
    /// When enabled, a map-wide giveaway fires shortly after each map loads.
    /// A random player wins a large AP prize; all others receive consolation AP.
    /// Inspired by the Fairside "Random Map-Credits" mechanic.
    /// Can optionally be restricted to scheduled time windows.
    /// </summary>
    public MapGiveAwayConfig MapGiveAway { get; set; } = new();

    // ── File logging ──────────────────────────────────────────────────────────

    /// <summary>
    /// Controls file-based winner logging (Name + SteamID, one line per event).
    /// Log files are rotated monthly: ZPLMegaEvents_YYYY-MM.log.
    /// Inspired by the Fairside per-map winner logs.
    /// </summary>
    public LoggingConfig Logging { get; set; } = new();

    /// <summary>
    /// Calendar-based event scheduler.  When enabled, rounds that start within
    /// a configured time window force a specific event type instead of using the
    /// random weight pool.  Supports hourly, daily, and weekly schedules.
    /// </summary>
    public ScheduledEventsConfig ScheduledEvents { get; set; } = new();
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
    public int ParticipantRewardAP { get; set; } = 8;
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
    public int ParticipantRewardAP { get; set; } = 8;
}

public class FortressDefenseConfig
{
    public bool Enable { get; set; } = true;
    public int Weight { get; set; } = 15;

    /// <summary>
    /// AP awarded to every human who survives until the round-end timer.
    /// Survival is genuinely difficult in ZPL, so this reward is higher than zombie-side events.
    /// </summary>
    public int RewardAP { get; set; } = 35;
}

public class ZombieArmadaConfig
{
    public bool Enable { get; set; } = true;
    public int Weight { get; set; } = 15;

    /// <summary>AP awarded to every alive zombie when all humans are wiped before the timer.</summary>
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

    /// <summary>
    /// Enables listening to ZPL special-class selection callbacks
    /// (Nemesis/Assassin/Survivor/Sniper) for SpecialClass event tracking.
    /// Disable this to isolate callback-related instability without recompiling.
    /// </summary>
    public bool EnableSelectionHooks { get; set; } = true;

    /// <summary>
    /// AP given to the Nemesis if zombies win the round.
    /// Nemesis is one player holding off all humans — high reward reflects high difficulty.
    /// </summary>
    public int NemesisWinAP { get; set; } = 120;

    /// <summary>AP given to the Assassin if zombies win the round.</summary>
    public int AssassinWinAP { get; set; } = 120;

    /// <summary>AP given to the Survivor if humans win the round.</summary>
    public int SurvivorWinAP { get; set; } = 100;

    /// <summary>AP given to the Sniper if humans win the round.</summary>
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

// ── New event config classes ──────────────────────────────────────────────────

public class GiveAwayConfig
{
    /// <summary>Allow this event to be selected by the random pool.</summary>
    public bool Enable { get; set; } = true;

    /// <summary>Relative selection weight.</summary>
    public int Weight { get; set; } = 10;

    /// <summary>AP awarded to the randomly selected winner.</summary>
    public int WinnerRewardAP { get; set; } = 60;

    /// <summary>
    /// AP awarded to every other online player (consolation).
    /// Kept low — the point is to reward the lucky winner, not the whole server.
    /// </summary>
    public int ConsolationAP { get; set; } = 5;
}

public class HeadshotKingConfig
{
    public bool Enable { get; set; } = true;
    public int Weight { get; set; } = 15;

    /// <summary>Number of headshot kills on zombies a human must achieve to win immediately.</summary>
    public int TargetHeadshots { get; set; } = 3;

    /// <summary>
    /// AP awarded to the first human to reach TargetHeadshots.
    /// Headshots require skill and aim; reward is higher than plain kills.
    /// </summary>
    public int WinnerRewardAP { get; set; } = 70;

    /// <summary>AP awarded to every human with at least 1 headshot kill (consolation).</summary>
    public int ParticipantRewardAP { get; set; } = 8;
}

public class KnifeKillConfig
{
    public bool Enable { get; set; } = true;
    public int Weight { get; set; } = 10;

    /// <summary>
    /// AP awarded to the first human who knife-kills a zombie.
    /// Knife-kills require getting dangerously close to the zombie — the highest single-kill reward.
    /// </summary>
    public int WinnerRewardAP { get; set; } = 150;
}

public class GrenadeKingConfig
{
    public bool Enable { get; set; } = true;
    public int Weight { get; set; } = 10;

    /// <summary>Number of grenade kills a human must achieve to win immediately.</summary>
    public int TargetGrenadeKills { get; set; } = 2;

    /// <summary>
    /// AP awarded to the first human to reach TargetGrenadeKills.
    /// Grenades have limited ammo; reward reflects the resource cost.
    /// </summary>
    public int WinnerRewardAP { get; set; } = 75;

    /// <summary>AP awarded to every human with at least 1 grenade kill (consolation).</summary>
    public int ParticipantRewardAP { get; set; } = 8;
}

public class MVPRoundConfig
{
    public bool Enable { get; set; } = true;
    public int Weight { get; set; } = 15;

    /// <summary>AP awarded to the player with the most kills at round end.</summary>
    public int WinnerRewardAP { get; set; } = 50;

    /// <summary>AP awarded to every other player who got at least 1 kill (consolation).</summary>
    public int ParticipantRewardAP { get; set; } = 8;
}

public class ZombieKingpinConfig
{
    public bool Enable { get; set; } = true;
    public int Weight { get; set; } = 10;

    /// <summary>AP awarded to the zombie who dealt the most damage to humans at round end.</summary>
    public int WinnerRewardAP { get; set; } = 55;

    /// <summary>AP awarded to every other zombie who dealt any damage to humans (consolation).</summary>
    public int ParticipantRewardAP { get; set; } = 8;
}

public class DoubleDownConfig
{
    public bool Enable { get; set; } = true;
    public int Weight { get; set; } = 5;

    /// <summary>
    /// Flat AP given to every online player at round start.
    /// No winner — kept deliberately low since it benefits everyone equally.
    /// </summary>
    public int RewardAP { get; set; } = 15;
}

// ── Map-level event config ────────────────────────────────────────────────────

/// <summary>
/// Configuration for the per-map giveaway event.
/// Fires once shortly after each map loads (optionally restricted to scheduled windows).
/// Inspired by the Fairside "Random Map-Credits / Map-Events for Shop" plugin.
/// </summary>
public class MapGiveAwayConfig
{
    /// <summary>Set to true to enable map-level giveaways.</summary>
    public bool Enable { get; set; } = false;

    /// <summary>
    /// AP awarded to the randomly selected winner.
    /// Map giveaways are rare (once per map), so the reward is significantly higher than per-round events.
    /// </summary>
    public int WinnerRewardAP { get; set; } = 250;

    /// <summary>AP awarded to every other online player (consolation).</summary>
    public int ConsolationAP { get; set; } = 15;

    /// <summary>
    /// Seconds after map load before the giveaway fires.
    /// A small delay lets all players connect before the winner is picked.
    /// </summary>
    public float DelaySeconds { get; set; } = 60f;

    /// <summary>
    /// When true, the map giveaway only fires if the current time falls
    /// inside one of the <see cref="ScheduledEventsConfig.Events"/> windows.
    /// Set to false to run on every map regardless of schedule.
    /// </summary>
    public bool OnlyDuringScheduledWindow { get; set; } = false;
}

// ── Logging config ────────────────────────────────────────────────────────────

/// <summary>
/// Configuration for file-based winner logging (Name + SteamID).
/// Inspired by the Fairside monthly winner log for Mega Events.
/// </summary>
public class LoggingConfig
{
    /// <summary>Set to true to enable file logging of event winners.</summary>
    public bool Enable { get; set; } = true;

    /// <summary>
    /// Directory in which log files are created.
    /// Relative paths are resolved from the SwiftlyS2 base directory.
    /// Log files are named <c>ZPLMegaEvents_YYYY-MM.log</c> (monthly rotation).
    /// </summary>
    public string LogDirectory { get; set; } = "logs/ZPLMegaEvents";
}

// ── Scheduled events ──────────────────────────────────────────────────────────

/// <summary>
/// A single time-window entry that forces a specific <see cref="MegaEventType"/>
/// to activate during matching rounds instead of using the random pool.
/// Times use the timezone configured in <see cref="ScheduledEventsConfig.TimezoneOffsetHours"/>.
/// </summary>
public class ScheduledEventEntry
{
    /// <summary>The event type to force during this window.</summary>
    public MegaEventType EventType { get; set; } = MegaEventType.HappyHour;

    /// <summary>
    /// Days of the week when this window is active (0 = Sunday, 1 = Monday, …, 6 = Saturday).
    /// Leave empty to apply every day.
    /// </summary>
    public List<int> DaysOfWeek { get; set; } = new();

    /// <summary>Start hour of the active window in 24-hour format (0–23).</summary>
    public int HourStart { get; set; } = 18;

    /// <summary>
    /// End hour of the active window in 24-hour format (0–23, exclusive).
    /// Set higher than HourStart for a same-day window (e.g. 18→20).
    /// Set lower to span midnight (e.g. 22→2 = 22:00 to 02:00).
    /// </summary>
    public int HourEnd { get; set; } = 20;

    /// <summary>
    /// Repeat every N calendar weeks.  1 = every week, 2 = every other week, etc.
    /// 0 or 1 = every matching week (no skipping).
    /// Uses ISO 8601 week numbering: week 1 is the week containing the year's first
    /// Thursday, so week 1 may begin in late December of the previous year.
    /// </summary>
    public int WeekInterval { get; set; } = 1;
}

/// <summary>
/// Configuration for calendar-based scheduled events.
/// When enabled, rounds whose start time falls within a defined window will
/// force the specified event type rather than drawing from the random pool.
/// </summary>
public class ScheduledEventsConfig
{
    /// <summary>Set to true to enable calendar-based event scheduling.</summary>
    public bool Enable { get; set; } = false;

    /// <summary>
    /// Timezone offset in whole hours from UTC used when evaluating schedules
    /// (e.g. 3 for UTC+3, -5 for UTC-5).  Fractional offsets are not supported.
    /// </summary>
    public int TimezoneOffsetHours { get; set; } = 0;

    /// <summary>List of scheduled time windows.  First match wins.</summary>
    public List<ScheduledEventEntry> Events { get; set; } = new();
}
