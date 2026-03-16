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
        _core.Event.OnEntityTakeDamage += Event_OnEntityTakeSoundDamage;
        _core.Event.OnEntityTakeDamage += Event_OnInGrenadeDamage;
    }

    /// <summary>
    /// Removes the C# delegate subscriptions registered in
    /// <see cref="HookZombieSoundEvents"/>. GameEvent pre-hooks for the same
    /// event types are cleaned up by <see cref="UnhookEvents"/> via
    /// <c>IGameEventService.UnhookPre&lt;T&gt;()</c>.
    /// </summary>
    public void UnhookZombieSoundEvents()
    {
        _core.Event.OnEntityTakeDamage -= Event_OnEntityTakeSoundDamage;
        _core.Event.OnEntityTakeDamage -= Event_OnInGrenadeDamage;
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

        bool isHeadshot = @event.HitGroup == 1;
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
        _core.Scheduler.DelayBySeconds(0.05f, () =>
        {
            if (player == null || !player.IsValid)
                return;

            if (!_globals.InSwing[player.PlayerID])
                _service.PlayerSelectSoundtoEntity(player, zombie.Sounds.SwingSound, zombie.Stats.ZombieSoundVolume);
        });

        return HookResult.Continue;
    }

    private void Event_OnEntityTakeSoundDamage(IOnEntityTakeDamageEvent @event)
    {
        var attacker = @event.Info.Attacker.Value;
        if (attacker == null || !attacker.IsValid)
            return;

        var AttackerPawn = attacker.As<CCSPlayerPawn>();
        if (AttackerPawn == null || !AttackerPawn.IsValid)
            return;

        var AttackerController = AttackerPawn.Controller.Value?.As<CCSPlayerController>();
        if (AttackerController == null || !AttackerController.IsValid)
            return;

        var AttackerPlayer = _core.PlayerManager.GetPlayerFromController(AttackerController);
        if (AttackerPlayer == null || !AttackerPlayer.IsValid)
            return;

        var Id = AttackerPlayer.PlayerID;
        _globals.IsZombie.TryGetValue(Id, out bool IsZombie);


        if (!IsZombie)
            return;

        if (!_helpers.IsPlayerUsingKnife(AttackerController))
            return;

        var zombieConfig = _zombieClassCFG.CurrentValue;
        var specialConfig = _SpecialClassCFG.CurrentValue;
        var zombie = _zombieState.GetZombieClass(Id, zombieConfig.ZombieClassList, specialConfig.SpecialClassList);
        if (zombie == null)
            return;

        var entity = @event.Entity;
        if (entity == null || !entity.IsValid)
            return;

        if (entity.DesignerName != "worldent")
            return;

        _globals.InSwing[AttackerPlayer.PlayerID] = true;
        _service.PlayerSelectSoundtoEntity(AttackerPlayer, zombie.Sounds.HitWallSound, zombie.Stats.ZombieSoundVolume);
    }

    private void Event_OnInGrenadeDamage(IOnEntityTakeDamageEvent @event)
    {
        var victim = @event.Entity;
        if (victim == null || !victim.IsValid)
            return;

        var VictimPawn = victim.As<CCSPlayerPawn>();
        if (VictimPawn == null || !VictimPawn.IsValid)
            return;

        var VictimController = VictimPawn.Controller.Value?.As<CCSPlayerController>();
        if (VictimController == null || !VictimController.IsValid)
            return;

        var VictimPlayer = _core.PlayerManager.GetPlayerFromController(VictimController);
        if (VictimPlayer == null || !VictimPlayer.IsValid)
            return;

        var Id = VictimPlayer.PlayerID;
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
                _service.PlayerSelectSoundtoEntity(VictimPlayer, zombie.Sounds.BurnSound, zombie.Stats.ZombieSoundVolume);
            }
        }
        else if (@event.Info.DamageType == DamageTypes_t.DMG_BLAST)
        {
            _service.PlayerSelectSoundtoEntity(VictimPlayer, zombie.Sounds.ExplodeSound, zombie.Stats.ZombieSoundVolume);
        }
    }

}

