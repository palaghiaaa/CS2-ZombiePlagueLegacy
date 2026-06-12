namespace ZPLTeamBets;

/// <summary>
/// Configuration model for the ZPLTeamBets plugin.
/// Loaded from configs/plugins/ZPLTeamBets/ZPLTeamBets.jsonc (key "ZPLTeamBets").
/// Hot-reload supported.
/// </summary>
public class ZPLTeamBetsConfig
{
    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Chat commands (without ! or /) that open the bet menu.
    /// First entry is the primary; remaining are aliases.
    /// </summary>
    public List<string> BetCommands { get; set; } = ["bet", "teambets"];

    // ── Chat ──────────────────────────────────────────────────────────────────

    /// <summary>Prefix prepended to all ZPLTeamBets chat messages.</summary>
    public string ChatPrefix { get; set; } = "[red][ZM][default]";

    // ── Economy ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Wallet kind in the Economy plugin used for bets.
    /// Must match the wallet kind configured in ZombiePlagueLegacyCS2 (default "ammo").
    /// </summary>
    public string WalletKind { get; set; } = "ammo";

    // ── Betting rules ─────────────────────────────────────────────────────────

    /// <summary>Minimum bet amount in Ammo Packs.</summary>
    public int MinBet { get; set; } = 5;

    /// <summary>Maximum bet amount in Ammo Packs (0 = no limit).</summary>
    public int MaxBet { get; set; } = 500;

    /// <summary>
    /// Win multiplier applied to the bet amount when the bettor's team wins.
    /// 2.0 means the player receives back 2× their stake (net gain = stake).
    /// </summary>
    public float WinMultiplier { get; set; } = 2.0f;

    /// <summary>
    /// Seconds of freeze time remaining when bets are locked (no more bets accepted).
    /// 0 = bets lock when EventRoundFreezeEnd fires.
    /// </summary>
    public float BetsLockLeadSeconds { get; set; } = 0f;

    // ── Quick-bet amounts ─────────────────────────────────────────────────────

    /// <summary>
    /// Preset AP amounts shown as quick-bet buttons in the menu.
    /// Players can also type a custom amount via chat command: !bet 250
    /// </summary>
    public List<int> QuickBetAmounts { get; set; } = [10, 25, 50, 100, 250];
}
