namespace MsgPulse.Config;

using Models;

public sealed class EventMessagesConfig
{
    public bool Enabled { get; set; } = true;
    public bool DebugLogs { get; set; } = false;
    public List<EventMessageRule> Rules { get; set; } = [];
}


