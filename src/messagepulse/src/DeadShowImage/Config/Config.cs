namespace MsgPulse.Config;

public sealed class DeadShowImageConfig
{
    public bool Enabled { get; set; } = true;
    public float Interval { get; set; } = 15f;
    public float? Delay { get; set; } = 5f;
    public List<string> Images { get; set; } = [];
}
