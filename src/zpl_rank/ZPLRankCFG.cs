namespace ZPLRank;

/// <summary>
/// Configuration for the ZPL Rank &amp; Top plugin.
/// Lives in configs/plugins/ZPLRank/ZPLRankCFG.jsonc (key "ZPLRankCFG").
/// All settings support hot-reload.
/// </summary>
public class ZPLRankCFG
{
    // ── Database ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Path (relative to the server's CS:GO root, or absolute) for the SQLite
    /// database file that stores rank statistics across server restarts.
    /// The directory is created automatically on first run.
    /// </summary>
    public string DatabasePath { get; set; } = "addons/swiftly/data/ZPLRank.db";

    // ── Chat ─────────────────────────────────────────────────────────────────

    /// <summary>Tag prepended to every chat message sent by this plugin.</summary>
    public string ChatTag { get; set; } = "[ZPL Rank]";

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Console / chat command that shows the player's current rank.</summary>
    public string RankCommand { get; set; } = "rank";

    /// <summary>Console / chat command that opens the default top-players menu.</summary>
    public string TopCommand { get; set; } = "top";

    /// <summary>Alias that always opens the top-15 menu.</summary>
    public string Top15Command { get; set; } = "top15";

    /// <summary>Alias that always opens the top-10 menu.</summary>
    public string Top10Command { get; set; } = "top10";

    // ── Score formula ─────────────────────────────────────────────────────────
    //
    // Score = (Kills × KillWeight + Infections × InfectionWeight
    //          + Assists × AssistWeight + Damage / DamageDivisor)
    //         / max(Deaths, 1)
    //
    // This is used as the single sort key for !rank position and all !top menus.

    /// <summary>Points awarded per human kill.</summary>
    public double KillWeight { get; set; } = 2.0;

    /// <summary>Points awarded per zombie infection.</summary>
    public double InfectionWeight { get; set; } = 2.0;

    /// <summary>Points awarded per kill assist.</summary>
    public double AssistWeight { get; set; } = 1.0;

    /// <summary>
    /// Every this many points of damage dealt add 1 score point.
    /// Set to 0 to exclude damage from the score.
    /// </summary>
    public double DamageDivisor { get; set; } = 100.0;

    // ── Top menu ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Number of players shown by the default <see cref="TopCommand"/>.
    /// The top-10 and top-15 aliases always use 10 and 15 respectively.
    /// </summary>
    public int TopListSize { get; set; } = 15;

    /// <summary>Number of rows visible at once in the top-players menu.</summary>
    public int TopMenuVisibleRows { get; set; } = 5;

    // ── Feature toggles ───────────────────────────────────────────────────────

    /// <summary>Set to false to disable the rank command entirely.</summary>
    public bool EnableRankCommand { get; set; } = true;

    /// <summary>Set to false to disable all top commands entirely.</summary>
    public bool EnableTopCommands { get; set; } = true;
}
