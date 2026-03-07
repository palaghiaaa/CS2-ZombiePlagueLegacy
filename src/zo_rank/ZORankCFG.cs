namespace ZORank;

/// <summary>
/// Configuration for the ZO Rank &amp; Top plugin.
/// Lives in configs/plugins/ZORank/ZORankCFG.jsonc (key "ZORankCFG").
/// All settings support hot-reload.
/// </summary>
public class ZORankCFG
{
    // ── Chat ─────────────────────────────────────────────────────────────────

    /// <summary>Tag prepended to every chat message sent by this plugin.</summary>
    public string ChatTag { get; set; } = "[ZO Rank]";

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Console / chat command that shows the player's current rank.</summary>
    public string RankCommand { get; set; } = "rank";

    /// <summary>Console / chat command that opens the default top-players menu.</summary>
    public string TopCommand { get; set; } = "top";

    /// <summary>Alias that always opens the top-15 menu.</summary>
    public string Top15Command { get; set; } = "top15";

    /// <summary>Alias that always opens the top-10 menu.</summary>
    public string Top10Command { get; set; } = "top10";

    // ── Sorting ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Primary stat used to rank players.
    /// "kills"      – most human kills first (PvP / last-man-standing focus).
    /// "infections" – most zombie infections first (classic Zombie Plague focus).
    /// "damage"     – most total damage dealt first.
    /// Tiebreakers are always applied in the order: kills → infections → assists →
    /// damage → deaths ascending.
    /// </summary>
    public string SortMode { get; set; } = "kills";

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
