using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using static ZombieOutstandingCS2.ZOMineCFG;

namespace ZombieOutstandingCS2;

/// <summary>
/// Core mine service: creates, tracks, and destroys laser trip-mines.
/// Implements the same logic as H-AN/HanLaserTripmineS2 but integrated
/// into the ZombieOutstandingCS2 plugin.
/// </summary>
public class ZOMineService
{
    private readonly ILogger<ZOMineService> _logger;
    private readonly ISwiftlyCore _core;
    private readonly ZOGlobals _globals;

    /// <summary>Units to advance past a hit entity so the next penetration trace doesn't re-hit it.</summary>
    private const float TraceAdvanceDistance = 8f;
    private readonly IOptionsMonitor<ZOMineCFG> _mineCFG;

    public ZOMineService(
        ISwiftlyCore core,
        ILogger<ZOMineService> logger,
        ZOGlobals globals,
        IOptionsMonitor<ZOMineCFG> mineCFG)
    {
        _core    = core;
        _logger  = logger;
        _globals = globals;
        _mineCFG = mineCFG;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a mine entity for <paramref name="player"/> using the mine type
    /// identified by <paramref name="mineName"/>.  Returns the spawned entity
    /// or null on failure.
    /// </summary>
    public CBaseModelEntity? CreateMineEnt(IPlayer player, string mineName)
    {
        if (player == null || !player.IsValid) return null;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid) return null;

        var controller = player.Controller;
        if (controller == null || !controller.IsValid) return null;

        if (string.IsNullOrEmpty(mineName)) return null;

        var mineConfig = GetMineConfigByName(mineName);
        if (mineConfig == null) return null;

        var steamId = player.SteamID;
        if (steamId == 0) return null;

        // ── Limit check ───────────────────────────────────────────────────────
        if (!_globals.PlayerMineCounts.TryGetValue(steamId, out var playerMines))
        {
            playerMines = new Dictionary<string, HashSet<uint>>();
            _globals.PlayerMineCounts[steamId] = playerMines;
        }
        if (!playerMines.TryGetValue(mineName, out var mineSet))
        {
            mineSet = new HashSet<uint>();
            playerMines[mineName] = mineSet;
        }
        if (mineConfig.Limit > 0 && mineSet.Count >= mineConfig.Limit)
        {
            player.SendMessage(MessageType.Chat,
                _core.Translation.GetPlayerLocalizer(player)["MineLimit", mineConfig.Limit]);
            return null;
        }

        // ── Price check (uses in-game money, not ammo packs) ──────────────────
        if (mineConfig.Price > 0)
        {
            var moneyServices = controller.InGameMoneyServices;
            if (moneyServices != null && moneyServices.IsValid)
            {
                if (moneyServices.Account < mineConfig.Price)
                {
                    player.SendMessage(MessageType.Chat,
                        _core.Translation.GetPlayerLocalizer(player)["NoMoney"]);
                    return null;
                }
                moneyServices.Account -= mineConfig.Price;
                controller.InGameMoneyServicesUpdated();
            }
        }

        // ── Trace from eye to find placement surface ──────────────────────────
        if (!CreateTraceByEyePosition(player, out CGameTrace trace, out Vector playerForward))
            return null;

        // ── Spawn mine entity ─────────────────────────────────────────────────
        CBaseModelEntity? mineEntity =
            _core.EntitySystem.CreateEntityByDesignerName<CBaseModelEntity>("prop_dynamic_override");
        if (mineEntity == null) return null;

        try
        {
            mineEntity.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= ~(uint)(1 << 2);
            mineEntity.DispatchSpawn();

            var mineHandle = _core.EntitySystem.GetRefEHandle(mineEntity);
            if (!mineHandle.IsValid)
            {
                if (mineEntity.IsValid) mineEntity.AcceptInput("Kill", 0);
                return null;
            }

            var mineData = new MineData
            {
                Name                = mineConfig.Name,
                Model               = mineConfig.Model,
                CanExplorer         = mineConfig.CanExplorer,
                CanOwnerTeamTrigger = mineConfig.CanOwnerTeamTrigger,
                LaserRate           = mineConfig.LaserRate,
                LaserDamage         = mineConfig.LaserDamage,
                LaserKnockBack      = mineConfig.LaserKnockBack,
                ExplorerRadius      = mineConfig.ExplorerRadius,
                ExplorerDamage      = mineConfig.ExplorerDamage,
                Team                = mineConfig.Team,
                Price               = mineConfig.Price,
                Limit               = mineConfig.Limit,
                Permissions         = mineConfig.Permissions,
                GlowColor           = mineConfig.GlowColor,
                LaserColor          = mineConfig.LaserColor,
                LaserSize           = mineConfig.LaserSize,
                MineOpenSound       = mineConfig.MineOpenSound,
                LaserOpenSound      = mineConfig.LaserOpenSound,
                LaserTouchSound     = mineConfig.LaserTouchSound,
                ModelAngleFix       = mineConfig.ModelAngleFix,
            };

            _globals.MineData[mineHandle.Raw] = mineData;
            mineSet.Add(mineHandle.Raw);

            var ent = mineHandle.Value;
            if (ent == null)
            {
                _globals.MineData.Remove(mineHandle.Raw);
                mineSet.Remove(mineHandle.Raw);
                if (mineEntity.IsValid) mineEntity.AcceptInput("Kill", 0);
                return null;
            }

            // ── Configure entity properties (model must be set after spawn) ────
            _core.Scheduler.NextTick(() =>
            {
                if (ent == null || !ent.IsValid) return;
                if (pawn == null || !pawn.IsValid)
                {
                    // Owner gone before setup — remove tracking and destroy entity
                    if (_globals.MineThink.TryGetValue(mineHandle.Raw, out var t))
                    {
                        t?.Cancel();
                        _globals.MineThink.Remove(mineHandle.Raw);
                    }
                    _globals.MineData.Remove(mineHandle.Raw);
                    mineSet.Remove(mineHandle.Raw);
                    ent.AcceptInput("Kill", 0);
                    return;
                }

                ent.SetModel(mineData.Model);
                ent.OwnerEntity.Raw = pawn.Index;
                ent.OwnerEntityUpdated();
                ent.MaxHealth = 3000;
                ent.Health    = 3000;
                ent.MoveType  = MoveType_t.MOVETYPE_NONE;
                ent.MoveTypeUpdated();

                SetGlow(ent, mineData.GlowColor, mineData.Model, mineData.Team);
            });

            // ── Position and orient the mine ───────────────────────────────────
            var endPos = trace.EndPos;
            var normal = trace.HitNormal;
            var angle  = NormalToAngles(normal, playerForward, mineData, out bool isVerticalSurface);
            ent.Teleport(endPos, angle, null);

            EmitSoundFromEntity(mineHandle, mineData.MineOpenSound);

            // ── Create beam 1 second after spawn ──────────────────────────────
            if (!TryParseColor(mineData.LaserColor, out SwiftlyS2.Shared.Natives.Color laserColor))
                laserColor = new SwiftlyS2.Shared.Natives.Color(0, 255, 0, 255);

            _core.Scheduler.DelayBySeconds(1.0f, () =>
            {
                if (ent == null || !ent.IsValid) return;
                CreateBeam(player, mineHandle, laserColor, mineData, isVerticalSurface);
            });

            return mineEntity;
        }
        catch (Exception ex)
        {
            _logger.LogError("[ZO-Mine] CreateMineEnt failed: {Ex}", ex.Message);
            if (mineEntity.IsValid)
                mineEntity.AcceptInput("Kill", 0);
            return null;
        }
    }

