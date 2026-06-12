namespace MsgPulse.Models;

public sealed class CustomCommand
{
    public List<string> Triggers { get; set; } = [];
    public string Target { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
