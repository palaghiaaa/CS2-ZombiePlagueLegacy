using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Memory;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace ZPLDarkFog;

public sealed class ZPLDarkFog_Service
{
    private const string PostProcessDesignerName = "post_processing_volume";

    private delegate void SnapViewAnglesDelegate(nint pawn, nint angle);

    private readonly ISwiftlyCore _core;
    private readonly ILogger<ZPLDarkFog_Service> _logger;
    private readonly Dictionary<int, uint> _volumeEntityIndexByPlayerId = [];
    private readonly IUnmanagedFunction<SnapViewAnglesDelegate>? _snapViewAngles;

    public ZPLDarkFog_Service(ISwiftlyCore core, ILogger<ZPLDarkFog_Service> logger)
    {
        _core = core;
        _logger = logger;

        if (!_core.GameData.TryGetSignature("SnapViewAngles", out var snapViewAnglesAddress) ||
            snapViewAnglesAddress == nint.Zero)
        {
            _logger.LogWarning("SnapViewAngles signature was not found. Refresh will skip angle snapping.");
            return;
        }

        _snapViewAngles =
            _core.Memory.GetUnmanagedFunctionByAddress<SnapViewAnglesDelegate>(snapViewAnglesAddress);

        _logger.LogInformation("SnapViewAngles signature loaded at 0x{Address:X}.", snapViewAnglesAddress);
    }

    public bool ApplyExposure(IPlayer? player, float exposure)
    {
        if (!TryGetExposureTarget(player, out var validPlayer))
        {
            return false;
        }

        exposure = MathF.Max(0.0f, exposure);

        _logger.LogInformation(
            "Applying exposure {Exposure} to player {PlayerId}.",
            exposure,
            validPlayer.PlayerID);

        RemovePlayerVolume(validPlayer.PlayerID);

        var volume = CreatePlayerVolume(validPlayer, exposure);
        if (volume is null || !volume.IsValid)
        {
            _logger.LogWarning(
                "Exposure apply aborted because the post-processing volume was not created for player {PlayerId}.",
                validPlayer.PlayerID);
            return false;
        }

        _volumeEntityIndexByPlayerId[validPlayer.PlayerID] = volume.Index;

        _logger.LogInformation(
            "Player {PlayerId} now owns post-processing volume {EntityIndex} with exposure {Exposure}.",
            validPlayer.PlayerID,
            volume.Index,
            exposure);

        RefreshPlayerVisuals(validPlayer);
        return true;
    }

    public void ResetPlayer(IPlayer? player)
    {
        if (player is null)
        {
            return;
        }

        _logger.LogInformation(
            "Resetting custom exposure for player {PlayerId}.",
            player.PlayerID);

        RemovePlayerVolume(player.PlayerID);

        if (player.IsValid && !player.IsFakeClient)
        {
            RefreshPlayerVisuals(player);
        }
    }

    public void RemovePlayer(int playerId)
    {
        _logger.LogInformation(
            "Removing dark fog state for player {PlayerId}.",
            playerId);
        RemovePlayerVolume(playerId);
    }

    public void ClearAllVolumes()
    {
        _logger.LogInformation(
            "Clearing all active post-processing volumes. Count={VolumeCount}.",
            _volumeEntityIndexByPlayerId.Count);

        foreach (var playerId in _volumeEntityIndexByPlayerId.Keys.ToArray())
        {
            RemovePlayerVolume(playerId);
        }
    }

    public void ClearAll()
    {
        _logger.LogInformation("Clearing all active dark fog state.");
        ClearAllVolumes();
    }

    private CPostProcessingVolume? CreatePlayerVolume(IPlayer player, float exposure)
    {
        CPostProcessingVolume? volume;

        try
        {
            volume = _core.EntitySystem.CreateEntityByDesignerName<CPostProcessingVolume>(PostProcessDesignerName);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(
                ex,
                "Failed to create post-processing volume for player {PlayerId} because designer '{DesignerName}' is invalid on this server.",
                player.PlayerID,
                PostProcessDesignerName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while creating post-processing volume for player {PlayerId}. Designer='{DesignerName}'.",
                player.PlayerID,
                PostProcessDesignerName);
            return null;
        }

        if (volume is null || !volume.IsValid)
        {
            _logger.LogWarning(
                "EntitySystem returned an invalid post-processing volume for player {PlayerId}. Designer='{DesignerName}'.",
                player.PlayerID,
                PostProcessDesignerName);
            return null;
        }

        volume.Master = true;
        volume.FadeDuration = 0.0f;
        volume.ExposureControl = true;
        volume.MinExposure = exposure;
        volume.MaxExposure = exposure;

        volume.DispatchSpawn();

        _logger.LogInformation(
            "Created post-processing volume {EntityIndex} for player {PlayerId}. Designer='{DesignerName}' Exposure={Exposure}.",
            volume.Index,
            player.PlayerID,
            PostProcessDesignerName,
            exposure);

        volume.SetTransmitState(false);
        _logger.LogInformation(
            "Configured volume {EntityIndex} to be hidden from all clients before owner assignment.",
            volume.Index);

        volume.SetTransmitState(true, player.PlayerID);
        _logger.LogInformation(
            "Configured volume {EntityIndex} to transmit only to player {PlayerId}.",
            volume.Index,
            player.PlayerID);

        return volume;
    }

