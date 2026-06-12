using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.EntitySystem;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using ZombiePlagueLegacyCS2;

namespace ZPLFlashlight;

public sealed class ZPLFlashlight_Service
{
    private const string FlashlightDesignerName = "light_barn";
    private const string FlashlightCookiePath = "materials/effects/lightcookies/flashlight.vtex";
    private const int DirectLightMode = 3;
    private const float BarnSoftX = 1.0f;
    private const float BarnSoftY = 1.0f;
    private const float BarnSkirt = 0.5f;
    private const float BarnSkirtNear = 1.0f;
    private const float BarnSizeExponent = 0.02f;

    private readonly ISwiftlyCore _core;
    private readonly ILogger<ZPLFlashlight_Service> _logger;
    private readonly Dictionary<ulong, FlashlightPlayerState> _statesBySessionId = [];
    private readonly Dictionary<int, ulong> _sessionIdByPlayerId = [];
    private readonly List<ulong> _staleSessions = [];

    private ZPLFlashlight_Config _config = new();
    private IZombiePlagueLegacyAPI? _zpApi;

    public ZPLFlashlight_Service(ISwiftlyCore core, ILogger<ZPLFlashlight_Service> logger)
    {
        _core = core;
        _logger = logger;
    }

    public void SetZombiePlagueApi(IZombiePlagueLegacyAPI? zpApi)
    {
        _zpApi = zpApi;
    }

    public void UpdateConfig(ZPLFlashlight_Config config)
    {
        _config = config.Clone();

        if (!_config.Enable)
        {
            foreach (var state in _statesBySessionId.Values)
            {
                ForceDisableState(state);
            }
        }
    }

    public bool TryToggleForPlayer(IPlayer? player, out bool enabled, out string message)
    {
        enabled = false;
        message = "Flashlight.ErrorServiceUnavailable";

        if (!_config.Enable)
        {
            message = "Flashlight.ErrorDisabledInConfig";
            return false;
        }

        if (!TryGetConnectedPlayer(player, out var connectedPlayer))
        {
            message = "Flashlight.ErrorPlayerUnavailableForControl";
            return false;
        }

        if (!IsUserFlashlightEnabled(connectedPlayer))
        {
            message = "Flashlight.ErrorUserDisabled";
            return false;
        }

        var state = GetOrCreateState(connectedPlayer);
        var currentTime = _core.Engine.GlobalVars.CurrentTime;
        var debounceWindow = Math.Max(0.01f, _config.ToggleDebounceMs / 1000.0f);

        if (state.LastToggleTime > 0.0f && currentTime - state.LastToggleTime < debounceWindow)
        {
            enabled = state.Enabled;
            message = state.Enabled ? "Flashlight.StateAlreadyEnabled" : "Flashlight.StateAlreadyDisabled";
            return false;
        }

        state.LastToggleTime = currentTime;

        if (state.Enabled)
        {
            state.Enabled = false;
            DestroyLightEntity(state);
            enabled = false;
            message = "Flashlight.StateDisabled";
            return true;
        }

        var resolvedProfile = ResolveEffectiveProfile(connectedPlayer);
        if (!resolvedProfile.Profile.Enable)
        {
            message = GetProfileDisabledMessage(resolvedProfile.Faction);
            return false;
        }

        if (!TryGetUsablePawn(connectedPlayer, requireAlive: true, out var pawn))
        {
            message = "Flashlight.ErrorPawnUnavailable";
            return false;
        }

        if (!TryCreateFlashlightEntity(connectedPlayer, pawn, state, resolvedProfile, out message))
        {
            enabled = false;
            return false;
        }

        state.Enabled = true;
        enabled = true;
        message = "Flashlight.StateEnabled";
        return true;
    }

