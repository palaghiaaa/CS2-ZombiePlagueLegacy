namespace ZPLTags;

/// <summary>
/// Configuration for the ZPL Tags bridge plugin.
/// Lives in configs/plugins/ZPLTags/ZPLTagsCFG.jsonc (key "ZPLTags").
/// Hot-reload supported: changes apply without restarting the server.
/// </summary>
public class ZPLTagsCFG
{
    /// <summary>
    /// List of admin group → tag mappings.
    /// When a player belongs to multiple groups the entry with the highest
    /// <see cref="GroupTagEntry.Priority"/> wins.
    /// Group names are matched case-insensitively against the Admins plugin's
    /// group names.
    /// </summary>
    public List<GroupTagEntry> GroupTags { get; set; } =
    [
        new GroupTagEntry
        {
            GroupName = "Owner",
            Priority  = 100,
            ScoreTag  = "[OWNER]",
            ChatTag   = "[red][OWNER][default] ",
            ChatColor = "[green]",
            NameColor = "[teamcolor]",
            ChatSound = true
        },
        new GroupTagEntry
        {
            GroupName = "Admin",
            Priority  = 50,
            ScoreTag  = "[ADMIN]",
            ChatTag   = "[blue][ADMIN][default] ",
            ChatColor = "[white]",
            NameColor = "[teamcolor]",
            ChatSound = true
        }
    ];
}

/// <summary>
/// Maps one Admins-plugin group to a set of tag properties for chat and scoreboard.
/// </summary>
public class GroupTagEntry
{
    /// <summary>
    /// Name of the admin group in the Admins plugin (case-insensitive).
    /// Must match the group name exactly as configured in the Admins plugin.
    /// </summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>
    /// Priority used when a player belongs to multiple groups that each have a
    /// configured tag.  Higher value wins.  Default is 0.
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Tag shown in the scoreboard clan-tag slot.
    /// Leave <c>null</c> or empty to not change the score tag.
    /// </summary>
    public string? ScoreTag { get; set; }

    /// <summary>
    /// Tag prepended to the player name in chat.
    /// Supports cs2-tags colour codes such as [red], [blue], [teamcolor], etc.
    /// Leave <c>null</c> or empty to fall back to the Tags plugin default.
    /// </summary>
    public string? ChatTag { get; set; }

    /// <summary>
    /// Colour applied to the message body in chat.
    /// Supports cs2-tags colour codes.
    /// Leave <c>null</c> or empty to fall back to the Tags plugin default.
    /// </summary>
    public string? ChatColor { get; set; }

    /// <summary>
    /// Colour applied to the player name in chat.
    /// Supports cs2-tags colour codes.
    /// Leave <c>null</c> or empty to fall back to the Tags plugin default.
    /// </summary>
    public string? NameColor { get; set; }

    /// <summary>
    /// Whether the chat ping sound plays when this admin sends a message.
    /// </summary>
    public bool ChatSound { get; set; } = true;
}