    private void RemovePlayerVolume(int playerId)
    {
        if (!_volumeEntityIndexByPlayerId.Remove(playerId, out var entityIndex))
        {
            _logger.LogDebug(
                "No active post-processing volume was tracked for player {PlayerId}.",
                playerId);
            return;
        }

        try
        {
            var volume = _core.EntitySystem.GetEntityByIndex<CPostProcessingVolume>(entityIndex);
            if (volume is not null && volume.IsValid)
            {
                _logger.LogInformation(
                    "Despawning post-processing volume {EntityIndex} for player {PlayerId}.",
                    entityIndex,
                    playerId);
                volume.Despawn();
                return;
            }

            _logger.LogDebug(
                "Tracked post-processing volume {EntityIndex} for player {PlayerId} was already invalid during removal.",
                entityIndex,
                playerId);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Failed to despawn post-processing volume {EntityIndex} for player {PlayerId}.",
                entityIndex,
                playerId);
        }
    }

    private void RefreshPlayerVisuals(IPlayer player)
    {
        if (!TryGetExposureTarget(player, out var validPlayer))
        {
            return;
        }

        var playerId = validPlayer.PlayerID;
        _logger.LogInformation(
            "Scheduling visual refresh for player {PlayerId} on next world update.",
            playerId);

        _core.Scheduler.NextWorldUpdate(() =>
        {
            var scheduledPlayer = _core.PlayerManager.GetPlayer(playerId);
            if (!TryGetExposureTarget(scheduledPlayer, out var refreshedPlayer))
            {
                return;
            }

            _logger.LogInformation(
                "Refreshing visuals for player {PlayerId} on next world update.",
                refreshedPlayer.PlayerID);

            TrySnapViewRefresh(refreshedPlayer);

            if (TryForceFullUpdate(refreshedPlayer))
            {
                return;
            }

            _logger.LogWarning(
                "ForceFullUpdate was unavailable for player {PlayerId}; skipped unsafe Teleport fallback to avoid model tilt.",
                refreshedPlayer.PlayerID);
        });
    }

    private void TrySnapViewRefresh(IPlayer player)
    {
        if (_snapViewAngles is null)
        {
            return;
        }

        try
        {
            var pawn = player.PlayerPawn;
            if (pawn is null || !pawn.IsValid)
            {
                return;
            }

            var eyeAngles = pawn.EyeAngles;
            var angleMemory = Marshal.AllocHGlobal(Marshal.SizeOf<QAngle>());

            try
            {
                Marshal.StructureToPtr(eyeAngles, angleMemory, false);
                _snapViewAngles.Call(pawn.Address, angleMemory);
            }
            finally
            {
                Marshal.FreeHGlobal(angleMemory);
            }

            _logger.LogInformation("Applied SnapViewAngles refresh for player {PlayerId}.", player.PlayerID);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "SnapViewAngles refresh failed for player {PlayerId}.",
                player.PlayerID);
        }
    }

    private bool TryForceFullUpdate(IPlayer player)
    {
        try
        {
            var serverSideClient = player.ServerSideClient;
            if (serverSideClient is not null)
            {
                serverSideClient.ForceFullUpdate();
                _logger.LogInformation(
                    "ForceFullUpdate succeeded on ServerSideClient for player {PlayerId}.",
                    player.PlayerID);
                return true;
            }

            if (TryInvokeForceFullUpdate(player))
            {
                _logger.LogInformation(
                    "ForceFullUpdate succeeded directly on IPlayer for player {PlayerId}.",
                    player.PlayerID);
                return true;
            }

            if (TryInvokeForceFullUpdate(player.Controller))
            {
                _logger.LogInformation(
                    "ForceFullUpdate succeeded on Controller for player {PlayerId}.",
                    player.PlayerID);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "ForceFullUpdate fallback path failed for player {PlayerId}.",
                player.PlayerID);
        }

        _logger.LogDebug(
            "ForceFullUpdate could not be resolved for player {PlayerId}.",
            player.PlayerID);
        return false;
    }

    private static bool TryInvokeForceFullUpdate(object? candidate)
    {
        if (candidate is null)
            return false;

        var method = candidate.GetType().GetMethod(
            "ForceFullUpdate",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        if (method is null)
            return false;

        method.Invoke(candidate, null);
        return true;
    }

    private static bool TryGetExposureTarget(IPlayer? player, out IPlayer validPlayer)
    {
        validPlayer = null!;

        if (player is null || !player.IsValid || player.IsFakeClient)
        {
            return false;
        }

        validPlayer = player;
        return true;
    }
}
