using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Helpers;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.SteamAPI;
using static ZombiePlagueLegacyCS2.ZPLZombieClassCFG;


namespace ZombiePlagueLegacyCS2;

public partial class ZPLServices
{
    private readonly ILogger<ZPLServices> _logger;
    private readonly ISwiftlyCore _core;
    private readonly ZPLGlobals _globals;
    private readonly ZPLHelpers _helpers;
    private readonly IOptionsMonitor<ZPLMainCFG> _mainCFG;
    private readonly IOptionsMonitor<ZPLZombieClassCFG> _zombieClassCFG;
    private readonly IOptionsMonitor<ZPLSpecialClassCFG> _specialClassCFG;
    private readonly PlayerZombieState _zombieState;
    private readonly ZPLGameMode _gameMode;

    private readonly ZombiePlagueLegacyAPI _api;
    private readonly ZPLWeaponsMenu _weaponsMenu;
    private readonly ZPLMineService _mineService;
    private readonly ZPLClassAbilities _classAbilities;
    private ZPLExtraItemsMenu? _extraItemsMenu;
    public ZPLServices(ISwiftlyCore core, ILogger<ZPLServices> logger,
        ZPLGlobals globals, ZPLHelpers helpers,
        IOptionsMonitor<ZPLMainCFG> mainCFG,
        IOptionsMonitor<ZPLZombieClassCFG> zombieClassCFG,
        PlayerZombieState zombieState, ZPLGameMode gameMode,
        IOptionsMonitor<ZPLSpecialClassCFG> specialClassCFG,
        ZombiePlagueLegacyAPI api,
        ZPLWeaponsMenu weaponsMenu,
        ZPLMineService mineService,
        ZPLClassAbilities classAbilities)
    {
        _core = core;
        _logger = logger;
        _globals = globals;
        _helpers = helpers;
        _mainCFG = mainCFG;
        _zombieClassCFG = zombieClassCFG;
        _zombieState = zombieState;
        _gameMode = gameMode;
        _specialClassCFG = specialClassCFG;
        _api = api;
        _weaponsMenu = weaponsMenu;
        _mineService = mineService;
        _classAbilities = classAbilities;
    }

    // Post-construction wiring — called from plugin entry after DI resolves
    public void SetExtraItemsMenu(ZPLExtraItemsMenu menu) => _extraItemsMenu = menu;

    public void SelectMotherZombie(int count)
    {
        var allplayer = _core.PlayerManager.GetAllPlayers();
        var candidates = new List<IPlayer>();

        foreach (var p in allplayer)
        {
            if (p == null || !p.IsValid)
                continue;

            var id = p.PlayerID;
            bool isZombie = false;
            _globals.IsZombie.TryGetValue(id, out isZombie);
            if (isZombie)
                continue;

            candidates.Add(p);
        }

        if (candidates.Count == 0)
            return;

        var cfg = _mainCFG.CurrentValue;
        if (candidates.Count < cfg.MinPlayersForInfection)
            return;

        // Build a preferred pool that excludes players who were a special role last
        // round (anti-repeat).  Fall back to the full candidate list only if the
        // preferred pool doesn't have enough players.
        var lastRound = _globals.SpecialRoleLastRound;
        var preferred = candidates
            .Where(p => p.SteamID == 0 || !lastRound.Contains(p.SteamID))
            .ToList();
        var pool = preferred.Count >= count ? preferred : candidates;
        Random.Shared.Shuffle(CollectionsMarshal.AsSpan(pool));

        // 确保不超过候选人数
        int actualCount = Math.Min(count, pool.Count);

        var zombieConfig = _zombieClassCFG.CurrentValue;
        var zombieClasses = zombieConfig.ZombieClassList;

        for (int i = 0; i < actualCount; i++)
        {
            var target = pool[i];
            if (target == null || !target.IsValid)
                continue;

            SetupMotherZombie(target);
            _globals.MotherZombieWasSelected = true;
            _helpers.SendChatToAllT("GameInfoBecomeMother", target.Name);

            // Record for next round's anti-repeat
            if (target.SteamID != 0)
                _globals.SpecialRoleThisRound.Add(target.SteamID);
        }
    }

