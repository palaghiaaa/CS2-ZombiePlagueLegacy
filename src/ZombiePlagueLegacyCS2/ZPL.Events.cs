#pragma warning disable CS0618 // IOnEntityTakeDamageEvent / IOnWeaponServicesCanUseHookEvent: deprecated by SwiftlyS2 1.4 — migration to GameHooks pending
using System.Numerics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Helpers;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Sounds;
using static ZombiePlagueLegacyCS2.ZPLZombieClassCFG;

namespace ZombiePlagueLegacyCS2;

public partial class ZPLEvents
{
    /// <summary>
    /// Extra seconds to wait before the second fog-application retry on map load.
    /// Provides a longer window for slow-loading or workshop maps whose entity
    /// system may not be ready within the initial 1.5-second delay.
    /// </summary>
    private const float FogSecondRetryDelaySec = 3.0f;

    private readonly ILogger<ZPLEvents> _logger;
    private readonly ISwiftlyCore _core;
    private readonly ZPLGlobals _globals;
    private readonly ZPLServices _service;
    private readonly ZPLCommands _commands;
    private readonly ZPLHelpers _helpers;
    private readonly IOptionsMonitor<ZPLMainCFG> _mainCFG;
    private readonly IOptionsMonitor<ZPLVoxCFG> _voxCFG;
    private readonly IOptionsMonitor<ZPLZombieClassCFG> _zombieClassCFG;
    private readonly IOptionsMonitor<ZPLSpecialClassCFG> _SpecialClassCFG;
    private readonly PlayerZombieState _zombieState;
    private readonly ZPLGameMode _gameMode;

    private readonly ZombiePlagueLegacyAPI _api;
    private readonly ZPLExtraItemsMenu _extraItemsMenu;

    // ── FlashingHtmlHudFix ───────────────────────────────────────────────────
    // Adapted from Ghost23161/FlashingHtmlHudFix (CSS) to SwiftlyS2.
    // Prevents HUD flicker caused by GameRestart flag being out-of-sync
    // with RestartRoundTime after Armory/AG2 CS2 updates.
    private CCSGameRulesProxy? _gameRulesProxy;
    private bool _hudFixRunThisTick;

    private readonly IOptionsMonitor<ZPLExtraItemsCFG> _extraItemsCFG;
    private readonly ZPLWeaponsMenu _weaponsMenu;
    private readonly AmmoPacksService _ammoPacks;
    private readonly ZPLMineService _mineService;
    private readonly IOptionsMonitor<ZPLMineCFG> _mineCFG;
    private readonly ZPLClassAbilities _classAbilities;

    public ZPLEvents(ISwiftlyCore core, ILogger<ZPLEvents> logger
        , ZPLGlobals globals, ZPLServices services,
        ZPLCommands commands, IOptionsMonitor<ZPLMainCFG> mainCFG,
        IOptionsMonitor<ZPLVoxCFG> voxCFG, ZPLHelpers helpers,
        IOptionsMonitor<ZPLZombieClassCFG> zombieClassCFG,
        PlayerZombieState zombieState, ZPLGameMode gameMode,
        IOptionsMonitor<ZPLSpecialClassCFG> specialClassCFG,
        ZombiePlagueLegacyAPI api,
        ZPLExtraItemsMenu extraItemsMenu,
        IOptionsMonitor<ZPLExtraItemsCFG> extraItemsCFG,
        ZPLWeaponsMenu weaponsMenu,
        AmmoPacksService ammoPacks,
        ZPLMineService mineService,
        IOptionsMonitor<ZPLMineCFG> mineCFG,
        ZPLClassAbilities classAbilities)
    {
        _core = core;
        _logger = logger;
        _globals = globals;
        _service = services;
        _commands = commands;
        _mainCFG = mainCFG;
        _voxCFG = voxCFG;
        _helpers = helpers;
        _zombieClassCFG = zombieClassCFG;
        _zombieState = zombieState;
        _gameMode = gameMode;
        _SpecialClassCFG = specialClassCFG;
        _api = api;
        _extraItemsMenu = extraItemsMenu;
        _extraItemsCFG = extraItemsCFG;
        _weaponsMenu = weaponsMenu;
        _ammoPacks = ammoPacks;
        _mineService = mineService;
        _mineCFG = mineCFG;
        _classAbilities = classAbilities;
    }

    public void HookEvents()
    {
        _core.GameEvent.HookPre<EventRoundStart>(OnTimerStart);
        _core.GameEvent.HookPre<EventRoundFreezeEnd>(OnRoundFreezeEnd);
        _core.GameEvent.HookPre<EventRoundEnd>(OnRoundEnd);
        _core.GameEvent.HookPre<EventPlayerDeath>(OnPlayerDeath);
        
        _core.GameEvent.HookPre<EventPlayerHurt>(OnPlayerHurtInfect);
        _core.GameEvent.HookPre<EventPlayerHurt>(OnPlayerHurtZombie);
        _core.GameEvent.HookPre<EventPlayerHurt>(OnPlayerDmgHud);


        _core.Event.OnClientDisconnected += Event_OnClientDisconnected;
        _core.Event.OnClientConnected += Event_OnClientConnected;
        _core.Event.OnClientKeyStateChanged += Event_OnClientKeyStateChangedNemesisFrost;
#pragma warning disable CS0618 // IOnEntityTakeDamageEvent / IOnWeaponServicesCanUseHookEvent: deprecated by SwiftlyS2 1.4, migration pending
        _core.Event.OnEntityTakeDamage += Event_OnEntityTakeDamage;
        _core.Event.OnMapLoad += Event_OnMapLoad;
        _core.Event.OnMapUnload += Event_OnMapUnload;
        _core.Event.OnWeaponServicesCanUseHook += Event_OnWeaponServicesCanUseHook;
#pragma warning restore CS0618
        _core.Event.OnPrecacheResource += Event_OnPrecacheResource;
        _core.Event.OnTick += Event_OnTickSpeed;
        _core.Event.OnTick += Event_OnTickNoRecoil;
        _core.Event.OnTick += Event_OnTickMultijump;
        _core.Event.OnTick += Event_OnTickJetpack;
        _core.Event.OnTick += Event_OnTickLeap;
        _core.Event.OnTick += Event_OnTickNemesisFrost;
        _core.Event.OnTick += Event_OnTickParachute;
        _core.Event.OnTick += Event_OnTickHudFix;

        _core.GameEvent.HookPre<EventWeaponFire>(OnHumanWeaponFire);

        _core.GameEvent.HookPre<EventPlayerDeath>(CheckRoundWinDeath);

        _core.GameEvent.HookPre<EventPlayerSpawn>(OnPlayerSpawn);
        _core.GameEvent.HookPre<EventPlayerSpawn>(CheckRoundWinSpawn);
        _core.GameEvent.HookPre<EventPlayerSpawn>(RandomSpawn);


        _core.GameEvent.HookPre<EventGrenadeThrown>(OnGrenadeThrown);
        _core.GameEvent.HookPre<EventHegrenadeDetonate>(OnGrenadeDetonate);

        _core.GameEvent.HookPre<EventPlayerBlind>(OnPlayerBlind);
        _core.GameEvent.HookPre<EventFlashbangDetonate>(OnFlashbangDetonate);
        _core.GameEvent.HookPre<EventPlayerFootstep>(OnPlayerFootstep);

        _core.GameEvent.HookPre<EventSmokegrenadeDetonate>(OnSmokegrenadeDetonate);

        _core.GameEvent.HookPre<EventDecoyFiring>(OnDecoyFiring);

        _core.Event.OnEntityCreated += Event_OnEntityCreated;
    }

    /// <summary>
    /// Removes all GameEvent.HookPre registrations and all Core.Event delegate
    /// subscriptions that were added in <see cref="HookEvents"/>. Must be called
    /// from <see cref="ZombiePlagueLegacyCS2.Unload"/> so that stale handlers
    /// from the previous load do not accumulate on hot-reload (map change) and
    /// cause double event processing or memory leaks.
    /// </summary>
    public void UnhookEvents()
    {
        // IGameEventService.UnhookPre<T>() removes all HookPre registrations
        // for event type T that were made by this plugin context.
        // Covers hooks from both HookEvents and HookZombieSoundEvents.
        _core.GameEvent.UnhookPre<EventRoundStart>();
        _core.GameEvent.UnhookPre<EventRoundFreezeEnd>();
        _core.GameEvent.UnhookPre<EventRoundEnd>();
        _core.GameEvent.UnhookPre<EventPlayerDeath>();
        _core.GameEvent.UnhookPre<EventPlayerHurt>();
        _core.GameEvent.UnhookPre<EventWeaponFire>();
        _core.GameEvent.UnhookPre<EventPlayerSpawn>();
        _core.GameEvent.UnhookPre<EventGrenadeThrown>();
        _core.GameEvent.UnhookPre<EventHegrenadeDetonate>();
        _core.GameEvent.UnhookPre<EventPlayerBlind>();
        _core.GameEvent.UnhookPre<EventFlashbangDetonate>();
        _core.GameEvent.UnhookPre<EventPlayerFootstep>();
        _core.GameEvent.UnhookPre<EventSmokegrenadeDetonate>();
        _core.GameEvent.UnhookPre<EventDecoyFiring>();

        // Remove C# multicast delegate subscriptions from HookEvents.
        _core.Event.OnClientDisconnected         -= Event_OnClientDisconnected;
        _core.Event.OnClientConnected            -= Event_OnClientConnected;
        _core.Event.OnClientKeyStateChanged      -= Event_OnClientKeyStateChangedNemesisFrost;
#pragma warning disable CS0618
        _core.Event.OnEntityTakeDamage           -= Event_OnEntityTakeDamage;
        _core.Event.OnMapLoad                    -= Event_OnMapLoad;
        _core.Event.OnMapUnload                  -= Event_OnMapUnload;
        _core.Event.OnWeaponServicesCanUseHook   -= Event_OnWeaponServicesCanUseHook;
#pragma warning restore CS0618
        _core.Event.OnPrecacheResource           -= Event_OnPrecacheResource;
        _core.Event.OnTick                       -= Event_OnTickSpeed;
        _core.Event.OnTick                       -= Event_OnTickNoRecoil;
        _core.Event.OnTick                       -= Event_OnTickMultijump;
        _core.Event.OnTick                       -= Event_OnTickJetpack;
        _core.Event.OnTick                       -= Event_OnTickLeap;
        _core.Event.OnTick                       -= Event_OnTickNemesisFrost;
        _core.Event.OnTick                       -= Event_OnTickParachute;
        _core.Event.OnTick                       -= Event_OnTickHudFix;
        _core.Event.OnEntityCreated              -= Event_OnEntityCreated;
    }

    private void Event_OnEntityCreated(IOnEntityCreatedEvent @event)
    {
        var entity = @event.Entity;
        if (entity == null || !entity.IsValid || !entity.IsValidEntity)
            return;

        if (!entity.DesignerName.Contains("_projectile"))
            return;

        _core.Scheduler.NextTick(() =>
        {
            if (entity.IsValid && entity.IsValidEntity)
            {
                _helpers.CheckGrenadeSpawned(entity);
            }
        });
    }

