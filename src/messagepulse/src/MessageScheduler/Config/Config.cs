namespace MsgPulse.Config;

using Models;

public sealed class ChatBroadcast
{
    public float DefaultIntervalSeconds { get; set; } = 60f;

    public List<MsgModel> Messages { get; set; } = [];
}