    public bool TrySetForPlayer(IPlayer? player, bool shouldEnable, out bool changed, out string message)
    {
        changed = false;
        message = "Flashlight.ErrorServiceUnavailable";

        if (!_config.Enable)
        {
            message = "Flashlight.ErrorDisabledInConfig";
            return false;
        }

        if (!TryGetConnectedPlayer(player, out var connectedPlayer))
        {
            message = "Flashlight.ErrorPlayerUnavailableForControl";
            return false;
        }

        var state = GetOrCreateState(connectedPlayer);
        var currentTime = _core.Engine.GlobalVars.CurrentTime;

        if (!shouldEnable)
        {
            DestroyLightEntity(state);

            if (!state.Enabled)
            {
                message = "Flashlight.StateAlreadyDisabled";
                return true;
            }

            state.Enabled = false;
            state.LastToggleTime = currentTime;
            state.NextRetryTime = 0.0f;
            changed = true;
            message = "Flashlight.StateDisabled";
            return true;
        }

        if (!IsUserFlashlightEnabled(connectedPlayer))
        {
            ForceDisableState(state);
            message = "Flashlight.ErrorUserDisabled";
            return false;
        }

        if (state.Enabled)
        {
            message = "Flashlight.StateAlreadyEnabled";
            return true;
        }

        var resolvedProfile = ResolveEffectiveProfile(connectedPlayer);
        if (!resolvedProfile.Profile.Enable)
        {
            message = GetProfileDisabledMessage(resolvedProfile.Faction);
            return false;
        }

        if (!TryGetUsablePawn(connectedPlayer, requireAlive: true, out var pawn))
        {
            message = "Flashlight.ErrorPawnUnavailable";
            return false;
        }

        if (!TryCreateFlashlightEntity(connectedPlayer, pawn, state, resolvedProfile, out message))
        {
            return false;
        }

        state.Enabled = true;
        state.LastToggleTime = currentTime;
        state.NextRetryTime = 0.0f;
        changed = true;
        message = "Flashlight.StateEnabled";
        return true;
    }