    private HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event)
    {

        try
        {
            if (!_globals.SafeRoundStart)
                return HookResult.Continue;

            _globals.SafeRoundStart = false;

            // Fog reliability pass: re-apply fog if the controller is no longer
            // valid. This handles the case where the OnMapLoad scheduler callbacks
            // fire before the entity system is fully ready (common after a map
            // change), leaving the server without a fog controller.
            var fogCfg = _mainCFG.CurrentValue.Fog;
            if (fogCfg.Enable && !_globals.GlobalFogController.IsValid)
                _helpers.ApplyFog(fogCfg);

            _helpers.SwitchAllPlayerTeam();
            _commands.RoundCvar();
            _helpers.BuildSpawnCache();
            _helpers.RemoveHostage();

            var playerCount = _helpers.ServerPlayerCount();
            if (playerCount <= 0)
            {
                _globals.ServerIsEmpty = true;
                return HookResult.Continue;
            }
            _globals.ServerIsEmpty = false;

            var CFG = _mainCFG.CurrentValue;
            var VoxCFG = _voxCFG.CurrentValue;
            var VoxList = VoxCFG.VoxList;

            _helpers.SetAllDefaultModel(CFG);

            //_logger.LogInformation("开始选择游戏模式...");
            var selectedMode = _gameMode.PickRandomMode();
            //_logger.LogInformation($"当前模式: {_gameMode.GetModeName()}");

            if (_api != null)
                _api.NotifyGameModeSelect(_gameMode.GetModeName());

            _globals.IsheroSetup = false;
            _globals.GameInfiniteClipMode = false;
            _service.CheckEndTimer();
            if (_globals.RoundVoxGroup == null && VoxList != null)
            {
                _globals.RoundVoxGroup = _helpers.PickRandomActiveGroup(VoxList);
            }

            // Not enough real players to run an infection round.
            // Cancel any stale countdown and let the natural round timer expire.
            if (playerCount < CFG.MinPlayersForInfection)
            {
                _globals.g_hCountdown?.Cancel();
                _globals.g_hCountdown = null;
                _helpers.SendCenterHTMLLocalizedToAll(p =>
                    $"<b><span color='#AAAAAA'>{_helpers.T(p, "ServerGameWaitingForPlayers")}</span></b>",
                    duration: 1100);
                return HookResult.Continue;
            }

            if (CFG.RoundReadyTime > 0)
            {
                //_logger.LogInformation($"开始倒计时: {CFG.RoundReadyTime}秒");
                _globals.Countdown = (int)Math.Ceiling(CFG.RoundReadyTime);

                if (_globals.GameStart)
                    return HookResult.Continue;

                // Auto-open weapons menu pre-infection (buy phase)
                _weaponsMenu.ShowPrimaryMenuToAllEligible();

                if (_globals.RoundVoxGroup != null)
                {
                    //_logger.LogInformation($"播放背景音乐: {_globals.RoundVoxGroup.RoundMusicVox}");
                    _service.PlayerSelectSoundtoAll(_globals.RoundVoxGroup.RoundMusicVox, _globals.RoundVoxGroup.Volume);
                }

                _globals.g_hCountdown?.Cancel();
                _globals.g_hCountdown = null;
                _globals.g_hCountdown = _core.Scheduler.DelayAndRepeatBySeconds(0.1f, 1.0f, () => _service.Round_Countdown());
                _core.Scheduler.StopOnMapChange(_globals.g_hCountdown);

            }
            else
            {
                _globals.Countdown = 3;

                if (_globals.GameStart)
                    return HookResult.Continue;

                // Auto-open weapons menu pre-infection (buy phase)
                _weaponsMenu.ShowPrimaryMenuToAllEligible();

                if (_globals.RoundVoxGroup != null)
                {
                    //_logger.LogInformation($"播放背景音乐: {_globals.RoundVoxGroup.RoundMusicVox}");
                    _service.PlayerSelectSoundtoAll(_globals.RoundVoxGroup.RoundMusicVox, _globals.RoundVoxGroup.Volume);
                }

                _globals.g_hCountdown?.Cancel();
                _globals.g_hCountdown = null;
                _globals.g_hCountdown = _core.Scheduler.DelayAndRepeatBySeconds(0.1f, 1.0f, () => _service.Round_Countdown());
                _core.Scheduler.StopOnMapChange(_globals.g_hCountdown);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"OnRoundStart ERROR: {ex.Message}");
            _logger.LogError($"ERROR: {ex.StackTrace}");

            if (_globals.RoundVoxGroup != null)
            {
                _logger.LogError($"RoundMusicVox: {_globals.RoundVoxGroup.RoundMusicVox}");
                _logger.LogError($"Volume: {_globals.RoundVoxGroup.Volume}");
            }

            return HookResult.Continue;
        }

        return HookResult.Continue;
    }

    private HookResult OnTimerStart(EventRoundStart @event)
    {
        _mineService.CleanupAllMines();
        _service.SetRoundEndTime();
        _globals.SafeRoundStart = true;
        _globals.InfectionStartedThisRound = false;
        _globals.AdminForcedModeThisRound = false;
        _globals.MotherZombieWasSelected = false;
        _gameMode.ResetMode();

        // Rotate anti-repeat tracking: this round's selections become "last round"
        // so that PickPreferredPlayer / SelectMotherZombie can deprioritise them.
        _globals.SpecialRoleLastRound.Clear();
        foreach (var sid in _globals.SpecialRoleThisRound)
            _globals.SpecialRoleLastRound.Add(sid);
        _globals.SpecialRoleThisRound.Clear();

        var CFG = _mainCFG.CurrentValue;
        float configDist = CFG.Assassin.InvisibilityDist;
        _core.Scheduler.DelayBySeconds(1.0f, () =>
        {
            _service.GlobalIdleTimer();
            _service.ZombieRegenTimer();
            _service.StartActivePlayerRewardTimer();
            _service.StartAssassinInvisibilityTimer(configDist);
        });

        // Round start chat announcement
        int playerCount = _helpers.ServerPlayerCount() ?? 0;
        foreach (var player in _core.PlayerManager.GetAllPlayers())
        {
            if (!player.IsValid || player.IsFakeClient) continue;
            int ap = _extraItemsMenu.GetAmmoPacks(player.PlayerID);
            _helpers.SendChatT(player, "RoundStartAnnounce", ap, playerCount);
        }

        // Reliability pass: ensure round-start weapon menu opens during pre-infection phase.
        _core.Scheduler.DelayBySeconds(0.3f, () =>
        {
            if (_globals.GameStart || _globals.InfectionStartedThisRound)
                return;
            _weaponsMenu.ShowPrimaryMenuToAllEligible();
        });

        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event)
    {
        // Show winner center message based on event winner team
        int winner = @event.Winner;
        if (winner == (int)Team.CT)
            _helpers.SendCenterHTMLLocalizedToAll(p =>
                $"<b><span color='#00FF7F' class='fontSize-xl'>{_helpers.T(p, "ServerGameHumanWin")}</span></b>",
                duration: 4000);
        else if (winner == (int)Team.T)
            _helpers.SendCenterHTMLLocalizedToAll(p =>
                $"<b><span color='#FF3030' class='fontSize-xl'>{_helpers.T(p, "ServerGameZombieWin")}</span></b>",
                duration: 4000);
        
        _helpers.ClearAllBurns();
        _helpers.ClearAllLights();
        _mineService.CleanupAllMines();
        _globals.GameInfiniteClipMode = false;
        _core.Scheduler.DelayBySeconds(2.0f, () =>
        {
            _globals.RoundVoxGroup = null;
            _globals.GameStart = false;
            _globals.g_hRoundEndTimer?.Cancel();
            _globals.g_hRoundEndTimer = null;
            var allplayer = _core.PlayerManager.GetAllPlayers();
            foreach (var player in allplayer)
            {
                if (!player.IsValid)
                    continue;

                _helpers.RemoveGlow(player);

                var id = player.PlayerID;
                bool wasZombie = false;
                _globals.IsZombie.TryGetValue(id, out wasZombie);

                _globals.IsZombie[id] = false;
                _globals.IsSurvivor[id] = false;
                _globals.IsAssassin[id] = false;
                _globals.IsSniper[id] = false;
                _globals.IsNemesis[id] = false;
                _globals.IsHero[id] = false;

                _globals.GodState[id] = false;

                // ── Per-round resets (consumables / temporary) ──────────────────
                // These do NOT persist between rounds (consumed on use or round-specific)
                _globals.ZombieMadnessActive.Remove(id);  // 10s invuln — expires each round
                _globals.HasReviveToken.Remove(id);        // consumed on death
                _globals.ScbaSuit[id] = false;             // consumed on infection
                _globals.SpawnProtectionEndTime.Remove(id);
                _globals.LeapCooldownEnd.Remove(id);
                _globals.ItemPurchaseCount.Remove(id);     // per-round purchase limits
                _globals.DamageAccumulator.Remove(id);
                _globals.PrevJumpPressed.Remove(id);
                _globals.PrevOnGround.Remove(id);
                _globals.JumpsUsed.Remove(id);
                _globals.KnifeBlinkCooldownEnd.Remove(id);

                // ── Persistent items (kept between rounds — CS 1.6 behaviour) ─────
                // Multijump: keep the purchased jump count (already stored in ExtraJumps)
                // JumpsUsed already cleared above — ExtraJumps count carries over unchanged
                // Knife Blink: restore charges if player owns blink
                if (_globals.KnifeBlinkCharges.ContainsKey(id))
                    _globals.KnifeBlinkCharges[id] = _extraItemsCFG.CurrentValue.KnifeBlinkCharges;
                // Jetpack: keep ownership, refill fuel
                if (_globals.HasJetpack.TryGetValue(id, out bool hasJp) && hasJp)
                    _globals.JetpackFuel[id] = _extraItemsCFG.CurrentValue.JetpackMaxFuel;
                // InfiniteClip, NoRecoil, Tryder, Parachute — ownership persists, no reset needed
                if (!wasZombie && player.Controller != null && player.Controller.IsValid && player.Controller.PawnIsAlive)
                {
                    int reward = _extraItemsCFG.CurrentValue.RoundSurviveReward;
                    if (reward > 0)
                    {
                        _extraItemsMenu.AddAmmoPacks(id, reward);
                        _helpers.SendStackedCenterHTML(
                            player,
                            "reward",
                            $"<span color=\"#FFD700\" class=\"fontSize-l fontWeight-bold\">{_helpers.T(player, "APRoundSurviveReward", reward, _extraItemsMenu.GetAmmoPacks(id))}</span>",
                            2500,
                            priority: 80);
                    }
                }

                _globals.CanBuyWeaponsThisRound.Remove(id);

                _service.StopAssassinTimer();
                _helpers.SetUnInvisibility(player);

                player.SwitchTeam(Team.CT);
                _helpers.ChangeKnife(player, false, false);
                _helpers.SetFov(player, 90);

            }
        });
        
        return HookResult.Continue;
    }
    private void Event_OnPrecacheResource(IOnPrecacheResourceEvent @event)
    {
        @event.AddItem("characters/models/ctm_st6/ctm_st6_variante.vmdl");
        @event.AddItem("particles/burning_fx/env_fire_large.vpcf");
        @event.AddItem("soundevents/game_sounds_physics.vsndevts");
        @event.AddItem("soundevents/game_sounds_weapons.vsndevts");
        @event.AddItem("soundevents/game_sounds_player.vsndevts");

        @event.AddItem("particles/ui/hud/ui_map_def_utility_trail.vpcf");
        @event.AddItem("particles/burning_fx/barrel_burning_trail.vpcf");
        @event.AddItem("particles/environment/de_train/train_coal_dump_trails.vpcf");

        @event.AddItem("particles/explosions_fx/explosion_hegrenade_water_intial_trail.vpcf");
        @event.AddItem("particles/survival_fx/danger_trail_spores_world.vpcf");

        var CFG = _mainCFG.CurrentValue;

        // Precache all configured mine models and sound events
        foreach (var mine in _mineCFG.CurrentValue.MineList)
        {
            if (!string.IsNullOrWhiteSpace(mine.Model))
                @event.AddItem(mine.Model);
            if (!string.IsNullOrWhiteSpace(mine.PrecacheSoundEvent))
                @event.AddItem(mine.PrecacheSoundEvent);
        }

        var ambsound = CFG.PrecacheAmbSound;
        if (!string.IsNullOrEmpty(ambsound))
        {
            var ambsoundList = ambsound
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s));

            foreach (var ambsounds in ambsoundList)
            {
                @event.AddItem(ambsounds);
            }
        }

        var VoxCFG = _voxCFG.CurrentValue;
        var VoxList = VoxCFG.VoxList;
        foreach (var vox in VoxList)
        {
            if (!string.IsNullOrEmpty(vox.PrecacheSoundEvent))
            {
                @event.AddItem(vox.PrecacheSoundEvent);
            }     
        }
        var zombieConfig = _zombieClassCFG.CurrentValue;
        var zombieList = zombieConfig.ZombieClassList;
        foreach (var sounds in zombieList)
        {
            if (!string.IsNullOrEmpty(sounds.PrecacheSoundEvent))
            {
                var soundList = sounds.PrecacheSoundEvent
                    .Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s));

                foreach (var sound in soundList)
                {
                    @event.AddItem(sound);
                }
            }
        }
        foreach (var models in zombieList)
        {
            if (!string.IsNullOrEmpty(models.Models.ModelPath))
            {
                @event.AddItem(models.Models.ModelPath);
            }
            if (!string.IsNullOrEmpty(models.Models.CustomKinfeModelPath))
            {
                @event.AddItem(models.Models.CustomKinfeModelPath);
            }
        }

        var Survivormodel = CFG.Survivor.ModelsPath;
        if (!string.IsNullOrEmpty(Survivormodel))
        {
            @event.AddItem(Survivormodel);
        }
        var Snipermodel = CFG.Sniper.ModelsPath;
        if (!string.IsNullOrEmpty(Snipermodel))
        {
            @event.AddItem(Snipermodel);
        }
        var Heromodel = CFG.Hero.ModelsPath;
        if (!string.IsNullOrEmpty(Heromodel))
        {
            @event.AddItem(Heromodel);
        }

        var HumanDefaultModel = CFG.HumandefaultModel;
        if (!string.IsNullOrEmpty(HumanDefaultModel))
        {
            @event.AddItem(HumanDefaultModel);
        }

        var SpecialzombieConfig = _SpecialClassCFG.CurrentValue;
        var SpecialzombieList = SpecialzombieConfig.SpecialClassList;
        foreach (var Specialsounds in SpecialzombieList)
        {
            if (!string.IsNullOrEmpty(Specialsounds.PrecacheSoundEvent))
            {
                var SpecialsoundList = Specialsounds.PrecacheSoundEvent
                    .Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s));

                foreach (var Specialsound in SpecialsoundList)
                {
                    @event.AddItem(Specialsound);
                }
            }
        }
        foreach (var Specialmodels in SpecialzombieList)
        {
            if (!string.IsNullOrEmpty(Specialmodels.Models.ModelPath))
            {
                @event.AddItem(Specialmodels.Models.ModelPath);
            }
            if (!string.IsNullOrEmpty(Specialmodels.Models.CustomKinfeModelPath))
            {
                @event.AddItem(Specialmodels.Models.CustomKinfeModelPath);
            }
        }


    }
    
    private HookResult OnPlayerSpawn(EventPlayerSpawn @event)
    {
        try
        {
            var player = @event.UserIdPlayer;
            if (player == null || !player.IsValid)
                return HookResult.Continue;

            var pawn = @event.UserIdPawn;
            if (pawn == null || !pawn.IsValid)
                return HookResult.Continue;

            var controller = @event.UserIdController;
            if (controller == null || !controller.IsValid)
                return HookResult.Continue;

            var Id = player.PlayerID;
            ulong steamId = player.SteamID;

            _core.Scheduler.NextWorldUpdate(() =>
            {
                try
                {
                    if (player == null || !player.IsValid)
                        return;

                    _helpers.SetNoBlock(player);
                    _helpers.ApplyFogToPlayer(player);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"SetNoBlock Error [{controller.PlayerName}]: {ex.Message}");
                }
            });

            _globals.IsZombie.TryGetValue(Id, out bool IsZombie);

            // Apply spawn protection: make the player temporarily invulnerable so
            // they cannot be killed the instant they appear (mirrors zp_spawn_protection).
            float protectionTime = _mainCFG.CurrentValue.SpawnProtectionTime;
            if (protectionTime > 0)
            {
                _globals.SpawnProtectionEndTime[Id] =
                    Environment.TickCount64 + (long)(protectionTime * 1000);
            }

            if (IsZombie)
            {
                _core.Scheduler.NextWorldUpdate(() =>
                {
                    try
                    {
                        //_logger.LogInformation($"玩家 [{controller.PlayerName}] 开始应用僵尸类...");

                        var zombieConfig = _zombieClassCFG.CurrentValue;
                        var zombieClasses = zombieConfig.ZombieClassList;
                        var specialConfig = _SpecialClassCFG.CurrentValue;

                        var preference = _zombieState.GetPlayerPreference(Id, steamId);

                        ZombieClass? zombie = null;

                        if (preference != null)
                        {
                            if (preference.Preference == ZombiePreference.Fixed)
                            {
                                zombie = zombieClasses.FirstOrDefault(c => c.Name == preference.FixedZombieName);
                                //_logger.LogInformation($"固定僵尸类: {zombie?.Name}");
                            }
                            else
                            {
                                zombie = _zombieState.PickRandomZombieClass(zombieClasses);
                                //_logger.LogInformation($"随机僵尸类: {zombie?.Name}");
                            }
                        }
                        else
                        {
                            zombie = _zombieState.GetZombieClass(Id, zombieConfig.ZombieClassList, specialConfig.SpecialClassList);
                            if (zombie == null)
                            {
                                zombie = _zombieState.PickRandomZombieClass(zombieClasses);
                                //_logger.LogInformation($"备用随机僵尸类: {zombie?.Name}");
                            }
                        }

                        if (zombie != null)
                        {
                            //_logger.LogInformation($"调用 posszombie: {zombie.Name}, 模型: {zombie.Models}");
                            _service.posszombie(player, zombie, false);
                            //_logger.LogInformation($"posszombie 完成");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"OnPlayerSpawn zombie Class Error [{controller.PlayerName}]: {ex.Message}");
                        _logger.LogError($"Error: {ex.StackTrace}");
                    }
                });
            }
            else
            {
                var CFG = _mainCFG.CurrentValue;
                _core.Scheduler.NextWorldUpdate(() =>
                {
                    if (player == null || !player.IsValid) return;
                    var currentPawn = player.PlayerPawn;
                    if (currentPawn == null || !currentPawn.IsValid) return;
                    currentPawn.MaxHealth = CFG.HumanMaxHealth;
                    currentPawn.MaxHealthUpdated();
                    currentPawn.Health = CFG.HumanMaxHealth;
                    currentPawn.HealthUpdated();

                    currentPawn.ActualGravityScale = CFG.HumanInitialGravity;

                    _service.GiveSpawnGrenade(player, CFG);

                    // ── Re-apply persistent extra items on new pawn ──────────────
                    var extraCFG = _extraItemsCFG.CurrentValue;

                    // Tryder: restore HP bonus + armor
                    if (_globals.TryderState.TryGetValue(Id, out bool hasTryder) && hasTryder)
                    {
                        currentPawn.MaxHealth = CFG.HumanMaxHealth + extraCFG.TryderHealth;
                        currentPawn.MaxHealthUpdated();
                        currentPawn.Health = CFG.HumanMaxHealth + extraCFG.TryderHealth;
                        currentPawn.HealthUpdated();
                        currentPawn.ArmorValue = extraCFG.TryderArmor;
                        currentPawn.ArmorValueUpdated();
                        _helpers.SetGlow(player,
                            extraCFG.TryderGlowR, extraCFG.TryderGlowG, extraCFG.TryderGlowB, 200);
                    }

                    // InfiniteClip / NoRecoil: flags are re-checked per-shot in tick handlers
                    // — no pawn action needed, the dict flags survive round transition.

                    // Jetpack: fuel was refilled at round-end, ownership in HasJetpack persists.
                    // No pawn action needed — thrust tick checks HasJetpack dict.
                });

                
            }

            return HookResult.Continue;
        }
        catch (Exception ex)
        {
            _logger.LogError($"OnPlayerSpawn ERROR: {ex.Message}");
            _logger.LogError($"ERROR: {ex.StackTrace}");
            return HookResult.Continue;
        }
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event)
    {
        var player = @event.UserIdPlayer;
        if(player == null || !player.IsValid)
            return HookResult.Continue;

        var Pawn = player.PlayerPawn;
        if (Pawn == null || !Pawn.IsValid)
            return HookResult.Continue;

        var Controller = player.Controller;
        if (Controller == null || !Controller.IsValid)
            return HookResult.Continue;


        _helpers.SetFov(player, 90);
        _helpers.RemoveGlow(player);

        var Id = player.PlayerID;
        var steamId = player.SteamID;
        _helpers.ClearStackedCenterHTML(Id);

        _helpers.ClearPlayerBurn(Id);
        _helpers.RemoveSHumanClass(Id);
        _helpers.RemoveSZombieClass(Id);

        _globals.IsMother.Remove(Id);
        _globals.ScbaSuit.Remove(Id);
        _globals.GodState.Remove(Id);
        _globals.InfiniteAmmoState.Remove(Id);
        _globals.SpawnProtectionEndTime.Remove(Id);
        _globals.LeapCooldownEnd.Remove(Id);

        // Extra items: reset per-death state for the victim
        _globals.ExtraJumps.Remove(Id);
        // Class abilities cleanup
        _classAbilities.ClearPlayer(Id);
        _globals.JumpsUsed.Remove(Id);
        _globals.KnifeBlinkCharges.Remove(Id);
        _globals.KnifeBlinkCooldownEnd.Remove(Id);
        _globals.ZombieMadnessActive.Remove(Id);
        _globals.PrevJumpPressed.Remove(Id);
        _globals.InfiniteClipState.Remove(Id);
        _globals.ExtraNoRecoilState.Remove(Id);
        _globals.TryderState.Remove(Id);
        // Jetpack, parachute and mines cleanup on death
        _extraItemsMenu.CleanupJetpack(Id);
        if (_globals.HasParachute.Remove(Id))
        {
            // Restore gravity in case player dies while parachuting
            var deathPawn = player.PlayerPawn;
            if (deathPawn != null && deathPawn.IsValid)
                RestoreParachuteGravity(Id, deathPawn);
            else
                _globals.ParachuteRestoreGravity.Remove(Id);
        }
        _globals.NemesisFrostCharges.Remove(Id);
        _globals.NemesisFrostCooldown.Remove(Id);
        _mineService.CleanupMinesForPlayer(steamId);
        // (HasReviveToken is intentionally kept here – handled below)

        if (!_globals.GameStart)
            return HookResult.Continue;

        // Award AP to zombie attacker for killing a human
        _globals.IsZombie.TryGetValue(Id, out bool victimIsZombie);
        if (!victimIsZombie)
        {
            var attacker = @event.AttackerPlayer;
            if (attacker != null && attacker.IsValid)
            {
                var aId = attacker.PlayerID;
                _globals.IsZombie.TryGetValue(aId, out bool attackerIsZombie);
                if (attackerIsZombie)
                {
                    var mainCfg = _mainCFG.CurrentValue;
                    if (mainCfg.EnableZombieKillFeedOverride)
                    {
                        @event.Weapon = string.IsNullOrWhiteSpace(mainCfg.ZombieKillFeedWeapon)
                            ? "knife"
                            : mainCfg.ZombieKillFeedWeapon.Trim();
                        @event.Headshot = false;
                    }

                    int reward = _extraItemsCFG.CurrentValue.ZombieKillReward;
                    if (reward > 0)
                    {
                        _extraItemsMenu.AddAmmoPacks(aId, reward);
                        _helpers.SendStackedCenterHTML(
                            attacker,
                            "reward",
                            $"<span color=\"#FF8C00\" class=\"fontSize-l fontWeight-bold\">{_helpers.T(attacker, "APZombieKillReward", reward, _extraItemsMenu.GetAmmoPacks(aId))}</span>",
                            2500,
                            priority: 80);
                    }
                }
            }
        }

        _globals.IsZombie.TryGetValue(Id, out bool IsZombie);
        float respawnDelay = _gameMode.GetRespawnDelay();
        if (IsZombie && _gameMode.CanZombieReborn())
        {

            var zombieClasses = _zombieClassCFG.CurrentValue.ZombieClassList;
            var specialClasses = _SpecialClassCFG.CurrentValue.SpecialClassList;
            _core.Scheduler.DelayBySeconds(respawnDelay, () =>
            {
                if (!_globals.GameStart) return;
                var p = _core.PlayerManager.GetPlayer(Id);
                if (p == null || !p.IsValid) return;
                _zombieState.ClearSpecialAndSetPlayerZombie(p, zombieClasses, specialClasses);
                p.Respawn();
            });
        }
        if (!IsZombie && _gameMode.CanZombieReborn())
        {
            // ── Revive Token check ──────────────────────────────────────────
            _globals.HasReviveToken.TryGetValue(Id, out bool hasReviveToken);
            var mode = _gameMode.CurrentMode;
            bool reviveModeAllowed = mode == GameModeType.Normal
                                  || mode == GameModeType.NormalInfection
                                  || mode == GameModeType.MultiInfection
                                  || mode == GameModeType.Hero;

            if (hasReviveToken && reviveModeAllowed)
            {
                _globals.HasReviveToken[Id] = false; // consume token
                float reviveDelay = _extraItemsCFG.CurrentValue.ReviveTokenRespawnDelay;
                _core.Scheduler.DelayBySeconds(reviveDelay, () =>
                {
                    var p = _core.PlayerManager.GetPlayer(Id);
                    if (p == null || !p.IsValid) return;
                    if (!_globals.GameStart) return;

                    _globals.IsZombie[Id] = false;
                    p.Respawn();

                    _core.Scheduler.NextWorldUpdate(() =>
                    {
                        var p2 = _core.PlayerManager.GetPlayer(Id);
                        if (p2 == null || !p2.IsValid) return;

                        var pawn = p2.PlayerPawn;
                        if (pawn == null || !pawn.IsValid) return;

                        var cfg = _mainCFG.CurrentValue;
                        pawn.MaxHealth = cfg.HumanMaxHealth;
                        pawn.MaxHealthUpdated();
                        pawn.Health = cfg.HumanMaxHealth;
                        pawn.HealthUpdated();

                        string defaultModel = string.IsNullOrEmpty(cfg.HumandefaultModel)
                            ? "characters/models/ctm_st6/ctm_st6_variante.vmdl"
                            : cfg.HumandefaultModel;
                        pawn.SetModel(defaultModel);
                    });

                    _helpers.SendChatT(p, "ReviveTokenUsed");
                });
                return HookResult.Continue;
            }
            // ── End Revive Token ────────────────────────────────────────────

            // ── Human auto-respawn (CS 1.6: zp_respawn_humans) ──────────────
            if (_gameMode.CanHumanReborn())
            {
                _core.Scheduler.DelayBySeconds(respawnDelay, () =>
                {
                    var p = _core.PlayerManager.GetPlayer(Id);
                    if (p == null || !p.IsValid || !_globals.GameStart) return;
                    // Respawn as zombie (infection has started — can't be human again)
                    var zombieClasses = _zombieClassCFG.CurrentValue.ZombieClassList;
                    var specialClasses = _SpecialClassCFG.CurrentValue.SpecialClassList;
                    _zombieState.ClearSpecialAndSetPlayerZombie(p, zombieClasses, specialClasses);
                    p.Respawn();
                    _helpers.SendChatT(p, "RespawnedAsZombie");
                });
                return HookResult.Continue;
            }

            _core.Scheduler.DelayBySeconds(1.0f, () =>
            {
                var player = _core.PlayerManager.GetPlayer(Id);
                if (player == null || !player.IsValid)
                    return;

                if (!_globals.GameStart) return;

                player.Respawn();

                _core.Scheduler.NextWorldUpdate(() =>
                {
                    if (player == null || !player.IsValid)
                        return;

                    var zombieConfig = _zombieClassCFG.CurrentValue;
                    var zombieClasses = zombieConfig.ZombieClassList;
                    var preference = _zombieState.GetPlayerPreference(Id, steamId);
                    ZombieClass? selectedClass;

                    if (preference != null && preference.Preference == ZombiePreference.Fixed)
                    {
                        selectedClass = zombieClasses.FirstOrDefault(c => c.Name == preference.FixedZombieName);
                    }
                    else
                    {
                        selectedClass = _zombieState.PickRandomZombieClass(zombieClasses);
                    }

                    if (selectedClass != null)
                    {
                        _service.posszombie(player, selectedClass, false);
                        _service.PlayerSelectSoundtoAll(selectedClass.Sounds.SoundInfect, selectedClass.Stats.ZombieSoundVolume);
                    }
                });

            });
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerHurtInfect(EventPlayerHurt @event)
    {
        var mode = _gameMode.CurrentMode;
        if (mode != GameModeType.Normal && mode != GameModeType.NormalInfection && mode != GameModeType.MultiInfection
            && mode != GameModeType.Hero)
            return HookResult.Continue;

        var victim = @event.UserIdPlayer;
        if (victim == null || !victim.IsValid)
            return HookResult.Continue;

        var attacker = @event.AttackerPlayer;
        if (attacker == null || !attacker.IsValid)
            return HookResult.Continue;

        var vId = victim.PlayerID;
        var aId = attacker.PlayerID;

        var CFG = _mainCFG.CurrentValue;

        int Dmg = @event.ActualDmgHealth;
        string waepon = @event.Weapon;
        _globals.IsZombie.TryGetValue(aId, out bool attackerIsZombie);
        _globals.IsZombie.TryGetValue(vId, out bool victimIsZombie);
        _globals.GodState.TryGetValue(vId, out bool IsGodState);
        if (attackerIsZombie && !victimIsZombie)
        {
            _globals.IsHero.TryGetValue(vId, out bool victimIsIsHero);
            if (victimIsIsHero)
                return HookResult.Continue;

            if (waepon != "knife")
                return HookResult.Continue;

            if(IsGodState)
                return HookResult.Continue;

            _service.Infect(attacker, victim, false);
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerHurtZombie(EventPlayerHurt @event)
    {
        var victim = @event.UserIdPlayer;
        if(victim == null || !victim.IsValid)
            return HookResult.Continue;

        var attacker = @event.AttackerPlayer;
        if (attacker == null || !attacker.IsValid)
            return HookResult.Continue;

        var vId = victim.PlayerID;
        var aId = attacker.PlayerID;
        _globals.IsZombie.TryGetValue(aId, out bool attackerIsZombie);
        _globals.IsZombie.TryGetValue(vId, out bool victimIsZombie);
        _globals.IsAssassin.TryGetValue(vId, out bool victimIsAssassin);
        if (!attackerIsZombie && victimIsZombie && victimIsAssassin)
        {
            _helpers.SetUnInvisibility(victim);
            _globals.g_IsInvisible[vId] = false;
        }

        // Damage-based ammo pack reward (CS 1.6-style: 500 dmg dealt = +1 AP)
        if (!attackerIsZombie && victimIsZombie && _globals.GameStart)
        {
            var cfg = _extraItemsCFG.CurrentValue;
            int threshold = cfg.HumanDamageRewardThreshold;
            int rewardPerThreshold = cfg.HumanDamageReward;

            if (threshold > 0 && rewardPerThreshold > 0)
            {
                int dmg = @event.ActualDmgHealth;
                _globals.DamageAccumulator.TryGetValue(aId, out int accumulated);
                accumulated += dmg;

                int packs = accumulated / threshold;
                if (packs > 0)
                {
                    accumulated -= packs * threshold;
                    int totalReward = packs * rewardPerThreshold;
                    _extraItemsMenu.AddAmmoPacks(aId, totalReward);
                    _helpers.SendStackedCenterHTML(
                        attacker,
                        "reward",
                        $"<span color=\"#FF8C00\" class=\"fontSize-l fontWeight-bold\">{_helpers.T(attacker, "APHumanDamageReward", totalReward, _extraItemsMenu.GetAmmoPacks(aId))}</span>",
                        2500,
                        priority: 80);
                }
                _globals.DamageAccumulator[aId] = accumulated;
            }
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerDmgHud(EventPlayerHurt @event)
    {
        var victim = @event.UserIdPlayer;
        if (victim == null || !victim.IsValid)
            return HookResult.Continue;

        var attacker = @event.AttackerPlayer;
        if (attacker == null || !attacker.IsValid)
            return HookResult.Continue;

        var vId = victim.PlayerID;
        var aId = attacker.PlayerID;

        var CFG = _mainCFG.CurrentValue;

        int Dmg = @event.ActualDmgHealth;
        _globals.IsZombie.TryGetValue(aId, out bool attackerIsZombie);
        _globals.IsZombie.TryGetValue(vId, out bool victimIsZombie);

        if (!attackerIsZombie && victimIsZombie && CFG.EnableDamageHud)
        {
            _service.ShowDmgHud(attacker, victim, Dmg);
        }
        return HookResult.Continue;



    }

#pragma warning disable CS0618
    public void Event_OnWeaponServicesCanUseHook(IOnWeaponServicesCanUseHookEvent @event)
    {
        var weapon = @event.Weapon;
        var weaponName = weapon?.Entity?.DesignerName; 
        var customname = weapon?.AttributeManager.Item.CustomName;

        var pawn = @event.WeaponServices.Pawn;
        if (pawn == null || !pawn.IsValid) return;

        var controller = pawn.Controller.Value?.As<CCSPlayerController>();
        if (controller == null || !controller.IsValid) return;

        var Player = _core.PlayerManager.GetPlayerFromController(controller);
        if (Player == null || !Player.IsValid) return;

        if (weaponName == "weapon_c4")
        {
            @event.SetResult(false);
            return;
        }

        _globals.IsZombie.TryGetValue(Player.PlayerID, out bool isZombie);
        if (isZombie)
        {
            if (weaponName != "weapon_knife" && customname != "TVirusGrenade")
            {
                @event.SetResult(false);
            }
        }
        else
        {

            bool isGrenade = weaponName == "weapon_hegrenade"
                     || weaponName == "weapon_flashbang"
                     || weaponName == "weapon_decoy"
                     || weaponName == "weapon_incgrenade"
                     || weaponName == "weapon_smokegrenade";

            if (isGrenade)
            {
                var allowedHumanGrenades = new HashSet<string> 
                { 
                    "FireGrenade", 
                    "FreezeGrenade", 
                    "LightGrenade", 
                    "TeleprotGrenade", 
                    "Incgrenade" 
                };

                if (string.IsNullOrEmpty(customname) || !allowedHumanGrenades.Contains(customname))
                {
                    @event.SetResult(false);
                }
            }
        }
    }
    private void Event_OnMapLoad(IOnMapLoadEvent @event)
    {
        _commands.ServerCvar();
        _helpers.ClearAllStackedCenterHTML();
        var VoxCFG = _voxCFG.CurrentValue;
        var VoxList = VoxCFG.VoxList;
        if (_globals.RoundVoxGroup == null && VoxList != null)
        {
            _globals.RoundVoxGroup = _helpers.PickRandomActiveGroup(VoxList);
        }

        var cfg = _mainCFG.CurrentValue;

        // Reset the cached fog controller so a fresh entity is always created on every
        // map load instead of accidentally reusing a stale handle from the previous map.
        _globals.GlobalFogController = default;
        _helpers.ResetBarCharCache();
        _gameRulesProxy = null; // invalidate cached GameRulesProxy on map change

        // Apply fog and skybox after a world tick so entities are fully ready.
        _core.Scheduler.NextWorldUpdate(() =>
        {
            _helpers.ApplyFog(cfg.Fog);
            _helpers.ApplySkybox(cfg.Skybox);
        });

        // Delayed retry for workshop maps whose entity system may not be fully
        // initialised by the time the first NextWorldUpdate fires.
        _core.Scheduler.DelayBySeconds(1.5f, () =>
        {
            _helpers.ApplyFog(cfg.Fog);
        });

        // Second delayed retry with a longer window for slow-loading maps.
        // OnRoundFreezeEnd also provides a final per-round fallback.
        _core.Scheduler.DelayBySeconds(FogSecondRetryDelaySec, () =>
        {
            _helpers.ApplyFog(cfg.Fog);
        });
    }

    private void Event_OnMapUnload(IOnMapUnloadEvent @event)
    {
        _helpers.ClearAllStackedCenterHTML();

        // ── Cleanup all map-level timers and state ────────────────────────────────
        // Map changes do not trigger per-entity unload, so we must explicitly
        // clean up all timers, handles, and dictionaries keyed by per-map data.

        // Cancel all round/game timers
        _globals.g_hRoundEndTimer?.Cancel();
        _globals.g_hRoundEndTimer = null;
        _globals.g_hCountdown?.Cancel();
        _globals.g_hCountdown = null;
        _globals.g_IdleTimer?.Cancel();
        _globals.g_IdleTimer = null;
        _globals.g_ZombieRegenTimer?.Cancel();
        _globals.g_ZombieRegenTimer = null;
        _globals.g_hAmbMusic?.Cancel();
        _globals.g_hAmbMusic = null;
        _globals.AssassinTimer?.Cancel();
        _globals.AssassinTimer = null;

        // Clean up burn state timers
        foreach (var timer in _globals.ActiveBurns.Values)
        {
            timer.timer?.Cancel();
        }
        _globals.ActiveBurns.Clear();

        // Clean up mine think timers and state
        foreach (var timer in _globals.MineThink.Values)
        {
            timer?.Cancel();
        }
        _globals.MineThink.Clear();
        _globals.MineData.Clear();
        _globals.MineBeam.Clear();
        _globals.PlayerMineCounts.Clear();
        _globals.MineCurrentHP.Clear();
        _globals.MineOwnerPlayerID.Clear();

        // Clean up light timers
        foreach (var timer in _globals.lightTimers.Values)
        {
            timer?.Cancel();
        }
        _globals.lightTimers.Clear();
        _globals.activeLights.Clear();

        // Clean up glow entities
        _globals.GlowEntity.Clear();

        // Reset round-level state flags
        _globals.GameStart = false;
        _globals.SafeRoundStart = false;
        _globals.InfectionStartedThisRound = false;
        _globals.AdminForcedModeThisRound = false;
        _globals.MotherZombieWasSelected = false;
        _globals.NormalRoundsStreak = 0;
        _globals.RoundVoxGroup = null;

        // Clear all per-player state dictionaries
        _globals.IsZombie.Clear();
        _globals.IsMother.Clear();
        _globals.IsSurvivor.Clear();
        _globals.IsSniper.Clear();
        _globals.IsNemesis.Clear();
        _globals.IsAssassin.Clear();
        _globals.IsHero.Clear();
        _globals.g_ZombieIdleStates.Clear();
        _globals.g_ZombieRegenStates.Clear();
        _globals.StopZombieTimers.Clear();
        _globals.g_IsInvisible.Clear();
        _globals.ThrowerIsZombie.Clear();
        _globals.ScbaSuit.Clear();
        _globals.GodState.Clear();
        _globals.InfiniteAmmoState.Clear();
        _globals.CanBuyWeaponsThisRound.Clear();
        _globals.WeaponGrenadesGivenThisRound.Clear();
        _globals.DamageAccumulator.Clear();
        _globals.ExtraJumps.Clear();
        _globals.HasParachute.Clear();
        _globals.ParachuteRestoreGravity.Clear();
        _globals.NemesisFrostCharges.Clear();
        _globals.NemesisFrostCooldown.Clear();
        _classAbilities.ClearAll();
        _globals.JumpsUsed.Clear();
        _globals.KnifeBlinkCharges.Clear();
        _globals.KnifeBlinkCooldownEnd.Clear();
        _globals.ZombieMadnessActive.Clear();
        _globals.PrevJumpPressed.Clear();
        _globals.PrevOnGround.Clear();
        _globals.LeapCooldownEnd.Clear();
        _globals.SpawnProtectionEndTime.Clear();
        _globals.InfiniteClipState.Clear();
        _globals.ExtraNoRecoilState.Clear();
        _globals.TryderState.Clear();
        _globals.ItemPurchaseCount.Clear();
        _globals.HasJetpack.Clear();
        _globals.JetpackFuel.Clear();
        _globals.JetpackLastFuelTime.Clear();
        _globals.HasReviveToken.Clear();
        _globals.jumpBoostState.Clear();
        _globals.SpecialRoleLastRound.Clear();
        _globals.SpecialRoleThisRound.Clear();
    }

    private readonly struct DamageEventContext
    {
        public DamageEventContext(CEntityInstance victimEntity)
        {
            VictimEntity = victimEntity;
        }

        public CEntityInstance VictimEntity { get; init; }
        public CCSPlayerPawn? VictimPawn { get; init; }
        public IPlayer? VictimPlayer { get; init; }
        public CEntityInstance? AttackerEntity { get; init; }
        public CCSPlayerPawn? AttackerPawn { get; init; }
        public IPlayer? AttackerPlayer { get; init; }
    }

    private bool TryBuildDamageContext(IOnEntityTakeDamageEvent @event, out DamageEventContext context)
    {
        context = default;

        var victimEntity = @event.Entity;
        if (victimEntity == null || !victimEntity.IsValid)
            return false;

        var victimPawn = TryAsPlayerPawn(victimEntity);

        CEntityInstance? attackerEntity = null;
        var attackerHandle = @event.Info.Attacker;
        if (attackerHandle.IsValid)
        {
            var resolved = attackerHandle.Value;
            if (resolved != null && resolved.IsValid)
                attackerEntity = resolved;
        }
        var attackerPawn = TryAsPlayerPawn(attackerEntity);

        context = new DamageEventContext(victimEntity)
        {
            VictimPawn = victimPawn,
            VictimPlayer = TryResolvePlayerFromPawn(victimPawn, out var vp) ? vp : null,
            AttackerEntity = attackerEntity,
            AttackerPawn = attackerPawn,
            AttackerPlayer = TryResolvePlayerFromPawn(attackerPawn, out var ap) ? ap : null,
        };

        return true;
    }

    private static CCSPlayerPawn? TryAsPlayerPawn(CEntityInstance? entity)
    {
        if (entity == null || !entity.IsValid)
            return null;

        var pawn = entity.As<CCSPlayerPawn>();
        return (pawn != null && pawn.IsValid) ? pawn : null;
    }

    private bool TryResolvePlayerFromPawn(CCSPlayerPawn? pawn, out IPlayer player)
    {
        player = null!;
        if (pawn == null || !pawn.IsValid)
            return false;

        var controllerHandle = pawn.Controller;
        if (!controllerHandle.IsValid)
            return false;

        var controller = controllerHandle.Value?.As<CCSPlayerController>();
        if (controller == null || !controller.IsValid)
            return false;

        var resolved = _core.PlayerManager.GetPlayerFromController(controller);
        if (resolved == null || !resolved.IsValid)
            return false;

        player = resolved;
        return true;
    }

    private static bool TryGetActiveWeapon(CCSPlayerPawn? pawn, out CBasePlayerWeapon activeWeapon)
    {
        activeWeapon = null!;
        if (pawn == null || !pawn.IsValid)
            return false;

        var weaponServices = pawn.WeaponServices;
        if (weaponServices == null || !weaponServices.IsValid)
            return false;

        var activeWeaponHandle = weaponServices.ActiveWeapon;
        if (!activeWeaponHandle.IsValid)
            return false;

        var resolved = activeWeaponHandle.Value;
        if (resolved == null || !resolved.IsValid)
            return false;

        activeWeapon = resolved;
        return true;
    }

    private void Event_OnEntityTakeDamage(IOnEntityTakeDamageEvent @event)
    {
        // ── No fall damage — same as CS 1.6 Zombie Plague ──────────────────────
        const DamageTypes_t DMG_FALL = (DamageTypes_t)32;
        if ((@event.Info.DamageType & DMG_FALL) != 0)
        {
            @event.Info.Damage = 0f;
            @event.Result = HookResult.Stop;
            return;
        }

        if (!TryBuildDamageContext(@event, out var context))
            return;

        HandleBaseEntityTakeDamage(@event, in context);
        HandleHumanTakeDamage(@event, in context);
        HandleEntityTakeSoundDamage(@event, in context);
        HandleInGrenadeDamage(@event, in context);
    }

    private void HandleBaseEntityTakeDamage(IOnEntityTakeDamageEvent @event, in DamageEventContext context)
    {
        var VictimPlayer = context.VictimPlayer;
        var AttackerPlayer = context.AttackerPlayer;
        if (VictimPlayer == null || !VictimPlayer.IsValid || AttackerPlayer == null || !AttackerPlayer.IsValid)
            return;

        var VictimPawn = context.VictimPawn;
        var AttackerPawn = context.AttackerPawn;

        var victimId = VictimPlayer.PlayerID;
        var attackerId = AttackerPlayer.PlayerID;

        _globals.IsZombie.TryGetValue(attackerId, out bool attackerIsZombie);
        _globals.IsZombie.TryGetValue(victimId, out bool victimIsZombie);
        _globals.ScbaSuit.TryGetValue(victimId, out bool IsHaveScbaSuit);
        _globals.GodState.TryGetValue(victimId, out bool IsGodState);

        // Block all incoming damage during spawn protection.
        if (_globals.SpawnProtectionEndTime.TryGetValue(victimId, out long protEnd)
            && Environment.TickCount64 < protEnd)
        {
            @event.Info.Damage = 0;
            return;
        }

        // Cross-check with actual team to resolve any IsZombie state mismatches.
        // Zombies are always on Team.T and humans on Team.CT.
        CCSPlayerController? VictimController = null;
        CCSPlayerController? AttackerController = null;
        if (VictimPawn != null)
        {
            var h = VictimPawn.Controller;
            if (h.IsValid) VictimController = h.Value?.As<CCSPlayerController>();
        }
        if (AttackerPawn != null)
        {
            var h = AttackerPawn.Controller;
            if (h.IsValid) AttackerController = h.Value?.As<CCSPlayerController>();
        }
        if (AttackerController != null)
        {
            if (AttackerController.Team == Team.T) attackerIsZombie = true;
            else if (AttackerController.Team == Team.CT) attackerIsZombie = false;
        }
        if (VictimController != null)
        {
            if (VictimController.Team == Team.T) victimIsZombie = true;
            else if (VictimController.Team == Team.CT) victimIsZombie = false;
        }

        var CFG = _mainCFG.CurrentValue;

        if (!attackerIsZombie && !victimIsZombie)
        {
            @event.Info.Damage = 0;
        }
        else if (attackerIsZombie && !victimIsZombie)
        {
            var zombieConfig = _zombieClassCFG.CurrentValue;
            var specialConfig = _SpecialClassCFG.CurrentValue;
            var zombie = _zombieState.GetZombieClass(attackerId, zombieConfig.ZombieClassList, specialConfig.SpecialClassList);
            if (zombie == null)
                return;

            if (IsHaveScbaSuit)
            {
                @event.Info.Damage = 0;
                _helpers.RemoveScbaSuit(VictimPlayer, CFG.ScbaSuitBrokenSound);
            }
            else if (IsGodState)
            {
                @event.Info.Damage = 0;
            }
            else
            {
                @event.Info.Damage += zombie.Stats.Damage;
            }
        }
        else if (!attackerIsZombie && victimIsZombie)
        {
            if (IsGodState)
            {
                @event.Info.Damage = 0;
                return;
            }

            _globals.ZombieMadnessActive.TryGetValue(victimId, out bool madnessActive);
            if (madnessActive)
            {
                @event.Info.Damage = 0;
                return;
            }
        }
    }

    private void Event_OnClientConnected(SwiftlyS2.Shared.Events.IOnClientConnectedEvent @event)
    {
        var id = @event.PlayerId;

        // Ensure the player's economy data is loaded so balance queries are reliable.
        var player = _core.PlayerManager.GetPlayer(id);
        if (player != null && player.IsValid && !player.IsFakeClient)
        {
            _logger.LogInformation("[ZM] OnClientConnected: Player {Id} connected (SteamID={SteamID}). Loading economy data...", id, player.SteamID);
            _ammoPacks.LoadData(player);
        }

        if (_globals.GameStart)
        {
            _service.CheckRoundWinConditions();
        }

        if (_globals.ServerIsEmpty)
        {
            _globals.ServerIsEmpty = false;
            _core.Scheduler.DelayBySeconds(1.0f, () =>
            {
                _helpers.restartgame();
            });
        }

        _globals.IsZombie[id] = _globals.GameStart;
    }

    private void Event_OnClientDisconnected(SwiftlyS2.Shared.Events.IOnClientDisconnectedEvent @event)
    {
        var id = @event.PlayerId;
        _helpers.ClearStackedCenterHTML(id);

        // Close any open menu FIRST – while the native player controller is still
        // guaranteed alive – so SwiftlyS2's per-player render timer cannot fire on
        // an already-freed controller after disconnect and crash with SIGSEGV
        // (MenuAPI.BuildMenuHtml null-dereference at address 0x0).
        var disconnectedPlayer = _core.PlayerManager.GetPlayer(id);
        if (disconnectedPlayer != null && disconnectedPlayer.IsValid)
        {
            _core.MenusAPI.CloseActiveMenu(disconnectedPlayer);
            _helpers.RemoveGlow(disconnectedPlayer);
        }

        if (_globals.GameStart)
        {
            _service.CheckRoundWinConditions();
        }

        _helpers.ClearPlayerBurn(id);
        _globals.IsZombie.Remove(id);
        _globals.IsMother.Remove(id);
        _globals.IsSurvivor.Remove(id);
        _globals.IsSniper.Remove(id);
        _globals.IsNemesis.Remove(id);
        _globals.IsAssassin.Remove(id);
        _globals.IsHero.Remove(id);

        _globals.ScbaSuit.Remove(id);
        _globals.GodState.Remove(id);
        _globals.InfiniteAmmoState.Remove(id);
        _globals.CanBuyWeaponsThisRound.Remove(id);
        _globals.WeaponGrenadesGivenThisRound.Remove(id);

        // Clean up Economy tracking
        _ammoPacks.RemovePlayer(id);

        _globals.g_ZombieIdleStates.Remove(id);
        _globals.g_ZombieRegenStates.Remove(id);
        _globals.StopZombieTimers.Remove(id);
        _globals.g_IsInvisible.Remove(id);
        _globals.ThrowerIsZombie.Remove(id);

        // Extra items cleanup
        _globals.DamageAccumulator.Remove(id);
        _globals.ExtraJumps.Remove(id);
        _globals.JumpsUsed.Remove(id);
        _globals.KnifeBlinkCharges.Remove(id);
        _globals.KnifeBlinkCooldownEnd.Remove(id);
        _globals.ZombieMadnessActive.Remove(id);
        _globals.PrevJumpPressed.Remove(id);
        _globals.InfiniteClipState.Remove(id);
        _globals.ExtraNoRecoilState.Remove(id);
        _globals.TryderState.Remove(id);
        _globals.ItemPurchaseCount.Remove(id);
        _globals.HasParachute.Remove(id);
        _globals.ParachuteRestoreGravity.Remove(id);
        // Jetpack / Revive Token
        _extraItemsMenu.CleanupJetpack(id);
        // Cleanup mines for disconnecting player
        if (disconnectedPlayer != null && disconnectedPlayer.IsValid && disconnectedPlayer.SteamID != 0)
            _mineService.CleanupMinesForPlayer(disconnectedPlayer.SteamID);
        _globals.HasReviveToken.Remove(id);

        _globals.InSwing[id] = false;
        _globals.SpawnProtectionEndTime.Remove(id);
        _globals.LeapCooldownEnd.Remove(id);
        _globals.PrevOnGround.Remove(id);
        _classAbilities.ClearPlayer(id);

        // Remove the player's local AP balance cache entry.
        _ammoPacks.RemovePlayer(id);

        _core.Scheduler.DelayBySeconds(1.0f, () =>
        {
            var playerCount = _helpers.ServerPlayerCount();
            if (playerCount <= 0 && !_globals.ServerIsEmpty)
            {
                _globals.ServerIsEmpty = true;
                _helpers.restartgame();
            }
        });
    }

    private void Event_OnTickSpeed()
    {
        var allplayer = _core.PlayerManager.GetAlive();
        foreach (var player in allplayer)
        {
            if (!player.IsValid)
                continue;

            var controller = player.Controller;
            if (controller == null || !controller.IsValid)
                continue;

            var pawn = player.PlayerPawn;
            if (pawn == null || !pawn.IsValid)
                continue;

            var Id = player.PlayerID;
            _globals.IsZombie.TryGetValue(Id, out bool IsZombie);
            _globals.IsSurvivor.TryGetValue(Id, out bool IsSurvivor);
            _globals.IsSniper.TryGetValue(Id, out bool IsSniper);
            _globals.IsHero.TryGetValue(Id, out bool IsHero);
            if (IsZombie)
            {
                var zombieConfig = _zombieClassCFG.CurrentValue;
                var specialConfig = _SpecialClassCFG.CurrentValue;
                var zombie = _zombieState.GetZombieClass(Id, zombieConfig.ZombieClassList, specialConfig.SpecialClassList);
                if (zombie == null)
                    continue;

                float zSpeed = zombie.Stats.Speed > 0 ? zombie.Stats.Speed : 1.0f;
                pawn.VelocityModifier = zSpeed;
                pawn.VelocityModifierUpdated();

                float zGravity = zombie.Stats.Gravity;
                pawn.ActualGravityScale = zGravity;
            }
            else if (IsSurvivor)
            {
                float Speed = _mainCFG.CurrentValue.Survivor.SurvivorSpeed > 0 ? _mainCFG.CurrentValue.Survivor.SurvivorSpeed : 3.0f;
                pawn.VelocityModifier = Speed;
                pawn.VelocityModifierUpdated();

                float Gravity = _mainCFG.CurrentValue.Survivor.SurvivorGravity;
                pawn.ActualGravityScale = Gravity;
            }
            else if (IsSniper)
            {
                float Speed = _mainCFG.CurrentValue.Sniper.SniperSpeed > 0 ? _mainCFG.CurrentValue.Sniper.SniperSpeed : 2.0f;
                pawn.VelocityModifier = Speed;
                pawn.VelocityModifierUpdated();

                float Gravity = _mainCFG.CurrentValue.Sniper.SniperGravity;
                pawn.ActualGravityScale = Gravity;
            }
            else if (IsHero)
            {
                float Speed = _mainCFG.CurrentValue.Hero.HeroSpeed > 0 ? _mainCFG.CurrentValue.Hero.HeroSpeed : 2.0f;
                pawn.VelocityModifier = Speed;
                pawn.VelocityModifierUpdated();

                float Gravity = _mainCFG.CurrentValue.Hero.HeroGravity;
                pawn.ActualGravityScale = Gravity;
            }
            else
            {
                float Speed = _mainCFG.CurrentValue.HumanInitialSpeed > 0 ? _mainCFG.CurrentValue.HumanInitialSpeed : 1.0f;
                pawn.VelocityModifier = Speed;
                pawn.VelocityModifierUpdated();

                float Gravity = _mainCFG.CurrentValue.HumanInitialGravity;
                pawn.ActualGravityScale = Gravity;
            }
        }

    }

    private void Event_OnTickNoRecoil()
    {
        var CFG = _mainCFG.CurrentValue;
        bool globalNoRecoil = CFG.EnableWeaponNoRecoil;

        foreach (var player in _core.PlayerManager.GetAllPlayers())
        {
            if (!player.IsValid)
                continue;

            if (!globalNoRecoil)
            {
                int id = player.PlayerID;
                _globals.ExtraNoRecoilState.TryGetValue(id, out bool extraNoRecoil);
                _globals.TryderState.TryGetValue(id, out bool isTryder);
                if (!extraNoRecoil && !isTryder)
                    continue;
            }

            var controller = player.Controller;
            if (controller == null || !controller.IsValid)
                continue;

            var pawn = player.PlayerPawn;
            if (pawn == null || !pawn.IsValid)
                continue;

            var ControllerValue = controller.PlayerPawn.Value;
            if (ControllerValue == null || !ControllerValue.IsValid)
                continue;

            var WeaponServices = ControllerValue.WeaponServices;
            if (WeaponServices == null || !WeaponServices.IsValid)
                continue;

            var weapon = WeaponServices.ActiveWeapon.Value;
            if (weapon == null || !weapon.IsValid)
                continue;

            var aimPunchServices = pawn.AimPunchServices;
            if (aimPunchServices == null || !aimPunchServices.IsValid)
                continue;

            aimPunchServices.PredictableBaseAngle.Pitch = 0;
            aimPunchServices.PredictableBaseAngle.Yaw = 0;
            aimPunchServices.PredictableBaseAngle.Roll = 0;
            aimPunchServices.PredictableBaseAngleVel.Pitch = 0;
            aimPunchServices.PredictableBaseAngleVel.Yaw = 0;
            aimPunchServices.PredictableBaseAngleVel.Roll = 0;
            aimPunchServices.UnpredictableBaseAngle.Pitch = 0;
            aimPunchServices.UnpredictableBaseAngle.Yaw = 0;
            aimPunchServices.UnpredictableBaseAngle.Roll = 0;
            aimPunchServices.PredictableBaseTickInterpAmount = 0;
        }
    }

    private void Event_OnTickMultijump()
    {
        const float JumpVelocityZ = 300f; // upward impulse for mid-air jumps

        foreach (var player in _core.PlayerManager.GetAlive())
        {
            if (!player.IsValid)
                continue;

            int id = player.PlayerID;
            _globals.ExtraJumps.TryGetValue(id, out int extraJumps);
            if (extraJumps <= 0)
                continue;

            var controller = player.Controller;
            if (controller == null || !controller.IsValid)
                continue;

            var pawn = player.PlayerPawn;
            if (pawn == null || !pawn.IsValid)
                continue;

            // Detect jump button press this tick
            bool jumpPressed = (player.PressedButtons & GameButtonFlags.Space) != 0;
            _globals.PrevJumpPressed.TryGetValue(id, out bool prevJumpPressed);
            _globals.PrevJumpPressed[id] = jumpPressed;

            // Only act on a fresh press (rising edge)
            if (!jumpPressed || prevJumpPressed)
                continue;

            // Restore jumps when player lands
            bool onGround = pawn.GroundEntity.IsValid;
            if (onGround)
            {
                // Re-read max jumps from class ability config for zombies
                _globals.IsZombie.TryGetValue(id, out bool isZombieForReset);
                if (isZombieForReset)
                {
                    var cfg = _zombieClassCFG.CurrentValue;
                    var zclass = _zombieState.GetZombieClass(id, cfg.ZombieClassList);
                    int maxJumps = zclass?.Abilities.ExtraJumps ?? 0;
                    if (maxJumps > 0) _globals.ExtraJumps[id] = maxJumps;
                }
                continue;
            }

            // Consume one extra jump and apply upward impulse
            _globals.ExtraJumps[id] = extraJumps - 1;
            var vel = pawn.AbsVelocity;
            pawn.Teleport(null, null, new SwiftlyS2.Shared.Natives.Vector(vel.X, vel.Y, JumpVelocityZ));
        }
    }

    private void Event_OnTickJetpack()
    {
        var jetpackCFG = _extraItemsCFG.CurrentValue;
        float rechargeTime = jetpackCFG.JetpackRechargeTime;

        foreach (var player in _core.PlayerManager.GetAlive())
        {
            if (!player.IsValid) continue;

            int id = player.PlayerID;
            if (!_globals.HasJetpack.TryGetValue(id, out bool hasJetpack) || !hasJetpack) continue;

            _globals.IsZombie.TryGetValue(id, out bool isZombie);
            if (isZombie) continue;

            _extraItemsMenu.TryExecuteJetpackThrust(player);

            // ── Auto-recharge when not thrusting ─────────────────────────────
            if (rechargeTime <= 0f) continue;

            _globals.JetpackFuel.TryGetValue(id, out float fuel);
            float maxFuel = jetpackCFG.JetpackMaxFuel;
            if (fuel >= maxFuel) continue;

            bool wantsJetpackThrust =
                (player.PressedButtons & GameButtonFlags.Ctrl) != 0 &&
                (player.PressedButtons & GameButtonFlags.Space) != 0;
            if (wantsJetpackThrust) continue;

            float now        = _core.Engine.GlobalVars.CurrentTime;
            float lastThrust = _globals.JetpackLastThrustTime.TryGetValue(id, out float lt) ? lt : 0f;

            // Only recharge if not currently thrusting
            // (lastThrustTime is updated every thrust tick, so if it's recent = still flying)
            if (now - lastThrust < 0.15f) continue;

            // Recharge rate = maxFuel / rechargeTime units per second
            // Tick runs at ~64Hz so dt ≈ 0.015s
            float rechargeRate = maxFuel / rechargeTime;
            float dt           = 1f / 64f;   // safe constant tick rate
            float newFuel      = Math.Min(maxFuel, fuel + rechargeRate * dt);
            _globals.JetpackFuel[id] = newFuel;

            // ── Recharge HUD — show every ~0.5s to avoid spam ────────────────
            // Use lastThrust as a proxy: show HUD on ticks where int(now*2) changes
            if ((int)(now * 2f) % 32 == 0)
            {
                float fuelPct   = Math.Clamp(newFuel / maxFuel, 0f, 1f);
                float remaining = rechargeTime * (1f - fuelPct);
                string fuelColor = "#FFD700"; // gold during recharge
                string bar = _helpers.BuildProgressBar(fuelPct, 10, fuelColor, "#666666", player);
                _helpers.SendStackedCenterHTML(
                    player,
                    "jetpack",
                    $"<span color=\"#AAAAAA\" class=\"fontSize-l fontWeight-bold\">JETPACK FUEL</span><br>"
                    + bar
                    + $" <span color=\"{fuelColor}\" class=\"fontSize-l fontWeight-bold\">{(int)(fuelPct * 100)}%</span><br>"
                    + $"<span color=\"#FF3030\" class=\"fontSize-l fontWeight-bold\">RECHARGING... {remaining:F0}s</span>",
                    600,
                    priority: 30);
            }
        }
    }

    private void Event_OnTickHudFix()
    {
        // Alternate ticks — runs every other tick to reduce overhead
        _hudFixRunThisTick = !_hudFixRunThisTick;
        if (!_hudFixRunThisTick) return;

        // Get (and cache) the CCSGameRulesProxy entity
        if (_gameRulesProxy == null || !_gameRulesProxy.IsValid || !_gameRulesProxy.IsValidEntity)
        {
            _gameRulesProxy = _core.EntitySystem
                .GetAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules")
                .FirstOrDefault();
        }
        if (_gameRulesProxy == null || !_gameRulesProxy.IsValid) return;

        var gameRules = _gameRulesProxy.GameRules;
        if (gameRules == null) return;
        if (gameRules.WarmupPeriod) return; // no fix needed during warmup

        float currentTime   = _core.Engine.GlobalVars.CurrentTime;
        float restartTime   = gameRules.RestartRoundTime.Value;
        bool  expectedState = restartTime < currentTime;

        // Only write when out-of-sync to avoid unnecessary schema churn
        if (gameRules.GameRestart != expectedState)
        {
            gameRules.GameRestart = expectedState;
            gameRules.GameRestartUpdated();
        }
    }

    private void Event_OnTickParachute()
    {
        if (!_globals.GameStart) return;
        if (_globals.HasParachute.Count == 0) return;

        const float parachuteGravityScale = 0.25f;
        var extraCfg = _extraItemsCFG.CurrentValue;
        float maxFallSpeed = Math.Max(1f, extraCfg.ParachuteFallSpeed); // max downward speed (positive u/s)

        foreach (var player in _core.PlayerManager.GetAlive())
        {
            if (!player.IsValid || player.IsFakeClient) continue;

            int id = player.PlayerID;
            if (!_globals.HasParachute.Contains(id)) continue;

            _globals.IsZombie.TryGetValue(id, out bool isZombie);
            if (isZombie) continue;

            var pawn = player.PlayerPawn;
            if (pawn == null || !pawn.IsValid) continue;

            bool onGround = pawn.GroundEntity.IsValid;
            bool eHeld    = (player.PressedButtons & GameButtonFlags.E) != 0;
            bool jetpackThrustHeld =
                (player.PressedButtons & GameButtonFlags.Ctrl) != 0 &&
                (player.PressedButtons & GameButtonFlags.Space) != 0;
            var vel = pawn.AbsVelocity;
            bool isFalling = vel.Z < 0f;

            // Restore gravity unless the player is actively falling with parachute held.
            if (onGround || !eHeld || jetpackThrustHeld || !isFalling)
            {
                RestoreParachuteGravity(id, pawn);
                continue;
            }

            // E held while falling - use light gravity and clamp descent speed.
            if (!_globals.ParachuteRestoreGravity.ContainsKey(id))
                _globals.ParachuteRestoreGravity[id] = pawn.ActualGravityScale;

            if (Math.Abs(pawn.ActualGravityScale - parachuteGravityScale) > 0.001f)
                pawn.ActualGravityScale = parachuteGravityScale;

            // Clamp downward velocity to maxFallSpeed
            if (vel.Z < -maxFallSpeed)
                pawn.Teleport(null, null,
                    new SwiftlyS2.Shared.Natives.Vector(vel.X, vel.Y, -maxFallSpeed));
        }
    }

    private void RestoreParachuteGravity(int playerId, CCSPlayerPawn pawn)
    {
        if (_globals.ParachuteRestoreGravity.TryGetValue(playerId, out float gravity))
        {
            pawn.ActualGravityScale = gravity;
            _globals.ParachuteRestoreGravity.Remove(playerId);
            return;
        }

        if (pawn.ActualGravityScale == 0f)
            pawn.ActualGravityScale = 1.0f;
    }

    private void Event_OnTickNemesisFrost()
    {
        if (!_globals.GameStart) return;

        var nemCfg = _mainCFG.CurrentValue.Nemesis;
        if (!nemCfg.FrostAbilityEnabled) return;

        foreach (var player in _core.PlayerManager.GetAlive())
        {
            if (!player.IsValid || player.IsFakeClient) continue;

            int id = player.PlayerID;
            if (!IsNemesisFrostUser(player)) continue;

            // E key triggers frost
            bool ePressed  = (player.PressedButtons & GameButtonFlags.E) != 0;
            _globals.PrevJumpPressed.TryGetValue(id + 10000, out bool prevE);
            _globals.PrevJumpPressed[id + 10000] = ePressed;
            if (!ePressed || prevE) continue; // rising edge only

            if (TryUseNemesisFrost(player, nemCfg))
                continue;

            // Check charges
            _globals.NemesisFrostCharges.TryGetValue(id, out int charges);
            if (charges <= 0)
            {
                _helpers.SendStackedCenterHTML(
                    player,
                    "frost",
                    "<span color=\"#00CFFF\" class=\"fontSize-l fontWeight-bold\">FROST</span><br>" +
                    _helpers.BuildProgressBar(0f, 10, "#666666", "#666666", player) +
                    " <span color=\"#FF3030\" class=\"fontSize-l fontWeight-bold\">NO CHARGES</span>",
                    2000,
                    priority: 35);
                continue;
            }

            // Check cooldown
            float now = _core.Engine.GlobalVars.CurrentTime;
            _globals.NemesisFrostCooldown.TryGetValue(id, out float nextUse);
            if (now < nextUse)
            {
                float wait = nextUse - now;
                float cdPct = 1f - Math.Clamp(wait / nemCfg.FrostCooldown, 0f, 1f);
                _helpers.SendStackedCenterHTML(
                    player,
                    "frost",
                    "<span color=\"#00CFFF\" class=\"fontSize-l fontWeight-bold\">FROST</span><br>" +
                    _helpers.BuildProgressBar(cdPct, 10, "#00CFFF", "#666666", player) +
                    $" <span color=\"#FFD700\" class=\"fontSize-l fontWeight-bold\">COOLDOWN {wait:F1}s</span>",
                    500,
                    priority: 35);
                continue;
            }

            // Find nearest human in range
            var pawn = player.PlayerPawn;
            if (pawn == null || !pawn.IsValid) continue;
            var origin = pawn.AbsOrigin;
            if (origin == null) continue;

            IPlayer? target = null;
            float closestDist = nemCfg.FrostRange;

            foreach (var other in _core.PlayerManager.GetAlive())
            {
                if (!other.IsValid || other.IsFakeClient) continue;
                int oid = other.PlayerID;
                if (oid == id) continue;

                var otherController = other.Controller;
                if (otherController == null || !otherController.IsValid || otherController.Team != Team.CT)
                    continue;

                _globals.IsZombie.TryGetValue(oid, out bool otherIsZombie);
                _globals.IsMother.TryGetValue(oid, out bool otherIsMother);
                _globals.IsNemesis.TryGetValue(oid, out bool otherIsNemesis);
                _globals.IsAssassin.TryGetValue(oid, out bool otherIsAssassin);
                if (otherIsZombie || otherIsMother || otherIsNemesis || otherIsAssassin)
                    continue;

                var oPawn = other.PlayerPawn;
                if (oPawn == null || !oPawn.IsValid) continue;
                var oPos = oPawn.AbsOrigin;
                if (oPos == null) continue;

                float dx = oPos.Value.X - origin.Value.X;
                float dy = oPos.Value.Y - origin.Value.Y;
                float dz = oPos.Value.Z - origin.Value.Z;
                float dist = MathF.Sqrt(dx*dx + dy*dy + dz*dz);

                if (dist < closestDist)
                {
                    closestDist = dist;
                    target = other;
                }
            }

            if (target == null) continue;

            // Apply frost
            int chargesLeft = charges - 1;
            _globals.NemesisFrostCharges[id] = chargesLeft;
            _globals.NemesisFrostCooldown[id] = now + nemCfg.FrostCooldown;

            _helpers.SetZombieFreezeOrStun(target, nemCfg.FrostDuration);

            // ── Frost HUD for Nemesis ─────────────────────────────────────────
            float chargesPct = nemCfg.FrostMaxCharges > 0
                ? Math.Clamp((float)chargesLeft / nemCfg.FrostMaxCharges, 0f, 1f)
                : 0f;
            string chargesBar = _helpers.BuildProgressBar(chargesPct, 10, "#00CFFF", "#666666", player);
            string frozenName  = target.Name;
            _helpers.SendStackedCenterHTML(
                player,
                "frost",
                $"<span color=\"#00CFFF\" class=\"fontSize-l fontWeight-bold\">FROST</span>" +
                $" <span color=\"#AAAAAA\" class=\"fontSize-l\">-></span> <span color=\"#FFFFFF\" class=\"fontSize-l\">{frozenName}</span><br>" +
                chargesBar +
                $" <span color=\"#00CFFF\" class=\"fontSize-l fontWeight-bold\">{chargesLeft}/{nemCfg.FrostMaxCharges}</span>",
                2500,
                priority: 35);

            // Chat notifications
            _helpers.SendChatT(player, "NemesisFrostUsed", chargesLeft);
            _helpers.SendChatT(target, "NemesisFrozenByNemesis");
        }
    }

    private void Event_OnClientKeyStateChangedNemesisFrost(IOnClientKeyStateChangedEvent @event)
    {
        if (!_globals.GameStart || !@event.Pressed || !IsNemesisFrostKey(@event.Key.ToString()))
            return;

        var nemCfg = _mainCFG.CurrentValue.Nemesis;
        if (!nemCfg.FrostAbilityEnabled)
            return;

        var player = _core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player == null || !player.IsValid || player.IsFakeClient || !IsNemesisFrostUser(player))
            return;

        _globals.PrevJumpPressed[player.PlayerID + 10000] = true;
        TryUseNemesisFrost(player, nemCfg);
    }

    private bool TryUseNemesisFrost(IPlayer player, NemesisModeConfig nemCfg)
    {
        int id = player.PlayerID;

        _globals.NemesisFrostCharges.TryGetValue(id, out int charges);
        if (charges <= 0)
        {
            _helpers.SendStackedCenterHTML(
                player,
                "frost",
                "<span color=\"#00CFFF\" class=\"fontSize-l fontWeight-bold\">FROST</span><br>" +
                _helpers.BuildProgressBar(0f, 10, "#666666", "#666666", player) +
                " <span color=\"#FF3030\" class=\"fontSize-l fontWeight-bold\">NO CHARGES</span>",
                2000,
                priority: 35);
            return false;
        }

        float now = _core.Engine.GlobalVars.CurrentTime;
        _globals.NemesisFrostCooldown.TryGetValue(id, out float nextUse);
        if (now < nextUse)
        {
            float wait = nextUse - now;
            float cdPct = nemCfg.FrostCooldown > 0f
                ? 1f - Math.Clamp(wait / nemCfg.FrostCooldown, 0f, 1f)
                : 1f;
            _helpers.SendStackedCenterHTML(
                player,
                "frost",
                "<span color=\"#00CFFF\" class=\"fontSize-l fontWeight-bold\">FROST</span><br>" +
                _helpers.BuildProgressBar(cdPct, 10, "#00CFFF", "#666666", player) +
                $" <span color=\"#FFD700\" class=\"fontSize-l fontWeight-bold\">COOLDOWN {wait:F1}s</span>",
                500,
                priority: 35);
            return false;
        }

        var targets = FindNemesisFrostTargets(player, nemCfg);
        if (targets.Count == 0)
        {
            _helpers.SendStackedCenterHTML(
                player,
                "frost",
                "<span color=\"#00CFFF\" class=\"fontSize-l fontWeight-bold\">FROST</span><br>" +
                _helpers.BuildProgressBar(0f, 10, "#666666", "#666666", player) +
                " <span color=\"#FF3030\" class=\"fontSize-l fontWeight-bold\">NO TARGET</span>",
                900,
                priority: 35);
            return false;
        }

        int chargesLeft = charges - 1;
        _globals.NemesisFrostCharges[id] = chargesLeft;
        _globals.NemesisFrostCooldown[id] = now + Math.Max(0f, nemCfg.FrostCooldown);

        foreach (var target in targets)
        {
            _helpers.SetZombieFreezeOrStun(target, nemCfg.FrostDuration);
            _helpers.SendChatT(target, "NemesisFrozenByNemesis");
        }

        float chargesPct = nemCfg.FrostMaxCharges > 0
            ? Math.Clamp((float)chargesLeft / nemCfg.FrostMaxCharges, 0f, 1f)
            : 0f;
        string chargesBar = _helpers.BuildProgressBar(chargesPct, 10, "#00CFFF", "#666666", player);
        string targetText = targets.Count == 1 ? targets[0].Name : $"{targets.Count} TARGETS";
        _helpers.SendStackedCenterHTML(
            player,
            "frost",
            $"<span color=\"#00CFFF\" class=\"fontSize-l fontWeight-bold\">FROST</span>" +
            $" <span color=\"#AAAAAA\" class=\"fontSize-l\">-></span> <span color=\"#FFFFFF\" class=\"fontSize-l\">{targetText}</span><br>" +
            chargesBar +
            $" <span color=\"#00CFFF\" class=\"fontSize-l fontWeight-bold\">{chargesLeft}/{nemCfg.FrostMaxCharges}</span>",
            2500,
            priority: 35);

        _helpers.SendChatT(player, "NemesisFrostUsed", chargesLeft);
        return true;
    }

    private List<IPlayer> FindNemesisFrostTargets(IPlayer nemesis, NemesisModeConfig nemCfg)
    {
        var result = new List<(IPlayer Player, float DistanceSquared)>();
        var pawn = nemesis.PlayerPawn;
        if (pawn == null || !pawn.IsValid || pawn.AbsOrigin == null)
            return [];

        var origin = pawn.AbsOrigin.Value;
        float range = Math.Max(1f, nemCfg.FrostRange);
        float rangeSquared = range * range;

        foreach (var other in _core.PlayerManager.GetAlive())
        {
            if (!other.IsValid || other.IsFakeClient || other.PlayerID == nemesis.PlayerID)
                continue;

            if (!IsNemesisFrostTarget(other))
                continue;

            var otherPawn = other.PlayerPawn;
            if (otherPawn == null || !otherPawn.IsValid || otherPawn.AbsOrigin == null)
                continue;

            float distanceSquared = _helpers.DistanceSquared(origin, otherPawn.AbsOrigin.Value);
            if (distanceSquared <= rangeSquared)
                result.Add((other, distanceSquared));
        }

        int maxTargets = nemCfg.FrostMaxTargets <= 0
            ? result.Count
            : Math.Min(nemCfg.FrostMaxTargets, result.Count);

        return result
            .OrderBy(static item => item.DistanceSquared)
            .Take(maxTargets)
            .Select(static item => item.Player)
            .ToList();
    }

    private bool IsNemesisFrostUser(IPlayer player)
    {
        int id = player.PlayerID;
        if (_globals.IsNemesis.TryGetValue(id, out bool isNemesis) && isNemesis)
            return true;

        var className = _zombieState.GetPlayerZombieClass(id);
        if (string.IsNullOrWhiteSpace(className))
            return false;

        var nemCfg = _mainCFG.CurrentValue.Nemesis;
        bool classMatches = className.Equals("Nemesis", StringComparison.OrdinalIgnoreCase)
            || nemCfg.NemesisNames
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(name => name.Equals(className, StringComparison.OrdinalIgnoreCase));

        if (!classMatches)
            return false;

        _globals.IsNemesis[id] = true;
        if (!_globals.NemesisFrostCharges.ContainsKey(id))
            _globals.NemesisFrostCharges[id] = nemCfg.FrostMaxCharges;
        if (!_globals.NemesisFrostCooldown.ContainsKey(id))
            _globals.NemesisFrostCooldown[id] = 0f;

        return true;
    }

    private bool IsNemesisFrostTarget(IPlayer player)
    {
        var controller = player.Controller;
        if (controller == null || !controller.IsValid || controller.Team != Team.CT)
            return false;

        int id = player.PlayerID;
        _globals.IsZombie.TryGetValue(id, out bool isZombie);
        _globals.IsMother.TryGetValue(id, out bool isMother);
        _globals.IsNemesis.TryGetValue(id, out bool isNemesis);
        _globals.IsAssassin.TryGetValue(id, out bool isAssassin);

        return !isZombie && !isMother && !isNemesis && !isAssassin;
    }

    private static bool IsNemesisFrostKey(string keyName)
    {
        return keyName.Equals("E", StringComparison.OrdinalIgnoreCase)
            || keyName.Equals("KeyE", StringComparison.OrdinalIgnoreCase)
            || keyName.Equals("Use", StringComparison.OrdinalIgnoreCase);
    }

    private void Event_OnTickLeap()
    {
        if (!_globals.GameStart) return;

        var cfg = _mainCFG.CurrentValue;

        foreach (var player in _core.PlayerManager.GetAlive())
        {
            if (!player.IsValid) continue;

            int id = player.PlayerID;
            _globals.IsZombie.TryGetValue(id, out bool isZombie);
            if (!isZombie) continue;

            // Determine which leap settings apply to this zombie.
            _globals.IsNemesis.TryGetValue(id, out bool isNemesis);
            _globals.IsAssassin.TryGetValue(id, out bool isAssassin);

            bool leapEnabled;
            float leapForce, leapHeight, leapCooldown;

            if (isNemesis)
            {
                leapEnabled  = cfg.LeapNemesisEnabled;
                leapForce    = cfg.LeapNemesisForce;
                leapHeight   = cfg.LeapNemesisHeight;
                leapCooldown = cfg.LeapNemesisCooldown;
            }
            else if (isAssassin)
            {
                leapEnabled  = cfg.LeapAssassinEnabled;
                leapForce    = cfg.LeapAssassinForce;
                leapHeight   = cfg.LeapAssassinHeight;
                leapCooldown = cfg.LeapAssassinCooldown;
            }
            else
            {
                leapEnabled  = cfg.LeapZombiesEnabled;
                leapForce    = cfg.LeapZombiesForce;
                leapHeight   = cfg.LeapZombiesHeight;
                leapCooldown = cfg.LeapZombiesCooldown;
            }

            if (!leapEnabled) continue;

            var controller = player.Controller;
            if (controller == null || !controller.IsValid) continue;

            var pawn = player.PlayerPawn;
            if (pawn == null || !pawn.IsValid) continue;

            bool onGround = pawn.GroundEntity.IsValid;
            bool jumpPressed = (player.PressedButtons & GameButtonFlags.Space) != 0;

            _globals.PrevOnGround.TryGetValue(id, out bool prevOnGround);
            _globals.PrevOnGround[id] = onGround;

            // Leap triggers on the first tick the player is airborne after a fresh jump
            // from the ground (i.e., prevOnGround = true, onGround = false, jump pressed).
            // This avoids fighting with multijump (which fires for humans only) and lets
            // the game engine handle the vertical velocity; we add forward impulse on top.
            bool freshLeap = prevOnGround && !onGround && jumpPressed;
            if (!freshLeap) continue;

            // Check cooldown.
            _globals.LeapCooldownEnd.TryGetValue(id, out long cooldownEndMs);
            if (Environment.TickCount64 < cooldownEndMs) continue;

            // Get the player's forward direction from eye angles.
            // In CS2/Source2, QAngle.Y is the yaw (left/right horizontal rotation).
            SwiftlyS2.Shared.Natives.QAngle eyeAngles = pawn.EyeAngles;
            float yawRad = eyeAngles.Y * MathF.PI / 180f;
            float vx = leapForce * MathF.Cos(yawRad);
            float vy = leapForce * MathF.Sin(yawRad);

            // Preserve existing Z velocity (the jump) and add the configured height boost.
            float vz = pawn.AbsVelocity.Z + leapHeight;
            pawn.Teleport(null, null, new SwiftlyS2.Shared.Natives.Vector(vx, vy, vz));

            _globals.LeapCooldownEnd[id] = Environment.TickCount64 + (long)(leapCooldown * 1000);
        }
    }

    private void HandleHumanTakeDamage(IOnEntityTakeDamageEvent @event, in DamageEventContext context)
    {
        var victimPlayer = context.VictimPlayer;
        var AttackerPlayer = context.AttackerPlayer;
        if (victimPlayer == null || !victimPlayer.IsValid || AttackerPlayer == null || !AttackerPlayer.IsValid)
            return;

        var victimPawn = context.VictimPawn;
        var AttackerPawn = context.AttackerPawn;

        if (!TryGetActiveWeapon(AttackerPawn, out var activeWeapon))
            return;

        var attackerId = AttackerPlayer.PlayerID;
        var victimId = victimPlayer.PlayerID;

        var CFG = _mainCFG.CurrentValue;

        _globals.IsZombie.TryGetValue(attackerId, out bool attackerIsZombie);
        _globals.IsZombie.TryGetValue(victimId, out bool victimIsZombie);

        // Cross-check with actual team to resolve any IsZombie state mismatches.
        // Zombies are always on Team.T and humans on Team.CT.
        CCSPlayerController? AttackerController = null;
        CCSPlayerController? victimController = null;
        if (AttackerPawn != null)
        {
            var h = AttackerPawn.Controller;
            if (h.IsValid) AttackerController = h.Value?.As<CCSPlayerController>();
        }
        if (victimPawn != null)
        {
            var h = victimPawn.Controller;
            if (h.IsValid) victimController = h.Value?.As<CCSPlayerController>();
        }
        if (AttackerController != null)
        {
            if (AttackerController.Team == Team.T) attackerIsZombie = true;
            else if (AttackerController.Team == Team.CT) attackerIsZombie = false;
        }
        if (victimController != null)
        {
            if (victimController.Team == Team.T) victimIsZombie = true;
            else if (victimController.Team == Team.CT) victimIsZombie = false;
        }

        if (attackerIsZombie || !victimIsZombie)
            return;

        _globals.IsSurvivor.TryGetValue(attackerId, out bool attackerIsSurvivor);
        _globals.IsSniper.TryGetValue(attackerId, out bool attackerIsSniper);
        _globals.IsHero.TryGetValue(attackerId, out bool attackerIsHero);

        if (attackerIsSurvivor || attackerIsSniper || attackerIsHero)
        {
            var config = _mainCFG.CurrentValue;
            if (attackerIsSurvivor && activeWeapon.DesignerName == config.Survivor.SurvivorWeapon)
            {
                @event.Info.Damage *= config.Survivor.SurvivorDamage;
            }
            else if (attackerIsSniper && activeWeapon.DesignerName == config.Sniper.SniperWeapon)
            {
                if (config.Sniper.OneShotKill && victimPawn != null)
                    @event.Info.Damage = victimPawn.Health;
                else
                    @event.Info.Damage *= config.Sniper.SniperDamage;
            }
            else if (attackerIsHero)
            {
                @event.Info.Damage *= config.Hero.HeroDamage;
            }
        }

        var AmmoType = @event.Info.AmmoType;
        if (AmmoType == -1)
            return;

        float stunTime = CFG.StunZombieTime;
        _helpers.SetZombieFreezeOrStun(victimPlayer, stunTime);

        bool isheadshot = @event.Info.ActualHitGroup == HitGroup_t.HITGROUP_HEAD;

        //_logger.LogInformation($"Damage Info - Attacker: {AttackerPlayer.Name}, Victim: {victimPlayer.Name}, AmmoType: {@event.Info.AmmoType}, IsHeadshot: {isheadshot}");

        var inflictorHandle = @event.Info.Inflictor;
        if (!inflictorHandle.IsValid)
            return;
        var inflictor = inflictorHandle.Value;
        if (inflictor == null || !inflictor.IsValid || !inflictor.IsValidEntity)
            return;

        string inflictorname = inflictor.DesignerName;

        // Do not knock back zombies that are in GodState.
        _globals.GodState.TryGetValue(victimId, out bool victimIsGodState);
        if (!victimIsGodState)
        {
            _helpers.KnockBackZombie(AttackerPlayer, victimPlayer, inflictorname, CFG.KnockZombieForce, isheadshot, CFG);
        }
    }

    private HookResult OnHumanWeaponFire(EventWeaponFire @event)
    {
        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var pawn = @event.UserIdPawn;
        if (pawn == null || !pawn.IsValid)
            return HookResult.Continue;

        var Id = player.PlayerID;
        _globals.IsZombie.TryGetValue(Id, out bool IsZombie);

        // ── Zombie knife → mine proximity damage ──────────────────────────────
        if (IsZombie)
        {
            var knifeWeapon = pawn.WeaponServices?.ActiveWeapon.Value;
            if (knifeWeapon?.DesignerName == "weapon_knife" && _globals.MineCurrentHP.Count > 0)
                CheckZombieMineAttack(player, pawn);
            return HookResult.Continue;
        }
        // ─────────────────────────────────────────────────────────────────────

        _globals.IsSurvivor.TryGetValue(Id, out bool IsSurvivor);
        _globals.IsSniper.TryGetValue(Id, out bool IsSniper);
        _globals.IsHero.TryGetValue(Id, out bool IsHero);
        _globals.InfiniteAmmoState.TryGetValue(Id, out bool IsInfiniteAmmoState);
        _globals.InfiniteClipState.TryGetValue(Id, out bool IsInfiniteClipState);
        _globals.TryderState.TryGetValue(Id, out bool IsTryder);

        var CFG = _mainCFG.CurrentValue;

        var activeWeapon = pawn.WeaponServices?.ActiveWeapon.Value;
        if (activeWeapon == null || !activeWeapon.IsValid)
            return HookResult.Continue;

        if(_helpers.CheckIsGrenade(activeWeapon))
            return HookResult.Continue;

        // Knife blink: trigger on knife fire instead of console command
        if (activeWeapon.DesignerName == "weapon_knife")
        {
            _globals.KnifeBlinkCharges.TryGetValue(Id, out int charges);
            if (charges > 0)
                _extraItemsMenu.TryExecuteKnifeBlink(player);
            return HookResult.Continue;
        }

        if (CFG.EnableInfiniteReserveAmmo && activeWeapon.ReserveAmmo[0] < 1000)
        {
            activeWeapon.ReserveAmmo[0] = 1000;
        }

        // Infinite clip: global game-mode flag, API state, per-player extra item, or Tryder
        bool hasAnyInfiniteClipSource = _globals.GameInfiniteClipMode || IsInfiniteAmmoState || IsInfiniteClipState || IsTryder ||
            (IsSurvivor && activeWeapon.DesignerName == CFG.Survivor.SurvivorWeapon) ||
            (IsSniper && activeWeapon.DesignerName == CFG.Sniper.SniperWeapon) ||
            IsHero;

        if (hasAnyInfiniteClipSource)
        {
            activeWeapon.Clip1 = 100;
            activeWeapon.Clip1Updated();
        }

        return HookResult.Continue;
    }

    private HookResult CheckRoundWinDeath(EventPlayerDeath @event)
    {
        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        if(!_globals.GameStart)
            return HookResult.Continue;

        _service.CheckRoundWinConditions();

        return HookResult.Continue;
    }

    private HookResult CheckRoundWinSpawn(EventPlayerSpawn @event)
    {
        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        if (!_globals.GameStart)
            return HookResult.Continue;

        _service.CheckRoundWinConditions();

        return HookResult.Continue;
    }

    private HookResult RandomSpawn(EventPlayerSpawn @event)
    {
        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return HookResult.Continue;

        var Id = player.PlayerID;

        _globals.IsZombie.TryGetValue(Id, out var isZombie);

        _service.RandomSpawnPoint(player, !isZombie);

        return HookResult.Continue;
    }

    /// <summary>
    /// When a zombie swings their knife, check for any placed mines within melee range.
    /// Deals ZombieAttackDamage to each mine in range (may trigger explosion when HP → 0).
    /// </summary>
    private void CheckZombieMineAttack(IPlayer player, CCSPlayerPawn pawn)
    {
        var origin = pawn.AbsOrigin;
        if (origin == null) return;

        float range   = _mineCFG.CurrentValue.ZombieAttackRange;
        float rangeSq = range * range;

        foreach (var kvp in _globals.MineCurrentHP)
        {
            uint mineRaw = kvp.Key;
            if (!_globals.MineData.TryGetValue(mineRaw, out var mineData)) continue;
            if (mineData.MineHealth <= 0 || mineData.ZombieAttackDamage <= 0) continue;

            var sp = mineData.SpawnOrigin;
            float dx = origin.Value.X - sp.X;
            float dy = origin.Value.Y - sp.Y;
            float dz = origin.Value.Z - sp.Z;
            if (dx * dx + dy * dy + dz * dz <= rangeSq)
                _mineService.HandleMineDamage(mineRaw, mineData.ZombieAttackDamage, player);
        }
    }

    private HookResult OnGrenadeThrown(EventGrenadeThrown @event)
    {
        if (!_globals.GameStart)
            return HookResult.Continue;

        var Thrower = @event.UserIdPlayer;
        if (Thrower == null || !Thrower.IsValid)
            return HookResult.Continue;

        var ThrowerId = Thrower.PlayerID;

        _globals.IsZombie.TryGetValue(ThrowerId, out bool isZombie);
        _globals.ThrowerIsZombie[ThrowerId] = isZombie;

        return HookResult.Continue;
    }
    private HookResult OnGrenadeDetonate(EventHegrenadeDetonate @event)
    {
        if(!_globals.GameStart)
            return HookResult.Continue;

        var Thrower = @event.UserIdPlayer;
        if(Thrower == null || !Thrower.IsValid)
            return HookResult.Continue;

        var entityId = @event.EntityID;
        var entity = _core.EntitySystem.GetEntityByIndex<CHEGrenadeProjectile>((uint)entityId);
        if (entity == null || !entity.IsValid)
            return HookResult.Continue;

        var ThrowerId = Thrower.PlayerID;

        var CFG = _mainCFG.CurrentValue;

        if (_globals.ThrowerIsZombie.TryGetValue(ThrowerId, out bool throwerIsZombie) && throwerIsZombie)
        {
            _globals.ThrowerIsZombie.Remove(ThrowerId);

            SwiftlyS2.Shared.Natives.Vector position = new SwiftlyS2.Shared.Natives.Vector(@event.X, @event.Y, @event.Z);
            float radius = CFG.TVirusGrenadeRange;
            _helpers.DrawExpandingRing(position, radius, 0, 255, 0, 125);

            using var sound = new SwiftlyS2.Shared.Sounds.SoundEvent(CFG.TVirusGrenadeSound, 1.0f, 1.0f);
            sound.SourceEntityIndex = (int)entity.Index;
            sound.Recipients.AddAllPlayers();
            sound.Emit();

            var allPlayer = _core.PlayerManager.GetAlive();
            foreach (var human in allPlayer)
            {
                if (human == null || !human.IsValid)
                    continue;

                _globals.IsZombie.TryGetValue(human.PlayerID, out bool isZombie);
                if (isZombie)
                    continue;

                _globals.IsHero.TryGetValue(human.PlayerID, out bool isHero);
                _globals.IsSniper.TryGetValue(human.PlayerID, out bool isSniper);
                _globals.IsSurvivor.TryGetValue(human.PlayerID, out bool isSurvivor);
                if (!CFG.TVirusCanInfectHero && (isHero || isSniper || isSurvivor))
                    continue;

                var pawn = human.PlayerPawn;
                if (pawn == null || !pawn.IsValid)
                    continue;
                // 计算玩家和爆炸位置的距离
                var humanPos = pawn.AbsOrigin;
                if (humanPos == null)
                    continue;

                float distance = MathF.Sqrt(
                    MathF.Pow(humanPos.Value.X - position.X, 2) +
                    MathF.Pow(humanPos.Value.Y - position.Y, 2) +
                    MathF.Pow(humanPos.Value.Z - position.Z, 2)
                );

                if (distance <= radius)
                {
                    _service.Infect(Thrower, human, true);
                }
            }
        }
        else
        {
            _globals.ThrowerIsZombie.Remove(ThrowerId);

            if(!CFG.FireGrenade)
                return HookResult.Continue;

            SwiftlyS2.Shared.Natives.Vector position = new SwiftlyS2.Shared.Natives.Vector(@event.X, @event.Y, @event.Z);
            float radius = CFG.FireGrenadeRange;
            _helpers.DrawExpandingRing(position, radius, 255, 0, 0, 125);

            using var sound = new SwiftlyS2.Shared.Sounds.SoundEvent(CFG.FireGrenadeSound, 1.0f, 1.0f);
            sound.SourceEntityIndex = (int)entity.Index;
            sound.Recipients.AddAllPlayers();
            sound.Emit();

            var allPlayer = _core.PlayerManager.GetAlive();
            foreach (var zombie in allPlayer)
            {
                if (zombie == null || !zombie.IsValid)
                    continue;

                _globals.IsZombie.TryGetValue(zombie.PlayerID, out bool isZombie);
                if (!isZombie)
                    continue;

                var pawn = zombie.PlayerPawn;
                if (pawn == null || !pawn.IsValid)
                    continue;
                // 计算玩家和爆炸位置的距离
                var zombiePos = pawn.AbsOrigin;
                if (zombiePos == null)
                    continue;

                float distance = MathF.Sqrt(
                    MathF.Pow(zombiePos.Value.X - position.X, 2) +
                    MathF.Pow(zombiePos.Value.Y - position.Y, 2) +
                    MathF.Pow(zombiePos.Value.Z - position.Z, 2)
                );

                if (distance <= radius)
                {
                    var zombieConfig = _zombieClassCFG.CurrentValue;
                    var specialConfig = _SpecialClassCFG.CurrentValue;
                    var zombieclass = _zombieState.GetZombieClass(zombie.PlayerID, zombieConfig.ZombieClassList, specialConfig.SpecialClassList);
                    if (zombieclass == null)
                        continue;

                    _helpers.StartIgnite(Thrower, zombie, CFG.FireGrenadeDmg, CFG.FireDmg, CFG.FireGrenadeDuration, zombieclass.Sounds.BurnSound, zombieclass.Stats.ZombieSoundVolume);
                }
            }

        }
        return HookResult.Continue;
    }

    /// <summary>
    /// Suppresses the player_footstep event for zombie classes with SilentSteps=true (Hunter).
    /// Returns HookResult.Stop to prevent the footstep sound from broadcasting to other players.
    /// </summary>
    private HookResult OnPlayerFootstep(EventPlayerFootstep @event)
    {
        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid || player.IsFakeClient)
            return HookResult.Continue;

        int id = player.PlayerID;
        _globals.IsZombie.TryGetValue(id, out bool isZombie);
        if (!isZombie)
            return HookResult.Continue;

        // Stop footstep sound for silent-step zombie classes
        if (_globals.SilentStepsActive.Contains(id))
            return HookResult.Stop;

        return HookResult.Continue;
    }

    private HookResult OnPlayerBlind(EventPlayerBlind @event)
    {
        var player = @event.UserIdPlayer;
        if(player == null || !player.IsValid)
            return HookResult.Continue;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return HookResult.Continue;

        pawn.BlindUntilTime.Value = _core.Engine.GlobalVars.CurrentTime;

        return HookResult.Continue;
    }
    private HookResult OnFlashbangDetonate(EventFlashbangDetonate @event)
    {
        var entityId = @event.EntityID;
        var entity = _core.EntitySystem.GetEntityByIndex<CFlashbangProjectile>((uint)entityId);
        if (entity == null || !entity.IsValid)
            return HookResult.Continue;

        var CFG = _mainCFG.CurrentValue;
        if(!CFG.LightGrenade)
            return HookResult.Continue;

        float Duration = CFG.LightGrenadeDuration;
        float range = CFG.LightGrenadeRange;
        SwiftlyS2.Shared.Natives.Vector position = new SwiftlyS2.Shared.Natives.Vector(@event.X, @event.Y, @event.Z);

        var light = _helpers.CreateLight(position, range, 255, 255, 255, 255, CFG.LightGrenadeSound);
        if (light == null || !light.IsValid)
            return HookResult.Continue;

        var lightIndex = light.Index;
        _globals.activeLights[lightIndex] = _core.EntitySystem.GetRefEHandle(light);
        _globals.lightTimers[lightIndex] = _core.Scheduler.DelayBySeconds(Duration, () => 
        {
            _helpers.RemoveLight(lightIndex);
        });

        return HookResult.Continue;
    }

    private HookResult OnSmokegrenadeDetonate(EventSmokegrenadeDetonate @event)
    {
        var entityId = @event.EntityID;
        var entity = _core.EntitySystem.GetEntityByIndex<CSmokeGrenadeProjectile>((uint)entityId);
        if (entity == null || !entity.IsValid)
            return HookResult.Continue;

        var CFG = _mainCFG.CurrentValue;

        if(!CFG.FreezeGrenade)
            return HookResult.Continue;


        SwiftlyS2.Shared.Natives.Vector position = new SwiftlyS2.Shared.Natives.Vector(@event.X, @event.Y, @event.Z);
        float radius = 500f;
        _helpers.DrawExpandingRing(position, radius, 0, 0, 255, 125);
        using var sound = new SwiftlyS2.Shared.Sounds.SoundEvent(CFG.FreezeGrenadeSound, 1.0f, 1.0f);
        sound.SourceEntityIndex = (int)entity.Index;
        sound.Recipients.AddAllPlayers();
        sound.Emit();


        var allPlayer = _core.PlayerManager.GetAlive();
        foreach (var zombie in allPlayer)
        {
            if (zombie == null || !zombie.IsValid)
                continue;

            _globals.IsZombie.TryGetValue(zombie.PlayerID, out bool isZombie);
            if (!isZombie)
                continue;

            var pawn = zombie.PlayerPawn;
            if (pawn == null || !pawn.IsValid)
                continue;
            // 计算玩家和爆炸位置的距离
            var zombiePos = pawn.AbsOrigin;
            if (zombiePos == null)
                continue;

            float distance = MathF.Sqrt(
                MathF.Pow(zombiePos.Value.X - position.X, 2) +
                MathF.Pow(zombiePos.Value.Y - position.Y, 2) +
                MathF.Pow(zombiePos.Value.Z - position.Z, 2)
            );

            if (distance <= radius)
            {
                _helpers.SetZombieFreezeOrStun(zombie, CFG.FreezeGrenadeDuration, "Glass.BulletImpact");
            }
        }

        if (entity != null && entity.IsValid && entity.IsValidEntity)
        {
            entity.AcceptInput("kill", 0);
        }
        return HookResult.Continue;

    }

    private static bool IsTeleportDestinationWithinBounds(SwiftlyS2.Shared.Natives.Vector origin, SwiftlyS2.Shared.Natives.Vector destination)
    {
        float dx = destination.X - origin.X;
        float dy = destination.Y - origin.Y;
        float dz = destination.Z - origin.Z;
        float dist2d = MathF.Sqrt(dx * dx + dy * dy);

        return destination.Z > -8192f && destination.Z < 16384f && dist2d <= 2500f && dz >= -128f;
    }

    private bool IsTeleportDestinationClear(IPlayer player, SwiftlyS2.Shared.Natives.Vector destination)
    {
        foreach (var other in _core.PlayerManager.GetAllPlayers())
        {
            if (other == null || !other.IsValid || other.PlayerID == player.PlayerID)
                continue;

            var otherPawn = other.PlayerPawn;
            if (otherPawn == null || !otherPawn.IsValid)
                continue;

            var otherPos = otherPawn.AbsOrigin;
            if (otherPos == null)
                continue;

            float ox = otherPos.Value.X - destination.X;
            float oy = otherPos.Value.Y - destination.Y;
            float oz = MathF.Abs(otherPos.Value.Z - destination.Z);
            if ((ox * ox + oy * oy) <= (26f * 26f) && oz <= 72f)
                return false;
        }

        return true;
    }

    private HookResult OnDecoyFiring(EventDecoyFiring @event)
    {
        var entityId = @event.EntityID;
        var entity = _core.EntitySystem.GetEntityByIndex<CDecoyProjectile>((uint)entityId);
        if (entity == null || !entity.IsValid)
            return HookResult.Continue;

        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var CFG = _mainCFG.CurrentValue;

        if (!CFG.TelportGrenade)
            return HookResult.Continue;

        SwiftlyS2.Shared.Natives.Vector position = new SwiftlyS2.Shared.Natives.Vector(@event.X, @event.Y, @event.Z + 18f);

        var id = player.PlayerID;
        _globals.IsZombie.TryGetValue(id, out bool isZombie);
        if (!isZombie)
        {
            var pawn = player.PlayerPawn;
            if (pawn != null && pawn.IsValid)
            {
                var current = pawn.AbsOrigin;
                if (current != null)
                {
                    var candidates = new[]
                    {
                        new SwiftlyS2.Shared.Natives.Vector(position.X, position.Y, position.Z + 12f),
                        new SwiftlyS2.Shared.Natives.Vector(position.X + 18f, position.Y, position.Z + 14f),
                        new SwiftlyS2.Shared.Natives.Vector(position.X - 18f, position.Y, position.Z + 14f),
                        new SwiftlyS2.Shared.Natives.Vector(position.X, position.Y + 18f, position.Z + 14f),
                        new SwiftlyS2.Shared.Natives.Vector(position.X, position.Y - 18f, position.Z + 14f)
                    };

                    SwiftlyS2.Shared.Natives.Vector? selected = null;
                    foreach (var candidate in candidates)
                    {
                        if (!IsTeleportDestinationWithinBounds(current.Value, candidate))
                            continue;
                        if (!IsTeleportDestinationClear(player, candidate))
                            continue;
                        selected = candidate;
                        break;
                    }

                    if (selected != null)
                    {
                        var eyeAngles = pawn.EyeAngles;
                        pawn.Teleport(selected.Value, eyeAngles, SwiftlyS2.Shared.Natives.Vector.Zero);
                        _helpers.ClearFreezeStaten(player);

                        // Post-teleport pass to reduce chances of collision lock.
                        _core.Scheduler.NextTick(() =>
                        {
                            if (player == null || !player.IsValid) return;
                            var p = player.PlayerPawn;
                            if (p == null || !p.IsValid) return;
                            var posNow = p.AbsOrigin;
                            if (posNow == null) return;
                            p.Teleport(new SwiftlyS2.Shared.Natives.Vector(posNow.Value.X, posNow.Value.Y, posNow.Value.Z + 6f), p.EyeAngles, SwiftlyS2.Shared.Natives.Vector.Zero);
                            _helpers.ClearFreezeStaten(player);
                        });
                    }
                    else
                    {
                        _helpers.SendChatT(player, "TeleportGrenadeUnsafeDestination");
                    }
                }
            }
        }
        if (entity != null && entity.IsValid && entity.IsValidEntity)
        {
            entity.AcceptInput("kill", 0);
        }
        return HookResult.Continue;

    }

}
