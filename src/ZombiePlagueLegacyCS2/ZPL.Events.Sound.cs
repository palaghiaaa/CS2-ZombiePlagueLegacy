using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.SchemaDefinitions;


namespace ZombiePlagueLegacyCS2;

public partial class ZPLEvents
{
    public void HookZombieSoundEvents()
    {
        _core.GameEvent.HookPre<EventPlayerSpawn>(OnPlayerSoundSpawn);
        _core.GameEvent.HookPre<EventPlayerHurt>(OnPlayerSoundHurt);
        _core.GameEvent.HookPre<EventPlayerDeath>(OnPlayerSoundDeath);
        _core.GameEvent.HookPre<EventPlayerHurt>(OnPlayerSoundAttack);
        _core.GameEvent.HookPre<EventWeaponFire>(OnWeaponSoundFire);
    }

    /// <summary>
    /// Removes the C# delegate subscriptions registered in
    /// <see cref="HookZombieSoundEvents"/>. GameEvent pre-hooks for the same
    /// event types are cleaned up by <see cref="UnhookEvents"/> via
    /// <c>IGameEventService.UnhookPre&lt;T&gt;()</c>.
    /// </summary>
    public void UnhookZombieSoundEvents()
    {
    }


    private HookResult OnPlayerSoundSpawn(EventPlayerSpawn @event)
    {
        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var Id = player.PlayerID;
        _globals.IsZombie.TryGetValue(Id, out bool IsZombie);

        if (IsZombie)
        {
            var zombieConfig = _zombieClassCFG.CurrentValue;
            var specialConfig = _SpecialClassCFG.CurrentValue;
            var zombie = _zombieState.GetZombieClass(Id, zombieConfig.ZombieClassList, specialConfig.SpecialClassList);
            if (zombie == null)
                return HookResult.Continue;

            if (_globals.RoundVoxGroup != null)
            {
                _service.PlayerSelectSoundtoAll(_globals.RoundVoxGroup.ZombieSpawnVox, _globals.RoundVoxGroup.Volume);
            }

            _core.Scheduler.DelayBySeconds(0.5f, () =>
            {
                var intervalMs = zombie.Stats.IdleInterval;
                var randomOffset = Random.Shared.Next(0, (int)intervalMs);

                var now = Environment.TickCount / 1000f;
                _globals.g_ZombieIdleStates[player.PlayerID] = new ZombieIdleState
                {
                    PlayerID = player.PlayerID,
                    IdleInterval = zombie.Stats.IdleInterval,
                    NextIdleTime = now + intervalMs + randomOffset
                };
            });
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerSoundHurt(EventPlayerHurt @event)
    {
        var attacker = @event.AttackerPlayer;
        if (attacker == null || !attacker.IsValid)
            return HookResult.Continue;

        var attackercontroller = attacker.Controller;
        if (attackercontroller == null || !attackercontroller.IsValid)
            return HookResult.Continue;

        var attackerpawn = attacker.PlayerPawn;
        if (attackerpawn == null || !attackerpawn.IsValid)
            return HookResult.Continue;

        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid)
            return HookResult.Continue;


        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return HookResult.Continue;


        var Id = player.PlayerID;
        _globals.IsZombie.TryGetValue(Id, out bool IsZombie);

        if (!IsZombie || pawn.TeamNum == attackerpawn.TeamNum || player == attacker)
            return HookResult.Continue;

        var zombieConfig = _zombieClassCFG.CurrentValue;
        var specialConfig = _SpecialClassCFG.CurrentValue;
        var zombie = _zombieState.GetZombieClass(Id, zombieConfig.ZombieClassList, specialConfig.SpecialClassList);
        if (zombie == null)
            return HookResult.Continue;

        bool isHeadshot = @event.ActualHitGroup == HitGroup_t.HITGROUP_HEAD;
        string soundPath = isHeadshot ? zombie.Sounds.SoundPain : zombie.Sounds.SoundHurt;
        _service.PlayerSelectSoundtoEntity(player, soundPath, zombie.Stats.ZombieSoundVolume);
        
        return HookResult.Continue;

    }

    private HookResult OnPlayerSoundDeath(EventPlayerDeath @event)
    {
        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var Id = player.PlayerID;
        

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return HookResult.Continue;


        _globals.IsZombie.TryGetValue(Id, out bool IsZombie);
        if (!IsZombie)
            return HookResult.Continue;

        _globals.g_ZombieIdleStates.Remove(Id);

        var zombieConfig = _zombieClassCFG.CurrentValue;
        var specialConfig = _SpecialClassCFG.CurrentValue;
        var zombie = _zombieState.GetZombieClass(Id, zombieConfig.ZombieClassList, specialConfig.SpecialClassList);
        if (zombie == null)
            return HookResult.Continue;

        _service.PlayerSelectSoundtoEntity(player, zombie.Sounds.SoundDeath, zombie.Stats.ZombieSoundVolume);

        return HookResult.Continue;
    }

    private HookResult OnPlayerSoundAttack(EventPlayerHurt @event)
    {
        var attacker = @event.AttackerPlayer;
        if (attacker == null || !attacker.IsValid)
            return HookResult.Continue;


        var Id = attacker.PlayerID;

        _globals.IsZombie.TryGetValue(Id, out bool IsZombie);

        var controller = attacker.Controller;
        if (controller == null || !controller.IsValid)
            return HookResult.Continue;

        if (!IsZombie)
            return HookResult.Continue;

        if (!_helpers.IsPlayerUsingKnife(controller))
            return HookResult.Continue;

        _globals.InSwing[Id] = true;

        var zombieConfig = _zombieClassCFG.CurrentValue;
        var specialConfig = _SpecialClassCFG.CurrentValue;
        var zombie = _zombieState.GetZombieClass(Id, zombieConfig.ZombieClassList, specialConfig.SpecialClassList);
        if (zombie == null)
            return HookResult.Continue;

        _service.PlayerSelectSoundtoEntity(attacker, zombie.Sounds.HitSound, zombie.Stats.ZombieSoundVolume);


        return HookResult.Continue;
    }

    private HookResult OnWeaponSoundFire(EventWeaponFire @event)
    {
        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var Id = player.PlayerID;
        _globals.IsZombie.TryGetValue(Id, out bool IsZombie);

        var controller = player.Controller;
        if (controller == null || !controller.IsValid)
            return HookResult.Continue;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return HookResult.Continue;

        if (!IsZombie)
            return HookResult.Continue;

        var zombieConfig = _zombieClassCFG.CurrentValue;
        var specialConfig = _SpecialClassCFG.CurrentValue;
        var zombie = _zombieState.GetZombieClass(Id, zombieConfig.ZombieClassList, specialConfig.SpecialClassList);
        if (zombie == null)
            return HookResult.Continue;

        if (!_helpers.IsPlayerUsingKnife(controller))
            return HookResult.Continue;


        _globals.InSwing[player.PlayerID] = false;
        var playerId = player.PlayerID;
        var sessionId = player.SessionId;
        _core.Scheduler.DelayBySeconds(0.05f, () =>
        {
            if (player == null || !player.IsValid || player.SessionId != sessionId)
                return;

            var zombieConfigLate = _zombieClassCFG.CurrentValue;
            var specialConfigLate = _SpecialClassCFG.CurrentValue;
            var zombieLate = _zombieState.GetZombieClass(playerId, zombieConfigLate.ZombieClassList, specialConfigLate.SpecialClassList);
            if (zombieLate == null)
                return;

            if (!_globals.InSwing[playerId])
                _service.PlayerSelectSoundtoEntity(player, zombieLate.Sounds.SwingSound, zombieLate.Stats.ZombieSoundVolume);
        });

        return HookResult.Continue;
    }

    private void HandleEntityTakeSoundDamage(IOnEntityTakeDamageEvent @event, in DamageEventContext context)
    {
        var attackerPlayer = context.AttackerPlayer;
        if (attackerPlayer == null || !attackerPlayer.IsValid)
            return;

        if (!TryGetActiveWeapon(context.AttackerPawn, out var activeWeapon))
            return;

        if (activeWeapon.DesignerName != "weapon_knife")
            return;

        var Id = attackerPlayer.PlayerID;
        _globals.IsZombie.TryGetValue(Id, out bool IsZombie);

        if (!IsZombie)
            return;

        var zombieConfig = _zombieClassCFG.CurrentValue;
        var specialConfig = _SpecialClassCFG.CurrentValue;
        var zombie = _zombieState.GetZombieClass(Id, zombieConfig.ZombieClassList, specialConfig.SpecialClassList);
        if (zombie == null)
            return;

        if (context.VictimEntity.DesignerName != "worldent")
            return;

        _globals.InSwing[attackerPlayer.PlayerID] = true;
        _service.PlayerSelectSoundtoEntity(attackerPlayer, zombie.Sounds.HitWallSound, zombie.Stats.ZombieSoundVolume);
    }

    private void HandleInGrenadeDamage(IOnEntityTakeDamageEvent @event, in DamageEventContext context)
    {
        var victimPlayer = context.VictimPlayer;
        if (victimPlayer == null || !victimPlayer.IsValid)
            return;

        var Id = victimPlayer.PlayerID;
        _globals.IsZombie.TryGetValue(Id, out bool IsZombie);

        if (!IsZombie)
            return;

        var zombieConfig = _zombieClassCFG.CurrentValue;
        var specialConfig = _SpecialClassCFG.CurrentValue;
        var zombie = _zombieState.GetZombieClass(Id, zombieConfig.ZombieClassList, specialConfig.SpecialClassList);
        if (zombie == null)
            return;

        if (@event.Info.DamageType == DamageTypes_t.DMG_BURN)
        {
            if (Random.Shared.Next(0, 10) <= 2)
            {
                _service.PlayerSelectSoundtoEntity(victimPlayer, zombie.Sounds.BurnSound, zombie.Stats.ZombieSoundVolume);
            }
        }
        else if (@event.Info.DamageType == DamageTypes_t.DMG_BLAST)
        {
            _service.PlayerSelectSoundtoEntity(victimPlayer, zombie.Sounds.ExplodeSound, zombie.Stats.ZombieSoundVolume);
        }
    }

}