    /// <summary>Creates the laser beam and starts the mine's think loop.</summary>
    public void CreateBeam(
        IPlayer player,
        CHandle<CBaseModelEntity> mineHandle,
        SwiftlyS2.Shared.Natives.Color color,
        MineData mineData,
        bool isVerticalSurface)
    {
        if (!mineHandle.IsValid) return;
        var ent = mineHandle.Value;
        if (ent == null || !ent.IsValid) return;

        if (!CreateTraceByEntity(mineHandle, out CGameTrace trace, out Vector forward, mineData, isVerticalSurface))
            return;

        CBeam? beam = _core.EntitySystem.CreateEntity<CBeam>();
        if (beam == null) return;
        beam.DispatchSpawn();

        EmitSoundFromEntity(mineHandle, mineData.LaserOpenSound);

        var beamHandle = _core.EntitySystem.GetRefEHandle(beam);
        if (!beamHandle.IsValid) return;
        var beamEnt = beamHandle.Value;
        if (beamEnt == null) return;

        float size = mineData.LaserSize;
        beamEnt.Render   = color;
        beamEnt.Width    = size;
        beamEnt.EndWidth = size;
        beamEnt.Teleport(trace.StartPos, null, null);
        beamEnt.EndPos = trace.EndPos;

        var beamStart = trace.StartPos;
        var beamDir   = forward;

        // Cancel any existing think for this mine
        if (_globals.MineThink.TryGetValue(mineHandle.Raw, out var oldTask))
        {
            oldTask?.Cancel();
            _globals.MineThink.Remove(mineHandle.Raw);
        }

        float rate = mineData.CanExplorer ? 0.1f : mineData.LaserRate;

        var thinkTask = _core.Scheduler.RepeatBySeconds(rate, () =>
        {
            if (ent == null || !ent.IsValid)
            {
                if (_globals.MineThink.TryGetValue(mineHandle.Raw, out var t))
                {
                    t?.Cancel();
                    _globals.MineThink.Remove(mineHandle.Raw);
                }
                return;
            }
            if (!player.IsValid) return;
            PenetratingTrace(player, mineHandle, mineData, beamHandle, beamStart, beamDir, 8192, 10);
        });

        _globals.MineThink[mineHandle.Raw] = thinkTask;
        _globals.MineBeam[mineHandle.Raw]  = beamHandle.Raw;
        _core.Scheduler.StopOnMapChange(thinkTask);
    }