    public void HandleKeyStateChanged(IOnClientKeyStateChangedEvent @event)
    {
        if (!_config.Enable || !@event.Pressed)
        {
            return;
        }

        if (!string.Equals(@event.Key.ToString(), "F", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var player = _core.PlayerManager.GetPlayer(@event.PlayerId);
        _ = TryToggleForPlayer(player, out _, out _);
    }

    public void HandlePlayerSpawn(int playerId)
    {
        if (!TryGetStateByPlayerId(playerId, out var state))
        {
            return;
        }

        ForceDisableState(state);
    }

    public void HandlePlayerDeath(int playerId)
    {
        if (!TryGetStateByPlayerId(playerId, out var state))
        {
            return;
        }

        ForceDisableState(state);
    }

    public void HandlePlayerInfected(IPlayer? infectedPlayer, string? zombieClassName)
    {
        if (infectedPlayer is null || !infectedPlayer.IsValid)
        {
            return;
        }

        if (!TryGetStateByPlayerId(infectedPlayer.PlayerID, out var state))
        {
            return;
        }

        ForceDisableState(state);
    }

    public void HandleClientDisconnected(int playerId)
    {
        if (!_sessionIdByPlayerId.Remove(playerId, out var sessionId))
        {
            return;
        }

        if (_statesBySessionId.Remove(sessionId, out var state))
        {
            DestroyLightEntity(state);
        }
    }

    public void HandleMapLoad()
    {
        _staleSessions.Clear();
    }

    public void HandleMapUnload()
    {
        ClearAll(clearState: true);
    }

    public void HandleRoundReset()
    {
        foreach (var state in _statesBySessionId.Values)
        {
            ForceDisableState(state);
        }
    }

    public void OnTick()
    {
        if (_statesBySessionId.Count == 0)
        {
            return;
        }

        var currentTime = _core.Engine.GlobalVars.CurrentTime;
        _staleSessions.Clear();

        foreach (var pair in _statesBySessionId)
        {
            var sessionId = pair.Key;
            var state = pair.Value;

            if (!state.Enabled)
            {
                DestroyLightEntity(state);
                continue;
            }

            if (!TryResolvePlayerBySession(sessionId, out var player))
            {
                DestroyLightEntity(state);
                _staleSessions.Add(sessionId);
                continue;
            }

            state.PlayerId = player.PlayerID;
            _sessionIdByPlayerId[player.PlayerID] = sessionId;

            if (!IsUserFlashlightEnabled(player))
            {
                ForceDisableState(state);
                continue;
            }

            if (!_config.AllowBots && player.IsFakeClient)
            {
                ForceDisableState(state);
                continue;
            }

            var resolvedProfile = ResolveEffectiveProfile(player);
            if (!resolvedProfile.Profile.Enable)
            {
                ForceDisableState(state);
                continue;
            }

            if (!TryGetUsablePawn(player, requireAlive: true, out var pawn))
            {
                DestroyLightEntity(state);
                continue;
            }

            var profileHash = GetProfileHash(resolvedProfile.Profile);

            if (TryGetTrackedLightEntity(state, out var trackedLight))
            {
                ApplyTransmitPolicy(trackedLight, player, resolvedProfile);

                if (state.ActiveProfileHash == profileHash)
                {
                    continue;
                }

                DestroyLightEntity(state);
            }

            if (currentTime < state.NextRetryTime)
            {
                continue;
            }

            if (!TryCreateFlashlightEntity(player, pawn, state, resolvedProfile, out _))
            {
                state.NextRetryTime = currentTime + 0.5f;
            }
        }

        foreach (var sessionId in _staleSessions)
        {
            if (_statesBySessionId.Remove(sessionId, out var state))
            {
                _sessionIdByPlayerId.Remove(state.PlayerId);
            }
        }
    }

    public void ClearAll(bool clearState)
    {
        foreach (var state in _statesBySessionId.Values)
        {
            ForceDisableState(state);
        }

        if (!clearState)
        {
            return;
        }

        _statesBySessionId.Clear();
        _sessionIdByPlayerId.Clear();
        _staleSessions.Clear();
    }

    public void HandleUserPreferenceChanged(ulong steamId, string key, bool enabled)
    {
        if (!string.Equals(key, ZPLUserPreferenceKeys.Flashlight, StringComparison.Ordinal) || enabled)
            return;

        foreach (var state in _statesBySessionId.Values)
        {
            if (!TryResolvePlayerBySession(state.SessionId, out var player))
                continue;

            if (player.SteamID == steamId)
            {
                ForceDisableState(state);
                return;
            }
        }
    }

    private void ForceDisableState(FlashlightPlayerState state)
    {
        DestroyLightEntity(state);
        state.Enabled = false;
        state.NextRetryTime = 0.0f;
    }

    private FlashlightPlayerState GetOrCreateState(IPlayer player)
    {
        var sessionId = player.SessionId;

        if (_sessionIdByPlayerId.TryGetValue(player.PlayerID, out var mappedSessionId) && mappedSessionId != sessionId)
        {
            if (_statesBySessionId.Remove(mappedSessionId, out var staleState))
            {
                DestroyLightEntity(staleState);
            }
        }

        if (_statesBySessionId.TryGetValue(sessionId, out var state))
        {
            state.PlayerId = player.PlayerID;
            _sessionIdByPlayerId[player.PlayerID] = sessionId;
            return state;
        }

        state = new FlashlightPlayerState
        {
            SessionId = sessionId,
            PlayerId = player.PlayerID
        };

        _statesBySessionId[sessionId] = state;
        _sessionIdByPlayerId[player.PlayerID] = sessionId;
        return state;
    }

    private bool IsUserFlashlightEnabled(IPlayer player)
        => _zpApi?.ZPL_GetUserPreference(player, ZPLUserPreferenceKeys.Flashlight, true) ?? true;

    private bool TryGetStateByPlayerId(int playerId, out FlashlightPlayerState state)
    {
        state = null!;

        if (!_sessionIdByPlayerId.TryGetValue(playerId, out var sessionId))
        {
            return false;
        }

        if (!_statesBySessionId.TryGetValue(sessionId, out var existingState))
        {
            return false;
        }

        state = existingState;
        return true;
    }

    private bool TryCreateFlashlightEntity(
        IPlayer player,
        CCSPlayerPawn pawn,
        FlashlightPlayerState state,
        ResolvedFlashlightProfile resolvedProfile,
        out string errorMessage)
    {
        errorMessage = "Flashlight.ErrorCreateEntityFailed";
        DestroyLightEntity(state);

        CBarnLight? light;

        try
        {
            light = _core.EntitySystem.CreateEntityByDesignerName<CBarnLight>(FlashlightDesignerName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to allocate flashlight entity '{DesignerName}'.", FlashlightDesignerName);
            return false;
        }

        if (light is null || !light.IsValid)
        {
            errorMessage = "Flashlight.ErrorCreateEntityUnavailable";
            return false;
        }

        try
        {
            ConfigureLightEntity(light, resolvedProfile.Profile);

            using var entityKv = CreateFlashlightKeyValues();
            light.DispatchSpawn(entityKv);

            ApplyTransmitPolicy(light, player, resolvedProfile);
            AttachLightToPawn(light, pawn, resolvedProfile.Profile);
            EnableLight(light);

            state.LightEntityIndex = light.Index;
            state.LightDesignerName = FlashlightDesignerName;
            state.NextRetryTime = 0.0f;
            state.ActiveProfileHash = GetProfileHash(resolvedProfile.Profile);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to initialize flashlight entity using designer '{DesignerName}' for player {PlayerId}.",
                FlashlightDesignerName,
                player.PlayerID);
            TryDestroyEntity(light);
            return false;
        }
    }

    private ResolvedFlashlightProfile ResolveEffectiveProfile(IPlayer player)
    {
        var faction = ResolvePlayerFaction(player);
        var profile = (faction == FlashlightFaction.Zombie ? _config.Zombie : _config.Human).Clone();
        string? zombieClassName = null;

        if (faction == FlashlightFaction.Zombie)
        {
            zombieClassName = TryGetZombieClassName(player);

            if (!string.IsNullOrWhiteSpace(zombieClassName))
            {
                var specialConfig = _config.SpecialZombies.FirstOrDefault(group =>
                    group.Enable
                    && !string.IsNullOrWhiteSpace(group.Name)
                    && string.Equals(group.Name.Trim(), zombieClassName, StringComparison.OrdinalIgnoreCase));

                specialConfig?.ApplyTo(profile);
            }
        }

        return new ResolvedFlashlightProfile(profile, faction, zombieClassName);
    }

    private FlashlightFaction ResolvePlayerFaction(IPlayer player)
    {
        if (TryIsZombie(player, out var isZombie))
        {
            return isZombie ? FlashlightFaction.Zombie : FlashlightFaction.Human;
        }

        if (player.Controller is null || !player.Controller.IsValid)
        {
            return FlashlightFaction.Unknown;
        }

        return player.Controller.TeamNum switch
        {
            (int)Team.CT => FlashlightFaction.Human,
            (int)Team.T => FlashlightFaction.Zombie,
            _ => FlashlightFaction.Unknown
        };
    }

    private bool TryIsZombie(IPlayer player, out bool isZombie)
    {
        isZombie = false;
        var zpApi = _zpApi;
        if (zpApi is null)
        {
            return false;
        }

        try
        {
            isZombie = zpApi.ZPL_IsZombie(player.PlayerID);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string? TryGetZombieClassName(IPlayer player)
    {
        var zpApi = _zpApi;
        if (zpApi is null)
        {
            return null;
        }

        try
        {
            var className = zpApi.ZPL_GetZombieClassname(player);
            return string.IsNullOrWhiteSpace(className) ? null : className.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string GetProfileDisabledMessage(FlashlightFaction faction)
    {
        return faction == FlashlightFaction.Zombie
            ? "Flashlight.ErrorProfileDisabledZombie"
            : "Flashlight.ErrorProfileDisabledHuman";
    }

    private static int GetProfileHash(ZPLFlashlight_ProfileConfig profile)
    {
        var hash = new HashCode();
        hash.Add(profile.Enable);
        hash.Add(profile.Brightness);
        hash.Add(profile.Distance);
        hash.Add(profile.AttachmentDistance);
        hash.Add(profile.FovOrConeAngle);
        hash.Add(profile.Shadows);
        hash.Add(profile.Attachment);
        hash.Add(profile.ColorR);
        hash.Add(profile.ColorG);
        hash.Add(profile.ColorB);
        hash.Add(profile.ColorA);
        hash.Add(profile.VisibleToTeammates);
        return hash.ToHashCode();
    }

    private static CEntityKeyValues CreateFlashlightKeyValues()
    {
        var entityKv = new CEntityKeyValues();
        entityKv.SetString("lightcookie", FlashlightCookiePath);
        return entityKv;
    }

    private bool TryGetTrackedLightEntity(FlashlightPlayerState state, out CBarnLight entity)
    {
        entity = null!;

        if (!state.LightEntityIndex.HasValue)
        {
            return false;
        }

        try
        {
            var trackedEntity = _core.EntitySystem.GetEntityByIndex<CBarnLight>(state.LightEntityIndex.Value);
            if (trackedEntity is null
                || !trackedEntity.IsValid
                || (!string.IsNullOrWhiteSpace(state.LightDesignerName)
                    && !string.Equals(trackedEntity.DesignerName, state.LightDesignerName, StringComparison.Ordinal)))
            {
                state.LightEntityIndex = null;
                state.LightDesignerName = null;
                state.ActiveProfileHash = null;
                return false;
            }

            entity = trackedEntity;
            return true;
        }
        catch
        {
            state.LightEntityIndex = null;
            state.LightDesignerName = null;
            state.ActiveProfileHash = null;
            return false;
        }
    }

    private void DestroyLightEntity(FlashlightPlayerState state)
    {
        if (!TryGetTrackedLightEntity(state, out var entity))
        {
            state.LightEntityIndex = null;
            state.LightDesignerName = null;
            state.ActiveProfileHash = null;
            return;
        }

        TryDestroyEntity(entity);
        state.LightEntityIndex = null;
        state.LightDesignerName = null;
        state.ActiveProfileHash = null;
    }

    private static void TryDestroyEntity(CEntityInstance entity)
    {
        if (entity is null || !entity.IsValid)
        {
            return;
        }

        try
        {
            entity.Despawn();
            return;
        }
        catch
        {
        }

        _ = TryAcceptInput(entity, "Kill", string.Empty);
    }

    private void ConfigureLightEntity(CBarnLight light, ZPLFlashlight_ProfileConfig profile)
    {
        light.Enabled = false;
        light.EnabledUpdated();

        light.ColorMode = 0;
        light.ColorModeUpdated();

        light.Color = ResolveConfiguredColor(profile);
        light.ColorUpdated();

        light.Brightness = profile.Brightness;
        light.BrightnessUpdated();

        light.BrightnessScale = 1.0f;
        light.BrightnessScaleUpdated();

        light.Range = profile.Distance;
        light.RangeUpdated();

        light.SoftX = BarnSoftX;
        light.SoftXUpdated();

        light.SoftY = BarnSoftY;
        light.SoftYUpdated();

        light.Skirt = BarnSkirt;
        light.SkirtUpdated();

        light.SkirtNear = BarnSkirtNear;
        light.SkirtNearUpdated();

        // CS2Fixes uses (angle, angle, 0.02f) for the barn-light cone vector.
        light.SizeParams = new Vector(profile.FovOrConeAngle, profile.FovOrConeAngle, BarnSizeExponent);
        light.SizeParamsUpdated();

        light.CastShadows = profile.Shadows ? 1 : 0;
        light.CastShadowsUpdated();

        light.DirectLight = DirectLightMode;
        light.DirectLightUpdated();
    }

    private static Color ResolveConfiguredColor(ZPLFlashlight_ProfileConfig profile)
    {
        return new Color(
            Math.Clamp(profile.ColorR, 0, 255),
            Math.Clamp(profile.ColorG, 0, 255),
            Math.Clamp(profile.ColorB, 0, 255),
            Math.Clamp(profile.ColorA, 0, 255));
    }

    private void ApplyTransmitPolicy(CEntityInstance entity, IPlayer owner, ResolvedFlashlightProfile resolvedProfile)
    {
        _ = TrySetTransmitState(entity, false, null);

        if (!resolvedProfile.Profile.VisibleToTeammates)
        {
            _ = TrySetTransmitState(entity, true, owner.PlayerID);
            return;
        }

        foreach (var viewer in _core.PlayerManager.GetAllValidPlayers())
        {
            if (viewer is null || !viewer.IsValid || viewer.PlayerID < 0)
            {
                continue;
            }

            var visible = CanViewerSeeFlashlight(owner, viewer, resolvedProfile);
            _ = TrySetTransmitState(entity, visible, viewer.PlayerID);
        }
    }

    private bool CanViewerSeeFlashlight(IPlayer owner, IPlayer viewer, ResolvedFlashlightProfile resolvedProfile)
    {
        if (!viewer.IsValid)
        {
            return false;
        }

        if (viewer.PlayerID == owner.PlayerID)
        {
            return true;
        }

        if (!resolvedProfile.Profile.VisibleToTeammates)
        {
            return false;
        }

        var viewerFaction = ResolvePlayerFaction(viewer);
        if (resolvedProfile.Faction != FlashlightFaction.Unknown && viewerFaction != FlashlightFaction.Unknown)
        {
            return resolvedProfile.Faction == viewerFaction;
        }

        return ArePlayersOnSamePlayableTeam(owner, viewer);
    }

    private static bool ArePlayersOnSamePlayableTeam(IPlayer left, IPlayer right)
    {
        if (left.Controller is null || !left.Controller.IsValid || right.Controller is null || !right.Controller.IsValid)
        {
            return false;
        }

        var leftTeam = left.Controller.TeamNum;
        var rightTeam = right.Controller.TeamNum;

        if (!IsPlayableTeam(leftTeam) || !IsPlayableTeam(rightTeam))
        {
            return false;
        }

        return leftTeam == rightTeam;
    }

    private static bool IsPlayableTeam(int teamNum)
    {
        return teamNum == (int)Team.CT || teamNum == (int)Team.T;
    }

    private void AttachLightToPawn(CBarnLight light, CCSPlayerPawn pawn, ZPLFlashlight_ProfileConfig profile)
    {
        var transform = ResolveAttachmentTransform(pawn, profile);

        _ = TryAcceptInput(light, "ClearParent", string.Empty);
        // Match CS2Fixes: place the flashlight slightly forward once, then let
        // the attachment preserve that offset without any per-tick updates.
        light.Teleport(transform.Position, transform.Angles, null);
        _ = TryAcceptInput(light, "SetParent", "!activator", pawn, light);

        if (!string.IsNullOrWhiteSpace(profile.Attachment))
        {
            _ = TryAcceptInput(light, "SetParentAttachmentMaintainOffset", profile.Attachment);
        }
    }

    private static void EnableLight(CBarnLight light)
    {
        if (TryAcceptInput(light, "Enable", string.Empty))
        {
            return;
        }

        light.Enabled = true;
        light.EnabledUpdated();
    }

    private static FlashlightTransform ResolveAttachmentTransform(CCSPlayerPawn pawn, ZPLFlashlight_ProfileConfig profile)
    {
        var position = pawn.AbsOrigin ?? Vector.Zero;
        var scale = 1.0f;

        var sceneNode = pawn.CBodyComponent?.SceneNode;
        if (sceneNode is not null)
        {
            scale = sceneNode.Scale;
        }

        position += new Vector(0.0f, 0.0f, 64.0f * scale);

        var angles = pawn.EyeAngles;
        angles.ToDirectionVectors(out var forward, out _, out _);
        position += forward * (profile.AttachmentDistance * scale);

        return new FlashlightTransform(position, angles);
    }

    private static bool TryAcceptInput<T>(
        CEntityInstance entity,
        string input,
        T value,
        CEntityInstance? activator = null,
        CEntityInstance? caller = null)
    {
        try
        {
            entity.AcceptInput(input, value, activator, caller);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TrySetTransmitState(CEntityInstance entity, bool visible, int? playerId)
    {
        try
        {
            if (playerId.HasValue)
            {
                entity.SetTransmitState(visible, playerId.Value);
                return true;
            }

            entity.SetTransmitState(visible);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryGetConnectedPlayer(IPlayer? player, out IPlayer connectedPlayer)
    {
        connectedPlayer = null!;

        if (player is null || !player.IsValid || player.PlayerID < 0)
        {
            return false;
        }

        if (!_config.AllowBots && player.IsFakeClient)
        {
            return false;
        }

        if (player.Controller is null || !player.Controller.IsValid)
        {
            return false;
        }

        connectedPlayer = player;
        return true;
    }

    private static bool TryGetUsablePawn(IPlayer player, bool requireAlive, out CCSPlayerPawn pawn)
    {
        pawn = null!;

        if (player.Controller is null || !player.Controller.IsValid)
        {
            return false;
        }

        if (requireAlive && !player.IsAlive)
        {
            return false;
        }

        if (player.PlayerPawn is null || !player.PlayerPawn.IsValid)
        {
            return false;
        }

        pawn = player.PlayerPawn;
        return true;
    }

    private bool TryResolvePlayerBySession(ulong sessionId, out IPlayer player)
    {
        player = null!;
        var currentPlayer = _core.PlayerManager.GetPlayerFromSessionId(sessionId);

        if (currentPlayer is null || !currentPlayer.IsValid)
        {
            return false;
        }

        if (currentPlayer.SessionId != sessionId)
        {
            return false;
        }

        player = currentPlayer;
        return true;
    }

    private readonly record struct FlashlightTransform(Vector Position, QAngle Angles);
    private readonly record struct ResolvedFlashlightProfile(
        ZPLFlashlight_ProfileConfig Profile,
        FlashlightFaction Faction,
        string? ZombieClassName);

    private enum FlashlightFaction
    {
        Unknown = 0,
        Human = 1,
        Zombie = 2
    }

    private sealed class FlashlightPlayerState
    {
        public required ulong SessionId { get; init; }
        public int PlayerId { get; set; }
        public bool Enabled { get; set; }
        public float LastToggleTime { get; set; }
        public float NextRetryTime { get; set; }
        public uint? LightEntityIndex { get; set; }
        public string? LightDesignerName { get; set; }
        public int? ActiveProfileHash { get; set; }
    }
}