    public void Infect(IPlayer attacker, IPlayer victim, bool grenade)
    {
        if (attacker == null || !attacker.IsValid)
            return;

        if (victim == null || !victim.IsValid)
            return;

        _helpers.RemoveGlow(victim);

        var attackerId = attacker.PlayerID;
        var victimId = victim.PlayerID;
        var victimsteamId = victim.SteamID;

        var CFG = _mainCFG.CurrentValue;

        _globals.IsZombie.TryGetValue(attackerId, out bool attackerIsZombie);
        _globals.IsZombie.TryGetValue(victimId, out bool victimIsZombie);
        if (attackerIsZombie && !victimIsZombie)
        {
            _globals.ScbaSuit.TryGetValue(victimId, out bool IsHaveScbaSuit);
            if (IsHaveScbaSuit && CFG.CanUseScbaSuit)
            {
                _helpers.RemoveScbaSuit(victim, CFG.ScbaSuitBrokenSound);
                return;
            }

            var zombieConfig = _zombieClassCFG.CurrentValue;
            var zombieClasses = zombieConfig.ZombieClassList;

            // 根据玩家偏好选择类
            var preference = _zombieState.GetPlayerPreference(victimId, victimsteamId);
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
                PlayerSelectSoundtoAll(selectedClass.Sounds.SoundInfect, selectedClass.Stats.ZombieSoundVolume);
                posszombie(victim, selectedClass, false);
                CreateFakeKill(attacker, victim, grenade);
                CheckRoundWinConditions();

                // Give the attacker HP for successfully infecting a human.
                int hpReward = CFG.ZombieInfectHealthReward;
                if (hpReward > 0)
                {
                    var attackerPawn = attacker.PlayerPawn;
                    if (attackerPawn != null && attackerPawn.IsValid)
                    {
                        attackerPawn.Health = Math.Min(attackerPawn.Health + hpReward, attackerPawn.MaxHealth);
                        attackerPawn.HealthUpdated();
                    }
                }

                // ── Class on-infect ability (heal, glow) for the infector ──
                _classAbilities.OnInfectAbility(attacker);

                if (_api != null)
                    _api.NotifyInfect(attacker, victim, grenade, selectedClass.Name);
            }
        }

