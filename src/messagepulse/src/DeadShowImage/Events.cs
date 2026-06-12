using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;

namespace MsgPulse.Services;

using SwiftlyS2.Shared.Players;

public partial class DeadShowImage
{
    private readonly HashSet<IPlayer> deadPlayers = [];

    [GameEventHandler(HookMode.Post)]
    private HookResult OnPlayerDeath(EventPlayerDeath @event)
    {
        var player = @event.UserIdPlayer;
        if (player is null || !player.IsValid)
            return HookResult.Continue;

        deadPlayers.Add(player);
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Pre)]
    private HookResult OnPlayerSpawn(EventPlayerSpawn @event)
    {
        deadPlayers.Remove(@event.UserIdPlayer!);

        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Pre)]
    private HookResult OnRoundPrestart(EventRoundPrestart @event)
    {
        if (core.EntitySystem.GetGameRules()?.TotalRoundsPlayed < 1)
            return HookResult.Continue;

        foreach (var player in deadPlayers)
        {
            if (player is null)
                continue;

            if (player.IsFakeClient is true || player.IsValid is false)
            {
                deadPlayers.Remove(player);
                continue;
            }

            if (player.Pawn?.Team is Team.Spectator or Team.None)
                continue;

            player.SendCenterHTML("", 1);
            deadPlayers.Remove(player);
        }

        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Pre)]
    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event)
    {
        var player = @event.UserIdPlayer;
        if (player is null || !player.IsValid)
            return HookResult.Continue;

        deadPlayers.Remove(player);
        return HookResult.Continue;
    }
}
