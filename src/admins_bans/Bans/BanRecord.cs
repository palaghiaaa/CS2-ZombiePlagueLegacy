namespace Admins.Bans.Manager;

/// <summary>
/// Immutable snapshot of a single ban row returned by <see cref="ServerBans.FindActiveBanAsync"/>.
/// </summary>
public sealed record BanRecord
{
    public long     Id             { get; init; }
    public long     SteamId64      { get; init; }
    public string   Ip             { get; init; } = string.Empty;
    public string   Reason         { get; init; } = string.Empty;
    public string   AdminName      { get; init; } = string.Empty;
    public long     AdminSteamId64 { get; init; }
    public DateTime CreatedAt      { get; init; }
    public DateTime? ExpiresAt     { get; init; }
}