        SetupHero();
    }

    public void ForceCommandInfect(IPlayer Infecter, bool IgnoreScbaSuit)
    {
        if (Infecter == null || !Infecter.IsValid)
            return;

        _helpers.RemoveGlow(Infecter);

        var InfecterId = Infecter.PlayerID;
        var InfectersteamId = Infecter.SteamID;
        var CFG = _mainCFG.CurrentValue;
        _globals.IsZombie.TryGetValue(InfecterId, out bool InfecterIsZombie);

        if (InfecterIsZombie)
            return;

        
        _globals.ScbaSuit.TryGetValue(InfecterId, out bool IsHaveScbaSuit);
        if (IsHaveScbaSuit && CFG.CanUseScbaSuit && !IgnoreScbaSuit)
        {
            _helpers.RemoveScbaSuit(Infecter, CFG.ScbaSuitBrokenSound);
            return;
        }

        var zombieConfig = _zombieClassCFG.CurrentValue;
        var zombieClasses = zombieConfig.ZombieClassList;

        // 根据玩家偏好选择类
        var preference = _zombieState.GetPlayerPreference(InfecterId, InfectersteamId);
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
            PlayerSelectSoundtoAll(selectedClass.Sounds.SoundInfect, selectedClass.Stats.ZombieSoundVolume);
            posszombie(Infecter, selectedClass, false);
            CreateFakeKill(Infecter, Infecter, false);
            CheckRoundWinConditions();

            if (_api != null)
                _api.NotifyInfect(Infecter, Infecter, false, selectedClass.Name);
        }
        

        SetupHero();
    }



    public void SetPlayerZombie(IPlayer player)
    {
        if (!player.IsValid)
            return;
        var Id = player.PlayerID;
        var steamId = player.SteamID;

        _helpers.RemoveSHumanClass(Id);
        _helpers.RemoveSZombieClass(Id);
        _helpers.RemoveGlow(player);

        _globals.IsZombie.TryGetValue(Id, out bool IsZombie);
        if (!IsZombie)
        {
            var zombieConfig = _zombieClassCFG.CurrentValue;
            var zombieClasses = zombieConfig.ZombieClassList;

            // 根据玩家偏好选择类
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
                PlayerSelectSoundtoAll(selectedClass.Sounds.SoundInfect, selectedClass.Stats.ZombieSoundVolume);
                posszombie(player, selectedClass, false);
                CreateFakeKill(player, player, false);
                CheckRoundWinConditions();
            }
        }

        SetupHero();
    }

    public void SetPlayerHuman(IPlayer player)
    {
        if (!player.IsValid)
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        var Id = player.PlayerID;

        _globals.IsZombie.TryGetValue(Id, out bool IsZombie);
        if (!IsZombie)
            return;

        var CFG = _mainCFG.CurrentValue;

        _helpers.RemoveSZombieClass(Id);

        _globals.IsZombie[Id] = false;
        player.SwitchTeam(Team.CT);
        _helpers.ChangeKnife(player, false, false);
        _helpers.SetFov(player, 90);
        _helpers.ClearPlayerBurn(Id);
        _helpers.ClearFreezeStaten(player);


        string Default = "characters/models/ctm_st6/ctm_st6_variante.vmdl";
        string Custom = string.IsNullOrEmpty(CFG.HumandefaultModel) ? Default : CFG.HumandefaultModel;

        _core.Scheduler.NextWorldUpdate(() =>
        {
            if (pawn.IsValid)
                pawn.SetModel(Custom);
        });

        var maxHealth = CFG.HumanMaxHealth;
        pawn.MaxHealth = maxHealth;
        pawn.MaxHealthUpdated();
        pawn.Health = maxHealth;
        pawn.HealthUpdated();

        pawn.VelocityModifier = CFG.HumanInitialSpeed;
        pawn.VelocityModifierUpdated();

        _helpers.EmitSoundFormPlayer(player, CFG.TVaccineSound, 1.0f);
        CheckRoundWinConditions();
    }


    public void FakeHumanWins()
    {
        _globals.GameStart = false;

        if (_globals.RoundVoxGroup != null)
        {
            PlayerSelectSoundtoAll(_globals.RoundVoxGroup.HumanWinVox, _globals.RoundVoxGroup.Volume);
        }

        _helpers.SendCenterHTMLLocalizedToAll(p =>
            $"<b><span color='#00FF7F' class='fontSize-xl'>{_helpers.T(p, "ServerGameHumanWin")}</span></b>",
            duration: 4000);
        _helpers.SetTeamScore(Team.CT);
        _helpers.TerminateRound(RoundEndReason.CTsWin, 5.0f);

        if (_api != null)
            _api.NotifyHumanWin(true);
    }

    
    
    public void FakeZombieWins()
    {
        _globals.GameStart = false;

        if (_globals.RoundVoxGroup != null)
        {
            PlayerSelectSoundtoAll(_globals.RoundVoxGroup.ZombieWinVox, _globals.RoundVoxGroup.Volume);
        }
        _helpers.SendCenterHTMLLocalizedToAll(p =>
            $"<b><span color='#FF3030' class='fontSize-xl'>{_helpers.T(p, "ServerGameZombieWin")}</span></b>",
            duration: 4000);
        _helpers.SetTeamScore(Team.T);
        _helpers.TerminateRound(RoundEndReason.TerroristsWin, 5.0f);

        if (_api != null)
            _api.NotifyHumanWin(false);
    }

    public void posszombie(IPlayer zombie, ZombieClass Zclass, bool isMother)
    {
        try
        {
            if(!_globals.GameStart)
                return;

            if (zombie == null || !zombie.IsValid)
                return;

            var controller = zombie.Controller;
            if (controller == null || !controller.IsValid)
                return;

            //_logger.LogInformation($"posszombie 开始 [{controller.PlayerName}]: {Zclass.Name}");

            if (Zclass == null)
                return;

            var pawn = zombie.PlayerPawn;
            if (pawn == null || !pawn.IsValid)
                return;

            //_logger.LogInformation($"生成丧尸 {Zclass.Name}");

            var Id = zombie.PlayerID;

            _helpers.RemoveSHumanClass(Id);

            var CFG = _mainCFG.CurrentValue;
            _helpers.RemoveScbaSuit(zombie, CFG.ScbaSuitBrokenSound);
            _helpers.RemoveGodState(zombie);
            _helpers.RemoveInfiniteAmmo(zombie);

            // Disable human-only extra items when player becomes zombie
            _globals.HasJetpack.Remove(Id);
            _globals.JetpackFuel.Remove(Id);
            _globals.JetpackLastFuelTime.Remove(Id);
            _globals.HasReviveToken.Remove(Id);
            _globals.ExtraJumps.Remove(Id);
            _globals.KnifeBlinkCharges.Remove(Id);
            _globals.KnifeBlinkCooldownEnd.Remove(Id);

            // Close any open menu (e.g. mine placement menu) before the team switch.
            _core.MenusAPI.CloseActiveMenu(zombie);

            // Kill all mines placed by this player.
            _mineService.CleanupMinesForPlayer(zombie.SteamID);
            
            _globals.IsZombie[Id] = true;
            zombie.SwitchTeam(Team.T);

            _helpers.ShakeZombie(zombie);

            _zombieState.SetPlayerZombieClass(Id, Zclass.Name);

            string path = Zclass.Models.ModelPath;
            _core.Scheduler.NextWorldUpdate(() =>
            {
                pawn.SetModel(path);
            });
            
            _helpers.DropAllWeapon(zombie);

            bool CustomKinfe = !string.IsNullOrEmpty(Zclass.Models.CustomKinfeModelPath);
            _helpers.ChangeKnife(zombie, true, CustomKinfe);


            int ZHealth;
            if (isMother)
            {
                ZHealth = Zclass.Stats.MotherZombieHealth > 0 ? ZHealth = Zclass.Stats.MotherZombieHealth : ZHealth = 8000;
            }
            else
            {
                ZHealth = Zclass.Stats.Health > 0 ? ZHealth = Zclass.Stats.Health : ZHealth = 3000;
            }
            pawn.MaxHealth = ZHealth;
            pawn.MaxHealthUpdated();
            pawn.Health = ZHealth;
            pawn.HealthUpdated();

            pawn.ActualGravityScale = Zclass.Stats.Gravity;

            int fov = Zclass.Stats.Fov;
            _helpers.SetFov(zombie, fov);

            float zSpeed = Zclass.Stats.Speed > 0 ? zSpeed = Zclass.Stats.Speed : zSpeed = 1.0f;
            pawn.VelocityModifier = zSpeed;
            pawn.VelocityModifierUpdated();

            if (Zclass.Stats.EnableRegen)
            {
                var now = Environment.TickCount / 1000f;
                _globals.g_ZombieRegenStates[Id] = new ZombieRegenState
                {
                    PlayerID = Id,
                    RegenAmount = Zclass.Stats.HpRegenHp,
                    RegenInterval = Zclass.Stats.HpRegenSec,
                    NextRegenTime = now + Zclass.Stats.HpRegenSec 
                };
            }

            var origin = pawn.AbsOrigin;
            if (origin == null)
                return;

            Vector offsetPos = new(origin.Value.X, origin.Value.Y, origin.Value.Z + 50);
            var particle = _helpers.CreateParticleAtPos(pawn, offsetPos, "particles/explosions_fx/explosion_hegrenade_water_intial_trail.vpcf");

            // ── Mutation HUD: "YOU HAVE MUTATED TO / ZOMBIE LEAPER" ──────────
            if (!zombie.IsFakeClient)
            {
                string mutLabel  = _helpers.T(zombie, isMother ? "YouAreMother" : "YouHaveMutatedTo");
                string className = Zclass.Name.ToUpperInvariant();
                zombie.SendCenterHTML(
                    $"<b><span color='#FF4444'>{mutLabel}</span></b><br>" +
                    $"<b><span color='#00FF7F' class='fontSize-l'>{className}</span></b>", 3000);
            }

            // ── Class special abilities (silent steps, extra jumps) ───────────
            _classAbilities.OnBecomeZombie(zombie, Zclass);

            //_logger.LogInformation($"posszombie 完成 [{controller.PlayerName}]");
        }
        catch (Exception ex)
        {
            var controller = zombie.Controller;
            if (controller == null || !controller.IsValid)
                return;

            _logger.LogError($"posszombie 异常 [{controller.PlayerName}]: {ex.Message}");
            _logger.LogError($"异常堆栈: {ex.StackTrace}");
            _logger.LogError($"僵尸类模型: {Zclass?.Models}");
        }
    }


    public void SetRoundEndTime()
    {
        var cvar = _core.ConVar.Find<float>("mp_roundtime");
        float roundSeconds = (cvar?.Value ?? 3) * 60f;
        _globals.g_hRoundEndTimer?.Cancel();
        _globals.g_hRoundEndTimer = null;

        _globals.g_hRoundEndTimer = _core.Scheduler.DelayBySeconds(roundSeconds, () =>
        {
            FakeHumanWins();
        });
    }

    public void CreateFakeKill(IPlayer attacker, IPlayer victim, bool grenade)
    {
        if (attacker == null || !attacker.IsValid)
            return;

        if (victim == null || !victim.IsValid)
            return;

        string weapon = grenade ? "hegrenade" : "knife";
        _core.GameEvent.Fire<EventPlayerDeath>(@event =>
        {
            @event.Attacker = attacker.PlayerID;
            @event.UserId = victim.PlayerID;
            @event.Weapon = weapon;
        });
    }


    public void JoinTeamCheck(IPlayer player)
    {
        if (player is not { IsValid: true } || player.Controller is not { IsValid: true } ctrl)
            return;

        if (!_globals.GameStart)
        {
            if (!ctrl.PawnIsAlive)
            {
                ctrl.Respawn();
            }
            return;
        }
    }

    public void Round_Countdown()
    {
        var CFG = _mainCFG.CurrentValue;
        int currentDisplay = _globals.Countdown;

        if (_globals.Countdown > 0)
            _globals.Countdown--;

        if (currentDisplay <= 10 && currentDisplay >= 1 && _globals.RoundVoxGroup != null)
        {
            var soundList = _globals.RoundVoxGroup.CountDownVox.Split(',');

            int soundIndex = currentDisplay - 1;

            if (soundIndex >= 0 && soundIndex < soundList.Length)
            {
                CheckEndTimer();
                _helpers.EmitSoundToAll(soundList[soundIndex].Trim(), _globals.RoundVoxGroup.Volume);
            }
        }
        else if (currentDisplay == 20 && _globals.RoundVoxGroup != null)
        {

            PlayerSelectSoundtoAll(_globals.RoundVoxGroup.SecRemainVox, _globals.RoundVoxGroup.Volume);
        }

        if (currentDisplay <= 0)
        {
            _globals.g_hCountdown?.Cancel();
            _globals.g_hCountdown = null;

            _globals.GameStart = true;

            if (_api != null)
                _api.NotifyGameStart(_globals.GameStart);

            _globals.GameInfiniteClipMode = _gameMode.InfiniteClipMode();
            CheckEndTimer();
            SwitchMode();
            _helpers.SetAmbSounds(CFG, _globals);

            var modeVox = _gameMode.SelectModeVox();
            if (_globals.RoundVoxGroup != null && !string.IsNullOrEmpty(modeVox))
            {
                PlayerSelectSoundtoAll(modeVox, _globals.RoundVoxGroup.Volume);
            }
            _core.Scheduler.DelayBySeconds(1.0f, () => 
            {
                if (_globals.GameStart)
                {
                    CheckRoundWinConditions();
                }
            });
            return;
        }
        // Build progress bar — pass a player so localizer can resolve translation chars
        var anyPlayer = _core.PlayerManager.GetAllPlayers()
            .FirstOrDefault(p => p.IsValid && !p.IsFakeClient);
        float cdProgress    = Math.Clamp((float)currentDisplay / 60f, 0f, 1f);
        string bar          = _helpers.BuildProgressBar(cdProgress, 12, "#FF8C00", "#666666", anyPlayer);
        string countdownHtml =
            $"<span color='#FF8C00' class='fontSize-l'><b>COUNTDOWN {currentDisplay}</b></span><br>" +
            bar;
        _core.PlayerManager.SendCenterHTML(countdownHtml, 1100);
    }

    public void CheckEndTimer()
    {
        if (_globals.g_hRoundEndTimer == null)
        {
            _helpers.restartgame();
        }
    }


    public void PlayerSelectSoundtoAll(string soundevent, float Volume)
    {
        if (_globals.RoundVoxGroup != null && !string.IsNullOrWhiteSpace(soundevent))
        {
            var sound = _helpers.RandomSelectSound(soundevent);
            if (sound != null)
            {
                _helpers.EmitSoundToAll(sound, Volume);
            }
        }
    }

    public void PlayerSelectSoundtoEntity(IPlayer player, string soundevent, float Volume)
    {
        if (_globals.RoundVoxGroup != null && !string.IsNullOrWhiteSpace(soundevent))
        {
            var sound = _helpers.RandomSelectSound(soundevent);
            if (sound != null)
            {
                _helpers.EmitSoundFormPlayer(player, sound, Volume);
            }
        }
    }

    public void CheckRoundWinConditions()
    {
        // Win conditions only apply once a mother zombie has been selected.
        // Without this guard a solo player on the server would trigger an
        // immediate round-end the moment infection "starts" with nobody left
        // to oppose them (humanCount == 0 or zombieCount == 0 with 1 player).
        if (!_globals.MotherZombieWasSelected)
            return;

        var allPlayers = _core.PlayerManager.GetAlive();
        int zombieCount = 0;
        int humanCount = 0;

        foreach (var p in allPlayers)
        {
            if (p == null || !p.IsValid)
                continue;

            if (p.PlayerPawn == null || !p.PlayerPawn.IsValid)
                continue;

            var controller = p.Controller;
            if (controller == null || !controller.IsValid)
                continue;

            if (!controller.PawnIsAlive)
                continue;

            var Id = p.PlayerID;
            _globals.IsZombie.TryGetValue(Id, out bool IsZombie);

            if (IsZombie)
                zombieCount++;
            else
                humanCount++;
        }

        if (zombieCount == 0)
            FakeHumanWins();
        else if (humanCount == 0)
            FakeZombieWins();
    }

    public void ZombieRegenTimer()
    {
        _globals.g_ZombieRegenTimer?.Cancel();
        _globals.g_ZombieRegenTimer = null;
        _globals.g_ZombieRegenTimer = _core.Scheduler.RepeatBySeconds(0.2f, () =>
        {

            int now = Environment.TickCount / 1000;
            var allalive = _core.PlayerManager.GetAlive();
            foreach (var player in allalive)
            {
                try
                {
                    var Id = player.PlayerID;
                    _globals.IsZombie.TryGetValue(Id, out bool IsZombie);

                    if (!IsZombie)
                        continue;


                    if (!_globals.g_ZombieRegenStates.TryGetValue(Id, out var state))
                        continue;

                    var pawn = player.PlayerPawn;
                    if (pawn == null || !pawn.IsValid)
                        continue;

                    int maxHealth = pawn.MaxHealth;
                    if (pawn.Health >= maxHealth)
                        continue;

                    if (now < state.NextRegenTime)
                        continue;

                    if (pawn.AbsVelocity.Length() > 0)
                        continue;


                    var zombieConfig = _zombieClassCFG.CurrentValue;
                    var specialConfig = _specialClassCFG.CurrentValue;
                    var zombie = _zombieState.GetZombieClass(Id, zombieConfig.ZombieClassList, specialConfig.SpecialClassList);
                    if (zombie == null)
                        continue;

                    pawn.Health = Math.Min(pawn.Health + state.RegenAmount, maxHealth);
                    pawn.HealthUpdated();

                    //_logger.LogInformation($"{player.Controller.PlayerName}回血成功 恢复 {state.RegenAmount} 最大血量 {maxHealth} 当前 {pawn.Health}");

                    PlayerSelectSoundtoEntity(player, zombie.Sounds.RegenSound, zombie.Stats.ZombieSoundVolume);
                    state.NextRegenTime = now + state.RegenInterval;
                }
                catch (Exception ex)
                {
                    _core.Logger.LogError($"Regen Error: {ex.Message}");
                }
            }
        });

        _core.Scheduler.StopOnMapChange(_globals.g_ZombieRegenTimer);
    }

    public void StartActivePlayerRewardTimer()
    {
        _globals.g_ActivePlayerRewardTimer?.Cancel();
        _globals.g_ActivePlayerRewardTimer = null;

        var cfg = _mainCFG.CurrentValue.NormalInfection;
        float interval = cfg.ActivePlayerRewardInterval;
        int   amount   = cfg.ActivePlayerRewardAmount;
        if (interval <= 0f || amount <= 0) return;

        _globals.g_ActivePlayerRewardTimer = _core.Scheduler.RepeatBySeconds(interval, () =>
        {
            if (_extraItemsMenu == null) return;
            foreach (var player in _core.PlayerManager.GetAllPlayers())
            {
                if (!player.IsValid || player.IsFakeClient) continue;
                _extraItemsMenu.AddAmmoPacks(player.PlayerID, amount);
                player.SendMessage(MessageType.Chat,
                    _core.Translation.GetPlayerLocalizer(player)["ActivePlayerReward", amount]);
            }
        });

        _core.Scheduler.StopOnMapChange(_globals.g_ActivePlayerRewardTimer);
    }

    public void GlobalIdleTimer()
    {
        _globals.g_IdleTimer?.Cancel();
        _globals.g_IdleTimer = null;

        _globals.g_IdleTimer = _core.Scheduler.RepeatBySeconds(0.1f, () =>
        {
            int now = Environment.TickCount / 1000;
            var allalive = _core.PlayerManager.GetAlive();
            foreach (var player in allalive)
            {
                try
                {
                    var Id = player.PlayerID;
                    _globals.IsZombie.TryGetValue(Id, out bool IsZombie);

                    if (!IsZombie)
                        continue;

                    if (!_globals.g_ZombieIdleStates.TryGetValue(Id, out var state))
                        continue;

                    var controller = player.Controller;
                    if (controller == null || !controller.IsValid)
                        continue;


                    if (!controller.PawnIsAlive)
                        continue;

                    if (now < state.NextIdleTime)
                        continue;

                    var zombieConfig = _zombieClassCFG.CurrentValue;
                    var specialConfig = _specialClassCFG.CurrentValue;
                    var zombie = _zombieState.GetZombieClass(Id, zombieConfig.ZombieClassList, specialConfig.SpecialClassList);
                    if (zombie == null)
                        continue;

                    PlayerSelectSoundtoEntity(player, zombie.Sounds.IdleSound, zombie.Stats.ZombieSoundVolume);
                    state.NextIdleTime = now + state.IdleInterval;
                }
                catch (Exception ex)
                {
                    _core.Logger.LogError($"Idle Error: {ex.Message}");
                }

            }
        });

        _core.Scheduler.StopOnMapChange(_globals.g_IdleTimer);
    }

    public void ShowDmgHud(IPlayer attacker, IPlayer victim, int damage)
    {
        if (attacker == null || victim == null || !attacker.IsValid || !victim.IsValid)
            return;

        var victimpawn = victim.PlayerPawn;
        if (victimpawn == null || !victimpawn.IsValid)
            return;

        int Maxhealth = victimpawn.MaxHealth;
        int health = victimpawn.Health;

        var ZombieClassName = _zombieState.GetPlayerZombieClass(victim.PlayerID);

        // Dynamic HP color: green → yellow → red
        float hpPct    = Maxhealth > 0 ? (float)health / Maxhealth : 0f;
        string hpColor = hpPct > 0.5f ? "#00FF7F" : hpPct > 0.25f ? "#FFD700" : "#FF3030";

        // HP bar — 10 segments
        string hpBar = _helpers.BuildProgressBar(hpPct, 10, hpColor, "#666666");

        // Compact damage HUD — all on separate lines via <br>
        // Using @ verbatim strings to avoid quote escaping issues
        string classLine  = $"<span color=\"#888888\">[</span><span color=\"#FFAA00\">{ZombieClassName}</span><span color=\"#888888\">]</span> <span color=\"#FFFFFF\">{victim.Name}</span>";
        string hpLine     = hpBar + $" <span color='{hpColor}'>{health}</span><span color=\"#666666\">/</span><span color=\"#888888\">{Maxhealth}</span>";
        string dmgLine    = $"<span color=\"#FF3030\">-{damage}</span>";

        string message = $"{classLine}<br>{hpLine}<br>{dmgLine}";

        attacker.SendCenterHTML(message, 2000);

    }

    public void RandomSpawnPoint(IPlayer player, bool isZombie)
    {
        if (!player.IsValid)
            return;

        var CFG = _mainCFG.CurrentValue;

        var spawnConfig = isZombie ? CFG.ZombieSpawnPoints : CFG.HumanSpawnPoints;
        var pool = _helpers.GetSpawnPool(spawnConfig);
        if (pool.Count == 0)
            return;

        var sp = pool[Random.Shared.Next(pool.Count)];
        player.Teleport(sp.Position, sp.Angle);
    }

    public void GiveSpawnGrenade(IPlayer player, ZPLMainCFG CFG)
    {
        if (CFG.SpawnGiveFireGrenade) 
            _helpers.GiveFireGrenade(player);

        if (CFG.SpawnGiveLightGrenade) 
            _helpers.GiveLightGrenade(player);

        if (CFG.SpawnGiveFreezeGrenade) 
            _helpers.GiveFreezeGrenade(player);

        if (CFG.SpawnGiveTelportGrenade) 
            _helpers.GiveTeleprotGrenade(player);

        if (CFG.SpawnGiveIncGrenade) 
            _helpers.GiveIncGrenade(player);

    }

}
