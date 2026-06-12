namespace MsgPulse.Models;

public sealed class EventMessageRule
{
    public string Event { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Target { get; set; } = "all";
    public bool? Broadcast { get; set; } = true;
}