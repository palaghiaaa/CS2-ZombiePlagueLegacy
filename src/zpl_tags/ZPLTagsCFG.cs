namespace ZPLTags;

/// <summary>
/// Configuration for the ZPL Tags bridge plugin.
/// Lives in configs/plugins/ZPLTags/ZPLTagsCFG.jsonc (key "ZPLTags").
/// Hot-reload supported: changes apply without restarting the server.
/// </summary>
public class ZPLTagsCFG
{
    // ── Menu ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Chat/console command that opens the tag-selection menu.
    /// Registered once on Load; change requires plugin reload.
    /// </summary>
    public string MenuCommand { get; set; } = "sw_tags";

    /// <summary>Title shown at the top of the tag-selection menu.</summary>
    public string MenuTitle { get; set; } = "Select a Tag";

    /// <summary>Label for the "remove current tag" entry at the bottom of the menu.</summary>
    public string NoTagLabel { get; set; } = "✖ Remove Tag";

    /// <summary>
    /// Message sent to a player who runs the command but has no eligible tags.
    /// </summary>
    public string NoTagsAvailableMessage { get; set; } = "{red}[ZPLTags]{default} You have no tags available.";

    // ── Tag entries ───────────────────────────────────────────────────────────

    /// <summary>
    /// List of tag entries.  A player is eligible for an entry when they match
    /// at least ONE of the two optional conditions:
    ///   • <see cref="GroupTagEntry.GroupName"/>  → Admins-plugin group name
    ///   • <see cref="GroupTagEntry.Permission"/> → SwiftlyS2 permission flag
    ///
    /// This means VIPs, admins, or any player with a configured permission can
    /// all see and select tags.  When both fields are set, either one matching
    /// is enough.
    ///
    /// When a player belongs to multiple eligible entries they are ALL shown in
    /// the menu; the one with the highest <see cref="GroupTagEntry.Priority"/>
    /// is auto-selected on first connect.
    /// </summary>
    public List<GroupTagEntry> GroupTags { get; set; } =
    [
        new GroupTagEntry
        {
            GroupName   = "Owner",
            Permission  = "@admins/owner",
            DisplayName = "Tag De Owner",
            Priority    = 100,
            ScoreTag    = "[OWNER]",
            ChatTag     = "[red][OWNER][default] ",
            ChatColor   = "[green]",
            NameColor   = "[teamcolor]",
            ChatSound   = true
        },
        new GroupTagEntry
        {
            GroupName   = "Admin",
            Permission  = "@admins/admin",
            DisplayName = "Tag De Admin",
            Priority    = 50,
            ScoreTag    = "[ADMIN]",
            ChatTag     = "[blue][ADMIN][default] ",
            ChatColor   = "[white]",
            NameColor   = "[teamcolor]",
            ChatSound   = true
        },
        new GroupTagEntry
        {
            GroupName   = "",
            Permission  = "@zplvip/vip",
            DisplayName = "Tag De VIP",
            Priority    = 30,
            ScoreTag    = "[VIP]",
            ChatTag     = "[gold][VIP][default] ",
            ChatColor   = "[white]",
            NameColor   = "[teamcolor]",
            ChatSound   = true
        }
    ];
}

/// <summary>
/// Maps one eligibility condition (admin group OR permission flag) to a set of
/// tag properties for chat and scoreboard.
/// </summary>
public class GroupTagEntry
{
    /// <summary>
    /// Admins-plugin group name (case-insensitive).
    /// Leave empty if you only want to match by <see cref="Permission"/>.
    /// </summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>
    /// SwiftlyS2 permission flag (e.g. "@zplvip/vip", "@admins/admin").
    /// Leave empty if you only want to match by <see cref="GroupName"/>.
    /// </summary>
    public string Permission { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable label shown in the tag-selection menu.
    /// Falls back to <see cref="GroupName"/> (or <see cref="Permission"/>)
    /// when left empty.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Priority used when a player matches multiple entries.
    /// Higher value = shown first and auto-selected on connect.
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>Scoreboard clan-tag slot text. Empty = no change.</summary>
    public string? ScoreTag { get; set; }

    /// <summary>
    /// Tag prepended to the player name in chat.
    /// Supports cs2-tags colour codes: [red], [blue], [teamcolor], [default], …
    /// </summary>
    public string? ChatTag { get; set; }

    /// <summary>Colour applied to the chat message body.</summary>
    public string? ChatColor { get; set; }

    /// <summary>Colour applied to the player name in chat.</summary>
    public string? NameColor { get; set; }

    /// <summary>Whether the chat ping sound plays for this tag.</summary>
    public bool ChatSound { get; set; } = true;

    /// <summary>Menu button label: DisplayName → GroupName → Permission.</summary>
    public string GetMenuLabel()
    {
        if (!string.IsNullOrWhiteSpace(DisplayName)) return DisplayName;
        if (!string.IsNullOrWhiteSpace(GroupName))   return GroupName;
        return Permission;
    }
}