    /// <summary>Performs a penetrating trace along the beam and applies effects to hit players.</summary>
    public void PenetratingTrace(
        IPlayer player,
        CHandle<CBaseModelEntity> mineHandle,
        MineData mineData,
        CHandle<CBeam> beamHandle,
        Vector start,
        Vector direction,
        float maxDistance,
        int maxTargets = 8)
    {
        if (!mineHandle.IsValid) return;
        var ent = mineHandle.Value;
        if (ent == null || !ent.IsValid) return;

        if (!player.IsValid) return;
        var ownerPawn = player.PlayerPawn;
        if (ownerPawn == null || !ownerPawn.IsValid) return;

        Vector currentStart   = start;
        float remainingDist   = maxDistance;
        int penetrationCount  = 0;

        while (remainingDist > 0.1f && penetrationCount < maxTargets)
        {
            Vector end     = currentStart + direction * remainingDist;
            CGameTrace trace = new CGameTrace();

            _core.Trace.SimpleTrace(
                currentStart, end,
                RayType_t.RAY_TYPE_LINE,
                RnQueryObjectSet.Static | RnQueryObjectSet.Dynamic,
                MaskTrace.Hitbox | MaskTrace.Player,
                MaskTrace.Empty, MaskTrace.Empty,
                CollisionGroup.Always,
                ref trace, ent);

            if (!trace.DidHit || trace.Fraction >= 0.99f) break;

            if (trace.HitPlayer(out IPlayer? target) && target != null)
            {
                var targetPawn = target.PlayerPawn;
                if (targetPawn == null || !targetPawn.IsValid)
                {
                    // Advance past this invalid entity so the next iteration doesn't re-hit it
                    currentStart  = trace.HitPoint + direction * TraceAdvanceDistance;
                    remainingDist = remainingDist * (1f - trace.Fraction) - TraceAdvanceDistance;
                    penetrationCount++;
                    continue;
                }

                bool isOwnerTeam = ownerPawn.TeamNum == targetPawn.TeamNum;
                bool canTrigger  = !isOwnerTeam || (isOwnerTeam && mineData.CanOwnerTeamTrigger);

                if (mineData.CanExplorer)
                {
                    if (canTrigger)
                    {
                        CreateGrenadeAndExplode(player, mineHandle, beamHandle, mineData);
                        return;
                    }
                }
                else
                {
                    if (canTrigger)
                    {
                        if (mineData.LaserDamage > 0)
                            ApplyDamage(player, target, mineHandle, mineData.LaserDamage, mineData.LaserTouchSound);
                        if (mineData.LaserKnockBack != 0)
                            ApplyKnockBack(mineHandle, player, target, mineData.LaserKnockBack);
                    }
                }

                currentStart   = trace.HitPoint + direction * TraceAdvanceDistance;
                remainingDist  = remainingDist * (1f - trace.Fraction) - TraceAdvanceDistance;
            }
            else
            {
                break;
            }

            penetrationCount++;
        }
    }

