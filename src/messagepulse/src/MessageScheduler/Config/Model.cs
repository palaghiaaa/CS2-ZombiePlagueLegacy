
namespace MsgPulse.Models;

public sealed class MsgModel
{
    public string Message { get; set; } = string.Empty;
    public float? Interval { get; set; }
    public float? Delay { get; set; }
    public bool? Broadcast { get; set; } = true;
}
