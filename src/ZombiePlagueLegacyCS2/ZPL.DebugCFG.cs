namespace ZombiePlagueLegacyCS2;

/// <summary>Debug / diagnostics settings for ZombiePlagueLegacyCS2.</summary>
public class ZPLDebugCFG
{
    /// <summary>When true, command invocations are logged to the server console.</summary>
    public bool EnableCommandDebugLogs { get; set; } = false;

    /// <summary>When true, command invocations produce a chat reply visible to the invoking player.</summary>
    public bool EnableCommandDebugChatReply { get; set; } = false;

    /// <summary>Prefix prepended to chat messages sent by the plugin.</summary>
    public string ChatPrefix { get; set; } = "[red][ZM][default]";
}