    /// <summary>Triggers a grenade explosion at the mine's position and removes the mine.</summary>
    public void CreateGrenadeAndExplode(
        IPlayer player,
        CHandle<CBaseModelEntity> mineHandle,
        CHandle<CBeam> beamHandle,
        MineData mineData)
    {
        if (!mineHandle.IsValid || !beamHandle.IsValid) return;

        var mine = mineHandle.Value;
        if (mine == null || !mine.IsValid) return;
        var beam = beamHandle.Value;
        if (beam == null || !beam.IsValid) return;

        var minePos   = mine.AbsOrigin;
        var mineAngle = mine.AbsRotation;
        if (minePos == null || mineAngle == null) return;

        if (!player.IsValid) return;
        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid) return;

        var grenade = CHEGrenadeProjectile.EmitGrenade(minePos.Value, mineAngle.Value, mine.AbsVelocity, pawn);
        if (grenade == null) return;

        grenade.DispatchSpawn();
        grenade.Damage      = mineData.ExplorerDamage;
        grenade.DamageUpdated();
        grenade.DmgRadius   = mineData.ExplorerRadius;
        grenade.DmgRadiusUpdated();
        grenade.Globalname  = "激光绊雷";
        grenade.Teleport(minePos, null, null);
        grenade.AcceptInput("InitializeSpawnFromWorld", "", pawn, pawn);
        grenade.DetonateTime.Value = 0;
        grenade.DetonateTimeUpdated();

        // Stop the mine's think loop
        if (_globals.MineThink.TryGetValue(mineHandle.Raw, out var task))
        {
            task?.Cancel();
            _globals.MineThink.Remove(mineHandle.Raw);
        }

        _globals.MineData.Remove(mineHandle.Raw);
        _globals.MineBeam.Remove(mineHandle.Raw);

        // Remove mine/beam entities from the player's count tracking
        RemoveMineFromPlayerCount(player.SteamID, mineHandle.Raw);

        _core.Scheduler.NextTick(() =>
        {
            if (mine.IsValid) mine.AcceptInput("Kill", 0);
            if (beam.IsValid) beam.AcceptInput("Kill", 0);
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Cleanup helpers (called from Events on round-end / death / disconnect)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Kills all mines and clears all tracking data (called on round-end / map-unload).</summary>
    public void CleanupAllMines()
    {
        foreach (var kvp in _globals.MineData)
        {
            // Cancel think timers
            if (_globals.MineThink.TryGetValue(kvp.Key, out var task))
            {
                task?.Cancel();
            }

            // Kill beam entity
            if (_globals.MineBeam.TryGetValue(kvp.Key, out var beamRaw))
                KillBeamEntity(beamRaw);

            // Kill mine entity
            KillMineEntity(kvp.Key);
        }
        _globals.MineThink.Clear();
        _globals.MineData.Clear();
        _globals.MineBeam.Clear();
        _globals.PlayerMineCounts.Clear();
    }

    /// <summary>Kills all mines owned by <paramref name="steamId"/> and updates tracking.</summary>
    public void CleanupMinesForPlayer(ulong steamId)
    {
        if (!_globals.PlayerMineCounts.TryGetValue(steamId, out var playerMines))
            return;

        foreach (var mineSet in playerMines.Values)
        {
            foreach (uint raw in mineSet)
            {
                if (_globals.MineThink.TryGetValue(raw, out var task))
                {
                    task?.Cancel();
                    _globals.MineThink.Remove(raw);
                }
                _globals.MineData.Remove(raw);

                // Capture beam raw for closure before removing from dictionary
                uint beamRaw = _globals.MineBeam.TryGetValue(raw, out var br) ? br : 0;
                _globals.MineBeam.Remove(raw);

                // Try to kill the mine entity
                _core.Scheduler.NextTick(() =>
                {
                    try
                    {
                        var handle = new CHandle<CBaseModelEntity>(raw);
                        if (handle.IsValid)
                            handle.Value?.AcceptInput("Kill", 0);
                    }
                    catch { }
                });

                // Kill the beam entity
                if (beamRaw != 0)
                    KillBeamEntity(beamRaw);
            }
        }

        _globals.PlayerMineCounts.Remove(steamId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Config helper
    // ─────────────────────────────────────────────────────────────────────────

    public LaserMine? GetMineConfigByName(string name)
    {
        var mine = _mineCFG.CurrentValue.MineList.FirstOrDefault(
            m => m.Enable && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (mine == null)
            _logger.LogWarning("[ZO-Mine] Mine type '{Name}' not found in config.", name);

        return mine;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private helpers (geometry, sounds, damage)
    // ─────────────────────────────────────────────────────────────────────────

    private void RemoveMineFromPlayerCount(ulong steamId, uint raw)
    {
        if (_globals.PlayerMineCounts.TryGetValue(steamId, out var playerMines))
        {
            foreach (var mineSet in playerMines.Values)
                mineSet.Remove(raw);
        }
    }

    private void KillBeamEntity(uint beamRaw)
    {
        _core.Scheduler.NextTick(() =>
        {
            try
            {
                var beamHandle = new CHandle<CBeam>(beamRaw);
                if (beamHandle.IsValid)
                    beamHandle.Value?.AcceptInput("Kill", 0);
            }
            catch { }
        });
    }

    private void KillMineEntity(uint mineRaw)
    {
        _core.Scheduler.NextTick(() =>
        {
            try
            {
                var handle = new CHandle<CBaseModelEntity>(mineRaw);
                if (handle.IsValid)
                    handle.Value?.AcceptInput("Kill", 0);
            }
            catch { }
        });
    }

    private bool CreateTraceByEyePosition(IPlayer player, out CGameTrace trace, out Vector forward)
    {
        trace   = new CGameTrace();
        forward = new Vector(0, 0, 0);

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid) return false;

        var eyePos = pawn.EyePosition;
        if (eyePos == null) return false;

        pawn.EyeAngles.ToDirectionVectors(out forward, out _, out _);

        var start = new Vector(eyePos.Value.X, eyePos.Value.Y, eyePos.Value.Z);
        var end   = start + forward * 8192f;

        _core.Trace.SimpleTrace(
            start, end,
            RayType_t.RAY_TYPE_LINE,
            RnQueryObjectSet.Static | RnQueryObjectSet.Dynamic,
            MaskTrace.Solid | MaskTrace.Player,
            MaskTrace.Empty, MaskTrace.Empty,
            CollisionGroup.Player,
            ref trace, null);

        return trace.Fraction < 1.0f;
    }

    private bool CreateTraceByEntity(
        CHandle<CBaseModelEntity> mineHandle,
        out CGameTrace trace,
        out Vector forward,
        MineData mineData,
        bool isVerticalSurface)
    {
        trace   = new CGameTrace();
        forward = new Vector(0, 0, 0);

        if (!mineHandle.IsValid) return false;

        var ent = mineHandle.Value;
        if (ent == null || !ent.IsValid) return false;

        var minePos   = ent.AbsOrigin;
        var mineAngle = ent.AbsRotation;
        if (minePos == null || mineAngle == null) return false;

        QAngle angle = new QAngle(mineAngle.Value.Pitch, mineAngle.Value.Yaw, mineAngle.Value.Roll);
        float angleFix = mineData.ModelAngleFix;

        if (isVerticalSurface)
        {
            if (angleFix != 0)
                angle.Pitch = angle.Pitch > 0 ? angle.Pitch - angleFix : angle.Pitch + angleFix;
        }
        else
        {
            if (angleFix != 0)
                angle.Yaw -= angleFix;
        }

        angle.ToDirectionVectors(out forward, out _, out _);

        var start  = new Vector(minePos.Value.X, minePos.Value.Y, minePos.Value.Z);
        var endPos = start + forward * 8192f;

        _core.Trace.SimpleTrace(
            start, endPos,
            RayType_t.RAY_TYPE_LINE,
            RnQueryObjectSet.Static | RnQueryObjectSet.Dynamic,
            MaskTrace.Solid | MaskTrace.Player,
            MaskTrace.Empty, MaskTrace.Empty,
            CollisionGroup.NPC,
            ref trace, null);

        return true;
    }

    private QAngle NormalToAngles(Vector normal, Vector playerForward, MineData mineData, out bool isVerticalSurface)
    {
        normal.Normalize();
        playerForward.Normalize();

        float angleFix = mineData.ModelAngleFix;
        isVerticalSurface = MathF.Abs(MathF.Abs(normal.Z) - 1.0f) < 0.01f;

        float pitch = 0f, yaw = 0f, roll = 0f;

        if (isVerticalSurface)
        {
            float originalPitch = MathF.Asin(-normal.Z) * 180f / MathF.PI;
            if (angleFix != 0)
            {
                pitch = normal.Z > 0.5f ? originalPitch - angleFix : originalPitch + angleFix;
                roll  = normal.Z > 0.5f ? angleFix : -angleFix;
            }
            else
            {
                pitch = originalPitch;
            }
        }
        else
        {
            pitch = MathF.Asin(-normal.Z) * 180f / MathF.PI;
            yaw   = MathF.Atan2(normal.Y, normal.X) * 180f / MathF.PI;
            if (angleFix != 0) yaw += angleFix;

            Vector right   = normal.Cross(playerForward).Normalized();
            Vector forward = right.Cross(normal).Normalized();
            roll = MathF.Atan2(forward.Z, forward.X) * 180f / MathF.PI;
        }

        return new QAngle(pitch, yaw, roll);
    }

    private void EmitSoundFromEntity(CHandle<CBaseModelEntity> mineHandle, string soundPath)
    {
        if (!mineHandle.IsValid || string.IsNullOrEmpty(soundPath)) return;
        var mine = mineHandle.Value;
        if (mine == null || !mine.IsValid) return;

        var sound = new SwiftlyS2.Shared.Sounds.SoundEvent(soundPath, 1.0f, 1.0f);
        sound.SourceEntityIndex = (int)mine.Index;
        sound.Recipients.AddAllPlayers();
        _core.Scheduler.NextTick(() => sound.Emit());
    }

    private void ApplyDamage(IPlayer attacker, IPlayer target, CHandle<CBaseModelEntity> mineHandle, float damage, string hurtSound)
    {
        if (!mineHandle.IsValid) return;
        var ent = mineHandle.Value;
        if (ent == null || !ent.IsValid) return;

        if (!attacker.IsValid || !target.IsValid) return;
        var attackerPawn = attacker.PlayerPawn;
        var targetPawn   = target.PlayerPawn;
        if (attackerPawn == null || !attackerPawn.IsValid) return;
        if (targetPawn   == null || !targetPawn.IsValid)   return;
        if (attacker == target || attackerPawn.TeamNum == targetPawn.TeamNum) return;

        var damageInfo = new CTakeDamageInfo(attackerPawn, attackerPawn, ent, damage, DamageTypes_t.DMG_BULLET);
        damageInfo.DamageForce = new SwiftlyS2.Shared.Natives.Vector(0, 0, 10f);

        var targetPos = targetPawn.AbsOrigin;
        if (targetPos != null) damageInfo.DamagePosition = targetPos.Value;

        target.TakeDamage(damageInfo);
        EmitSoundFromEntity(mineHandle, hurtSound);
    }

    private void ApplyKnockBack(CHandle<CBaseModelEntity> mineHandle, IPlayer owner, IPlayer target, float force)
    {
        if (!mineHandle.IsValid) return;
        var mine = mineHandle.Value;
        if (mine == null || !mine.IsValid) return;
        if (target == null || !target.IsValid || force <= 0) return;
        if (!owner.IsValid) return;

        var ownerPawn  = owner.PlayerPawn;
        var targetPawn = target.PlayerPawn;
        if (ownerPawn  == null || !ownerPawn.IsValid)  return;
        if (targetPawn == null || !targetPawn.IsValid) return;
        if (owner == target || ownerPawn.TeamNum == targetPawn.TeamNum) return;

        var minePos   = mine.AbsOrigin;
        var targetPos = targetPawn.AbsOrigin;
        if (minePos == null || targetPos == null) return;

        var dir = new Vector(
            targetPos.Value.X - minePos.Value.X,
            targetPos.Value.Y - minePos.Value.Y,
            targetPos.Value.Z - minePos.Value.Z);

        float len = MathF.Sqrt(dir.X * dir.X + dir.Y * dir.Y + dir.Z * dir.Z);
        if (len <= 0.01f) return;

        targetPawn.AbsVelocity = new Vector(
            dir.X / len * force,
            dir.Y / len * force,
            50f);
    }

    private void SetGlow(CBaseEntity entity, string glowColorStr, string modelName, string team)
    {
        if (entity == null || !entity.IsValid) return;
        if (string.IsNullOrEmpty(glowColorStr)) return;
        if (!TryParseColor(glowColorStr, out SwiftlyS2.Shared.Natives.Color parsedColor)) return;
        if (string.IsNullOrEmpty(modelName)) return;

        // Determine which team should see the glow (avoids cross-team wall visibility).
        // CS2 team indices: 2 = T, 3 = CT, -1 = all teams.
        const int GlowTeamT   = 2;
        const int GlowTeamCT  = 3;
        const int GlowTeamAll = -1;
        int glowTeam = (team ?? string.Empty).ToLowerInvariant() switch
        {
            "ct" => GlowTeamCT,
            "t"  => GlowTeamT,
            _    => GlowTeamAll
        };

        CBaseModelEntity? modelRelay = _core.EntitySystem.CreateEntity<CBaseModelEntity>();
        CBaseModelEntity? modelGlow  = _core.EntitySystem.CreateEntity<CBaseModelEntity>();
        if (modelRelay == null || modelGlow == null)
        {
            if (modelRelay != null && modelRelay.IsValid) modelRelay.AcceptInput("Kill", 0);
            if (modelGlow  != null && modelGlow.IsValid)  modelGlow.AcceptInput("Kill", 0);
            return;
        }

        try { modelRelay.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= unchecked((uint)~(1 << 2)); } catch { }
        modelRelay.SetModel(modelName);
        modelRelay.Spawnflags  = 256u;
        modelRelay.RenderMode  = RenderMode_t.kRenderNone;
        modelRelay.DispatchSpawn();

        try { modelGlow.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= unchecked((uint)~(1 << 2)); } catch { }
        modelGlow.SetModel(modelName);
        modelGlow.Spawnflags = 256u;
        modelGlow.DispatchSpawn();

        modelGlow.Glow.GlowColorOverride = parsedColor;
        modelGlow.Glow.GlowRange         = 5000;
        modelGlow.Glow.GlowTeam          = glowTeam;
        modelGlow.Glow.GlowType          = 3;
        modelGlow.Glow.GlowRangeMin      = 100;

        modelRelay.AcceptInput("FollowEntity", "!activator", entity,     modelRelay);
        modelGlow.AcceptInput( "FollowEntity", "!activator", modelRelay, modelGlow);
    }

    private static bool TryParseColor(string colorStr, out SwiftlyS2.Shared.Natives.Color color)
    {
        color = new SwiftlyS2.Shared.Natives.Color(255, 100, 0, 255);
        if (string.IsNullOrEmpty(colorStr)) return false;

        var parts = colorStr.Split(',');
        if (parts.Length < 3 || parts.Length > 4) return false;

        if (byte.TryParse(parts[0].Trim(), out byte r) &&
            byte.TryParse(parts[1].Trim(), out byte g) &&
            byte.TryParse(parts[2].Trim(), out byte b))
        {
            byte a = (parts.Length == 4 && byte.TryParse(parts[3].Trim(), out byte parsedA)) ? parsedA : (byte)255;
            color = new SwiftlyS2.Shared.Natives.Color(r, g, b, a);
            return true;
        }
        return false;
    }
}
