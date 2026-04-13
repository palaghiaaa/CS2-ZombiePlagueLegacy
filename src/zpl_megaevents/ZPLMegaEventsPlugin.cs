using Economy.Contract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using System.Data;
using System.Globalization;
using ZombiePlagueLegacyCS2;

namespace ZPLMegaEvents;

/// <summary>
/// Event type enumeration.  <see cref="None"/> means no mega event is active this round.
/// </summary>
public enum MegaEventType
{
    None            = 0,
    InfectionRush   = 1,   // First zombie to infect N humans wins AP
    KillFrenzy      = 2,   // First human to kill N zombies wins AP
    FortressDefense = 3,   // All surviving humans at round timer-end win AP
    ZombieArmada    = 4,   // All alive zombies win AP when all humans are wiped
    DamageMarathon  = 5,   // First player to deal N damage wins AP
    SpecialClass    = 6,   // Special-class player (Nemesis / Assassin / Survivor / Sniper) wins AP
    HappyHour       = 7,   // Double-AP meta-modifier (wraps another event type)
    // ── New event types ───────────────────────────────────────────────────────
    GiveAway        = 8,   // Random player wins AP; consolation for all others
    HeadshotKing    = 9,   // First human to get N headshot kills on zombies wins AP
    KnifeKill       = 10,  // First human to knife-kill a zombie wins AP
    GrenadeKing     = 11,  // First human to get N grenade kills wins AP
    MVPRound        = 12,  // Player with most kills at round-end wins AP
    ZombieKingpin   = 13,  // Zombie who dealt most damage to humans at round-end wins AP
    DoubleDown      = 14,  // Flat AP bonus given to every online player at round start
}

[PluginMetadata(
    Id          = "ZPLMegaEvents",
    Version     = "1.0",
    Name        = "ZPL Mega Events",
    Author      = "DeadPoolCS2",
    Description = "Auto-scheduler for per-round Mega Events with ammo-pack rewards for ZombiePlague Legacy CS2.")]
public class ZPLMegaEventsPlugin(ISwiftlyCore core) : BasePlugin(core)
{
    private const string ConfigFile = "ZPLMegaEventsCFG.jsonc";

    private ServiceProvider?                 _sp;
    private ILogger<ZPLMegaEventsPlugin>?    _logger;
    private IOptionsMonitor<ZPLMegaEventsCFG>? _cfgMonitor;
    private ZPLMegaEventsCFG                 _config = new();

    // ── External APIs (optional, graceful degradation) ────────────────────────
    private IZombiePlagueLegacyAPI? _zplApi;
    private IEconomyAPIv1?          _economyApi;

    // ── Per-round state ───────────────────────────────────────────────────────
    private MegaEventType           _activeEvent     = MegaEventType.None;
    private MegaEventType           _innerEvent      = MegaEventType.None; // event wrapped by HappyHour
    private bool                    _eventCompleted;
    private bool                    _isHappyHour;
    private int                     _roundsPlayed;
    private CancellationTokenSource? _roundCts;

    // Progress trackers (reset each round)
    private readonly Dictionary<ulong, int>  _infectionsThisRound  = new();
    private readonly Dictionary<ulong, int>  _killsThisRound       = new();
    private readonly Dictionary<ulong, long> _damageThisRound      = new();
    // New per-round trackers
    private readonly Dictionary<ulong, int>  _headshotsThisRound   = new();
    private readonly Dictionary<ulong, int>  _grenadeKillsRound    = new();
    private readonly Dictionary<ulong, int>  _totalKillsThisRound  = new();
    private readonly Dictionary<ulong, long> _zombieDmgThisRound   = new();

    // Special-class player tracking (set on ZPL_On*Selected events)
    private ulong  _specialClassSteamId;
    private string _specialClassName = string.Empty;
    private int    _specialClassAP;

    // Map-level state
    private bool   _mapGiveAwayDone;

    // Random engine (deterministic per load)
    private static readonly Random _rng = new();

    // ── MySQL stats ───────────────────────────────────────────────────────────
    private bool   _dbReady;
    private string _dbConnection = string.Empty;

    // ── Named event delegates for proper Unload() cleanup ─────────────────────
    private Action<IOnMapLoadEvent>?   _onMapLoadHandler;
    private Action<IOnMapUnloadEvent>? _onMapUnloadHandler;

    // ─────────────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    public override void Load(bool hotReload)
    {
        if (!hotReload)
            Core.Configuration.InitializeJsonWithModel<ZPLMegaEventsCFG>(ConfigFile, "ZPLMegaEventsCFG")
                .Configure(builder =>
                {
                    builder.AddJsonFile(ConfigFile, false, true);
                    builder.SetFileLoadExceptionHandler(ctx =>
                    {
                        Core.Logger.LogError(
                            "[MegaEvents] Failed to load {File}: {Error}. Using last valid config.",
                            ConfigFile, ctx.Exception.Message);
                        ctx.Ignore = true;
                    });
                });

        var collection = new ServiceCollection();
        collection.AddSwiftly(Core);
        collection.AddOptions<ZPLMegaEventsCFG>().BindConfiguration("ZPLMegaEventsCFG");

        _sp         = collection.BuildServiceProvider();
        _logger     = _sp.GetRequiredService<ILogger<ZPLMegaEventsPlugin>>();
        _cfgMonitor = _sp.GetRequiredService<IOptionsMonitor<ZPLMegaEventsCFG>>();
        _config     = _cfgMonitor.CurrentValue;
        _cfgMonitor.OnChange(cfg => _config = cfg);

        // MySQL stats
        if (!string.IsNullOrWhiteSpace(_config.DatabaseConnection))
            DbEnsureSchema(_config.DatabaseConnection);

        // Hook game events
        Core.GameEvent.HookPre<EventRoundFreezeEnd>(OnRoundFreezeEnd);
        Core.GameEvent.HookPre<EventRoundEnd>(OnRoundEnd);
        Core.GameEvent.HookPre<EventPlayerDeath>(OnPlayerDeath);
        Core.GameEvent.HookPre<EventPlayerHurt>(OnPlayerHurt);

        // Hook map lifecycle for map-level giveaway
        _onMapLoadHandler   = OnMapLoad;
        _onMapUnloadHandler = OnMapUnload;
        Core.Event.OnMapLoad   += _onMapLoadHandler;
        Core.Event.OnMapUnload += _onMapUnloadHandler;
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        // ── ZPL API ───────────────────────────────────────────────────────────
        if (interfaceManager.HasSharedInterface("ZombiePlagueLegacy"))
        {
            try
            {
                _zplApi = interfaceManager.GetSharedInterface<IZombiePlagueLegacyAPI>("ZombiePlagueLegacy");
                if (_zplApi != null)
                {
                    _zplApi.ZPL_OnPlayerInfect       += OnZPLPlayerInfect;
                    _zplApi.ZPL_OnMotherZombieSelected += OnMotherZombieSelected;
                    _zplApi.ZPL_OnNemesisSelected      += p => TrackSpecialClass(p, "Nemesis",  _config.SpecialClassBonus.NemesisWinAP);
                    _zplApi.ZPL_OnAssassinSelected     += p => TrackSpecialClass(p, "Assassin", _config.SpecialClassBonus.AssassinWinAP);
                    _zplApi.ZPL_OnSurvivorSelected     += p => TrackSpecialClass(p, "Survivor", _config.SpecialClassBonus.SurvivorWinAP);
                    _zplApi.ZPL_OnSniperSelected       += p => TrackSpecialClass(p, "Sniper",   _config.SpecialClassBonus.SniperWinAP);
                    _zplApi.ZPL_OnHumanWin             += OnHumanWin;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("[MegaEvents] Failed to acquire ZPL API: {Ex}. Events will run with limited tracking.", ex.Message);
            }
        }

        // ── Economy API ───────────────────────────────────────────────────────
        if (interfaceManager.HasSharedInterface("Economy.API.v1"))
        {
            try
            {
                _economyApi = interfaceManager.GetSharedInterface<IEconomyAPIv1>("Economy.API.v1");
                if (_economyApi != null)
                {
                    if (!_economyApi.WalletKindExists(_config.WalletKind))
                        _economyApi.EnsureWalletKind(_config.WalletKind);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("[MegaEvents] Failed to acquire Economy API: {Ex}. AP rewards will not be given.", ex.Message);
            }
        }
    }

    public override void Unload()
    {
        _roundCts?.Cancel();
        _roundCts?.Dispose();

        if (_zplApi != null)
        {
            _zplApi.ZPL_OnPlayerInfect        -= OnZPLPlayerInfect;
            _zplApi.ZPL_OnMotherZombieSelected -= OnMotherZombieSelected;
            _zplApi.ZPL_OnHumanWin             -= OnHumanWin;
        }

        Core.GameEvent.UnhookPre<EventRoundFreezeEnd>();
        Core.GameEvent.UnhookPre<EventRoundEnd>();
        Core.GameEvent.UnhookPre<EventPlayerDeath>();
        Core.GameEvent.UnhookPre<EventPlayerHurt>();

        // Unhook map lifecycle delegates
        if (_onMapLoadHandler != null)   Core.Event.OnMapLoad   -= _onMapLoadHandler;
        if (_onMapUnloadHandler != null) Core.Event.OnMapUnload -= _onMapUnloadHandler;

        _sp?.Dispose();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Event handlers
    // ─────────────────────────────────────────────────────────────────────────

    private HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event)
    {
        // Reset per-round state
        _roundCts?.Cancel();
        _roundCts?.Dispose();
        _roundCts = new CancellationTokenSource();

        _infectionsThisRound.Clear();
        _killsThisRound.Clear();
        _damageThisRound.Clear();
        _headshotsThisRound.Clear();
        _grenadeKillsRound.Clear();
        _totalKillsThisRound.Clear();
        _zombieDmgThisRound.Clear();
        _eventCompleted      = false;
        _specialClassSteamId = 0;
        _specialClassName    = string.Empty;
        _specialClassAP      = 0;

        _roundsPlayed++;

        // Determine active event for this round
        (_activeEvent, _innerEvent, _isHappyHour) = SelectEvent();

        if (_activeEvent == MegaEventType.None) return HookResult.Continue;

        AnnounceEventStart();

        var token = _roundCts.Token;

        // Immediate-resolution events: resolve after a short delay so all players are spawned
        var resolvedImmediate = _isHappyHour ? _innerEvent : _activeEvent;
        if (resolvedImmediate == MegaEventType.GiveAway || resolvedImmediate == MegaEventType.DoubleDown)
        {
            Core.Scheduler.DelayBySeconds(1f, () =>
            {
                if (token.IsCancellationRequested) return;
                if (resolvedImmediate == MegaEventType.GiveAway)
                    ResolveGiveAway();
                else
                    ResolveDoubleDown();
            });
        }

        // Schedule progress reminder for competitive events
        if (_config.EnableProgressReminder && _config.ProgressReminderSeconds > 0)
        {
            Core.Scheduler.DelayBySeconds(_config.ProgressReminderSeconds, () =>
            {
                if (token.IsCancellationRequested || _eventCompleted) return;
                AnnounceProgress();
            });
        }

        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event)
    {
        _roundCts?.Cancel();

        if (_activeEvent == MegaEventType.None || _eventCompleted) return HookResult.Continue;

        var resolvedEvent = _isHappyHour ? _innerEvent : _activeEvent;

        switch (resolvedEvent)
        {
            case MegaEventType.FortressDefense:
                RewardSurvivingHumans();
                break;

            case MegaEventType.SpecialClass:
                // Handled by OnHumanWin; nothing extra needed here
                break;

            case MegaEventType.InfectionRush:
            case MegaEventType.KillFrenzy:
            case MegaEventType.DamageMarathon:
            case MegaEventType.ZombieArmada:
                // First-to-complete; if nobody finished, give consolation
                AnnounceNoWinner();
                GiveConsolationAP();
                break;

            // ── New at-round-end events ────────────────────────────────────────

            case MegaEventType.MVPRound:
                ResolveMVPRound();
                break;

            case MegaEventType.ZombieKingpin:
                ResolveZombieKingpin();
                break;

            case MegaEventType.HeadshotKing:
                // Could have been completed mid-round (first-to-N); if not, award best performer
                if (!_eventCompleted)
                    ResolveHeadshotKing();
                else
                    GiveParticipantAP(_headshotsThisRound, _config.HeadshotKing.ParticipantRewardAP, excludeLeader: true);
                break;

            case MegaEventType.GrenadeKing:
                if (!_eventCompleted)
                    ResolveGrenadeKing();
                else
                    GiveParticipantAP(_grenadeKillsRound, _config.GrenadeKing.ParticipantRewardAP, excludeLeader: true);
                break;

            case MegaEventType.KnifeKill:
                if (!_eventCompleted)
                    AnnounceNoWinner();
                break;

            // GiveAway / DoubleDown are resolved immediately at round start — nothing to do here
        }

        _activeEvent = MegaEventType.None;
        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event)
    {
        if (_activeEvent == MegaEventType.None) return HookResult.Continue;
        var resolvedEvent = _isHappyHour ? _innerEvent : _activeEvent;

        var attacker = @event.AttackerPlayer;
        var victim   = @event.UserIdPlayer;
        if (attacker == null || victim == null || !attacker.IsValid || !victim.IsValid) return HookResult.Continue;
        if (attacker.SteamID == 0) return HookResult.Continue;

        // Resolve team membership (best-effort; falls back gracefully without ZPL API)
        bool attackerIsHuman = _zplApi != null && !_zplApi.ZPL_IsZombie(attacker.PlayerID);
        bool victimIsZombie  = _zplApi != null &&  _zplApi.ZPL_IsZombie(victim.PlayerID);

        string weapon = @event.Weapon ?? string.Empty;

        switch (resolvedEvent)
        {
            // ── KillFrenzy ────────────────────────────────────────────────────
            case MegaEventType.KillFrenzy:
            {
                if (_eventCompleted) break;
                if (!attackerIsHuman || !victimIsZombie) break;

                _killsThisRound.TryGetValue(attacker.SteamID, out int prev);
                int newCount = prev + 1;
                _killsThisRound[attacker.SteamID] = newCount;

                int target = _config.KillFrenzy.TargetKills;
                if (newCount >= target)
                {
                    _eventCompleted = true;
                    int ap = ApplyHappyHour(_config.KillFrenzy.WinnerRewardAP);
                    GiveAP(attacker, ap);
                    DbRecordEvent(attacker.SteamID, ap);
                    WriteLog("KillFrenzy", attacker.SteamID, attacker.Name, ap);
                    Announce($" {_config.ChatPrefix} [gold]🏆 {attacker.Name}[default] won the [gold]Kill Frenzy[default]! ({newCount}/{target} kills) → [gold]+{ap} AP");
                }
                break;
            }

            // ── HeadshotKing ──────────────────────────────────────────────────
            case MegaEventType.HeadshotKing:
            {
                if (_eventCompleted) break;
                if (!attackerIsHuman || !victimIsZombie) break;
                if (!@event.Headshot) break;

                _headshotsThisRound.TryGetValue(attacker.SteamID, out int prev);
                int newCount = prev + 1;
                _headshotsThisRound[attacker.SteamID] = newCount;

                int target = _config.HeadshotKing.TargetHeadshots;
                if (newCount >= target)
                {
                    _eventCompleted = true;
                    int ap = ApplyHappyHour(_config.HeadshotKing.WinnerRewardAP);
                    GiveAP(attacker, ap);
                    DbRecordEvent(attacker.SteamID, ap);
                    WriteLog("HeadshotKing", attacker.SteamID, attacker.Name, ap);
                    Announce($" {_config.ChatPrefix} [gold]🎯 {attacker.Name}[default] is the [gold]Headshot King[default]! ({newCount}/{target} headshots) → [gold]+{ap} AP");
                }
                break;
            }

            // ── KnifeKill ─────────────────────────────────────────────────────
            case MegaEventType.KnifeKill:
            {
                if (_eventCompleted) break;
                if (!attackerIsHuman || !victimIsZombie) break;
                if (weapon != "weapon_knife" && weapon != "knife") break;

                _eventCompleted = true;
                int ap = ApplyHappyHour(_config.KnifeKill.WinnerRewardAP);
                GiveAP(attacker, ap);
                DbRecordEvent(attacker.SteamID, ap);
                WriteLog("KnifeKill", attacker.SteamID, attacker.Name, ap);
                Announce($" {_config.ChatPrefix} [gold]🔪 {attacker.Name}[default] won [gold]Knife Kill[default]! → [gold]+{ap} AP");
                break;
            }

            // ── GrenadeKing ───────────────────────────────────────────────────
            case MegaEventType.GrenadeKing:
            {
                if (_eventCompleted) break;
                if (!attackerIsHuman || !victimIsZombie) break;
                if (!IsGrenadeWeapon(weapon)) break;

                _grenadeKillsRound.TryGetValue(attacker.SteamID, out int prev);
                int newCount = prev + 1;
                _grenadeKillsRound[attacker.SteamID] = newCount;

                int target = _config.GrenadeKing.TargetGrenadeKills;
                if (newCount >= target)
                {
                    _eventCompleted = true;
                    int ap = ApplyHappyHour(_config.GrenadeKing.WinnerRewardAP);
                    GiveAP(attacker, ap);
                    DbRecordEvent(attacker.SteamID, ap);
                    WriteLog("GrenadeKing", attacker.SteamID, attacker.Name, ap);
                    Announce($" {_config.ChatPrefix} [gold]💥 {attacker.Name}[default] is the [gold]Grenade King[default]! ({newCount}/{target} grenade kills) → [gold]+{ap} AP");
                }
                break;
            }

            // ── MVPRound (track all kills; resolved at round end) ─────────────
            case MegaEventType.MVPRound:
            {
                _totalKillsThisRound.TryGetValue(attacker.SteamID, out int prev);
                _totalKillsThisRound[attacker.SteamID] = prev + 1;
                break;
            }
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event)
    {
        if (_activeEvent == MegaEventType.None || _eventCompleted) return HookResult.Continue;
        var resolvedEvent = _isHappyHour ? _innerEvent : _activeEvent;
        if (resolvedEvent != MegaEventType.DamageMarathon) return HookResult.Continue;

        var attacker = @event.AttackerPlayer;
        if (attacker == null || !attacker.IsValid || attacker.SteamID == 0) return HookResult.Continue;

        int dmg = @event.DmgHealth;
        if (dmg <= 0) return HookResult.Continue;

        _damageThisRound.TryGetValue(attacker.SteamID, out long prev);
        long newDmg = prev + dmg;
        _damageThisRound[attacker.SteamID] = newDmg;

        long target = _config.DamageMarathon.TargetDamage;
        if (newDmg >= target)
        {
            _eventCompleted = true;
            int ap = ApplyHappyHour(_config.DamageMarathon.WinnerRewardAP);
            GiveAP(attacker, ap);
            DbRecordEvent(attacker.SteamID, ap);
            Announce($" {_config.ChatPrefix} [gold]🏆 {attacker.Name}[default] won the [gold]Damage Marathon[default]! ({newDmg:N0}/{target:N0} damage) → [gold]+{ap} AP");
        }

        return HookResult.Continue;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  ZPL API callbacks
    // ─────────────────────────────────────────────────────────────────────────

    private void OnZPLPlayerInfect(IPlayer? attacker, IPlayer victim, bool byGrenade, string zombieClass)
    {
        if (_activeEvent == MegaEventType.None) return;
        var resolvedEvent = _isHappyHour ? _innerEvent : _activeEvent;

        if (attacker == null || attacker.SteamID == 0) return;

        switch (resolvedEvent)
        {
            case MegaEventType.InfectionRush:
                if (_eventCompleted) break;
                _infectionsThisRound.TryGetValue(attacker.SteamID, out int prevInf);
                int newInf = prevInf + 1;
                _infectionsThisRound[attacker.SteamID] = newInf;

                int infTarget = _config.InfectionRush.TargetInfections;
                if (newInf >= infTarget)
                {
                    _eventCompleted = true;
                    int ap = ApplyHappyHour(_config.InfectionRush.WinnerRewardAP);
                    GiveAP(attacker, ap);
                    DbRecordEvent(attacker.SteamID, ap);
                    Announce($" {_config.ChatPrefix} [gold]🏆 {attacker.Name}[default] won the [gold]Infection Rush[default]! ({newInf}/{infTarget} infections) → [gold]+{ap} AP");
                }
                break;

            case MegaEventType.ZombieArmada:
                // Track infections to detect "all humans wiped" — handled via OnHumanWin(false)
                break;
        }
    }

    private void OnMotherZombieSelected(IPlayer player)
    {
        // Mother zombie is always the first zombie — track for infection rush purposes
        if (_activeEvent == MegaEventType.None) return;
        // No special handling needed; infection tracking fires on ZPL_OnPlayerInfect
    }

    private void TrackSpecialClass(IPlayer player, string className, int apReward)
    {
        if (_specialClassSteamId != 0) return; // only track first special class
        _specialClassSteamId = player.SteamID;
        _specialClassName    = className;
        _specialClassAP      = apReward;
    }

    private void OnHumanWin(bool humansWon)
    {
        if (_activeEvent == MegaEventType.None || _eventCompleted) return;
        var resolvedEvent = _isHappyHour ? _innerEvent : _activeEvent;

        switch (resolvedEvent)
        {
            case MegaEventType.ZombieArmada:
                // Zombies win = humans wiped → reward all alive zombies
                if (!humansWon)
                {
                    _eventCompleted = true;
                    RewardAliveZombies();
                }
                break;

            case MegaEventType.FortressDefense:
                // Humans won (survived) → reward all alive humans (timer-end)
                if (humansWon)
                {
                    _eventCompleted = true;
                    RewardSurvivingHumans();
                }
                break;

            case MegaEventType.SpecialClass:
                // Special class wins their condition
                if (_specialClassSteamId != 0 && !_eventCompleted)
                {
                    bool specialWon = (_specialClassName is "Survivor" or "Sniper") ? humansWon : !humansWon;
                    if (specialWon)
                    {
                        _eventCompleted = true;
                        int ap = ApplyHappyHour(_specialClassAP);
                        var player = FindPlayerBySteamId(_specialClassSteamId);
                        if (player != null)
                        {
                            GiveAP(player, ap);
                            DbRecordEvent(_specialClassSteamId, ap);
                            Announce($" {_config.ChatPrefix} [gold]🏆 {player.Name}[default] ([gold]{_specialClassName}[default]) won the [gold]Special Class Bonus[default]! → [gold]+{ap} AP");
                        }
                    }
                }
                break;
        }

        _activeEvent = MegaEventType.None;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Reward helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void RewardSurvivingHumans()
    {
        if (_economyApi == null) return;
        int ap   = ApplyHappyHour(_config.FortressDefense.RewardAP);
        int count = 0;
        foreach (var player in Core.PlayerManager.GetAlive())
        {
            if (!player.IsValid || player.IsFakeClient) continue;
            if (_zplApi != null && _zplApi.ZPL_IsZombie(player.PlayerID)) continue;
            GiveAP(player, ap);
            DbRecordEvent(player.SteamID, ap);
            count++;
        }
        if (count > 0)
            Announce($" {_config.ChatPrefix} [green]🛡 Fortress Defense[default]: {count} humans survived! Each earned [gold]+{ap} AP");
    }

    private void RewardAliveZombies()
    {
        if (_economyApi == null) return;
        int ap    = ApplyHappyHour(_config.ZombieArmada.RewardAP);
        int count = 0;
        foreach (var player in Core.PlayerManager.GetAlive())
        {
            if (!player.IsValid || player.IsFakeClient) continue;
            if (_zplApi != null && !_zplApi.ZPL_IsZombie(player.PlayerID)) continue;
            GiveAP(player, ap);
            DbRecordEvent(player.SteamID, ap);
            count++;
        }
        if (count > 0)
            Announce($" {_config.ChatPrefix} [red]🧟 Zombie Armada[default]: all humans wiped! {count} zombies earned [gold]+{ap} AP");
    }

    private void GiveConsolationAP()
    {
        if (_economyApi == null) return;
        var resolvedEvent = _isHappyHour ? _innerEvent : _activeEvent;

        switch (resolvedEvent)
        {
            case MegaEventType.InfectionRush:
            {
                int ap = ApplyHappyHour(_config.InfectionRush.ParticipantRewardAP);
                if (ap <= 0) break;
                foreach (var (sid, count) in _infectionsThisRound)
                {
                    if (count <= 0) continue;
                    var p = FindPlayerBySteamId(sid);
                    if (p != null) GiveAP(p, ap);
                }
                break;
            }
            case MegaEventType.KillFrenzy:
            {
                int ap = ApplyHappyHour(_config.KillFrenzy.ParticipantRewardAP);
                if (ap <= 0) break;
                foreach (var (sid, count) in _killsThisRound)
                {
                    if (count <= 0) continue;
                    var p = FindPlayerBySteamId(sid);
                    if (p != null) GiveAP(p, ap);
                }
                break;
            }
        }
    }

    private void GiveAP(IPlayer player, int amount)
    {
        if (_economyApi == null || amount <= 0 || !player.IsValid) return;
        try
        {
            _economyApi.AddPlayerBalance(player, _config.WalletKind, amount);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("[MegaEvents] GiveAP({Name},{Amount}) failed: {Ex}", player.Name, amount, ex.Message);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Event selection
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns (activeEvent, innerEvent, isHappyHour).
    /// <paramref name="innerEvent"/> is only set when <paramref name="isHappyHour"/> is true.
    /// Scheduled events (calendar-based) take precedence over the random pool.
    /// </summary>
    private (MegaEventType active, MegaEventType inner, bool isHappyHour) SelectEvent()
    {
        // ── Calendar-based scheduled events (highest priority) ────────────────
        var scheduledType = FindScheduledEventType();
        if (scheduledType.HasValue && scheduledType.Value != MegaEventType.None)
        {
            _logger?.LogInformation("[MegaEvents] Scheduled event active: {Event}", scheduledType.Value);
            return (scheduledType.Value, MegaEventType.None, false);
        }

        // ── Round-counter happy hour ──────────────────────────────────────────
        bool happyHour = _config.HappyHour.RoundInterval > 0 &&
                         (_roundsPlayed % _config.HappyHour.RoundInterval == 0);

        var pool = BuildEventPool();
        if (pool.Count == 0) return (MegaEventType.None, MegaEventType.None, false);

        int totalWeight = 0;
        foreach (var (_, w) in pool) totalWeight += w;
        if (totalWeight <= 0) return (MegaEventType.None, MegaEventType.None, false);

        int roll = _rng.Next(totalWeight);
        int acc  = 0;
        MegaEventType chosen = MegaEventType.None;
        foreach (var (evt, w) in pool)
        {
            acc += w;
            if (roll < acc) { chosen = evt; break; }
        }

        if (happyHour)
            return (MegaEventType.HappyHour, chosen, true);

        return (chosen, MegaEventType.None, false);
    }

    /// <summary>
    /// Scans the <see cref="ZPLMegaEventsCFG.ScheduledEvents"/> list and returns the
    /// first <see cref="MegaEventType"/> whose time window matches the current wall-clock
    /// time, or <c>null</c> when no window is active.
    /// </summary>
    private MegaEventType? FindScheduledEventType()
    {
        var scheduleCfg = _config.ScheduledEvents;
        if (!scheduleCfg.Enable || scheduleCfg.Events.Count == 0)
            return null;

        // Apply the configured UTC offset so operators can express local times.
        var now        = DateTimeOffset.UtcNow.AddHours(scheduleCfg.TimezoneOffsetHours);
        int currentDow  = (int)now.DayOfWeek; // 0 = Sunday
        int currentHour = now.Hour;
        int weekOfYear  = ISOWeek.GetWeekOfYear(now.DateTime);

        foreach (var entry in scheduleCfg.Events)
        {
            // Day-of-week filter (empty list = every day)
            if (entry.DaysOfWeek.Count > 0 && !entry.DaysOfWeek.Contains(currentDow))
                continue;

            // Hour-of-day window (handles wrap-around midnight when HourEnd < HourStart)
            bool inHourWindow;
            if (entry.HourStart <= entry.HourEnd)
                inHourWindow = currentHour >= entry.HourStart && currentHour < entry.HourEnd;
            else
                inHourWindow = currentHour >= entry.HourStart || currentHour < entry.HourEnd;
            if (!inHourWindow)
                continue;

            // Week-interval filter (0 or 1 = every week, N = every N-th ISO week)
            int interval = entry.WeekInterval <= 1 ? 1 : entry.WeekInterval;
            if (interval > 1 && (weekOfYear % interval) != 0)
                continue;

            return entry.EventType;
        }

        return null;
    }

    private List<(MegaEventType evt, int weight)> BuildEventPool()
    {
        var pool = new List<(MegaEventType, int)>(6);
        if (_config.InfectionRush.Enable   && _config.InfectionRush.Weight   > 0) pool.Add((MegaEventType.InfectionRush,   _config.InfectionRush.Weight));
        if (_config.KillFrenzy.Enable      && _config.KillFrenzy.Weight      > 0) pool.Add((MegaEventType.KillFrenzy,      _config.KillFrenzy.Weight));
        if (_config.FortressDefense.Enable && _config.FortressDefense.Weight > 0) pool.Add((MegaEventType.FortressDefense, _config.FortressDefense.Weight));
        if (_config.ZombieArmada.Enable    && _config.ZombieArmada.Weight    > 0) pool.Add((MegaEventType.ZombieArmada,    _config.ZombieArmada.Weight));
        if (_config.DamageMarathon.Enable  && _config.DamageMarathon.Weight  > 0) pool.Add((MegaEventType.DamageMarathon,  _config.DamageMarathon.Weight));
        if (_config.SpecialClassBonus.Enable && _config.SpecialClassBonus.Weight > 0) pool.Add((MegaEventType.SpecialClass, _config.SpecialClassBonus.Weight));
        return pool;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Announcement helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void AnnounceEventStart()
    {
        if (!_config.EnableAnnouncements) return;
        var resolvedEvent = _isHappyHour ? _innerEvent : _activeEvent;
        string happyTag   = _isHappyHour ? $" [gold]⭐ HAPPY HOUR x{_config.HappyHour.Multiplier}![default]" : string.Empty;

        string desc = resolvedEvent switch
        {
            MegaEventType.InfectionRush   => $"[red]INFECTION RUSH[default]: First zombie to infect [gold]{_config.InfectionRush.TargetInfections}[default] humans wins [gold]{_config.InfectionRush.WinnerRewardAP} AP",
            MegaEventType.KillFrenzy      => $"[green]KILL FRENZY[default]: First human to kill [gold]{_config.KillFrenzy.TargetKills}[default] zombies wins [gold]{_config.KillFrenzy.WinnerRewardAP} AP",
            MegaEventType.FortressDefense => $"[blue]FORTRESS DEFENSE[default]: Every human who survives wins [gold]{_config.FortressDefense.RewardAP} AP",
            MegaEventType.ZombieArmada    => $"[red]ZOMBIE ARMADA[default]: If zombies wipe all humans, every zombie wins [gold]{_config.ZombieArmada.RewardAP} AP",
            MegaEventType.DamageMarathon  => $"[orange]DAMAGE MARATHON[default]: First to deal [gold]{_config.DamageMarathon.TargetDamage:N0}[default] damage wins [gold]{_config.DamageMarathon.WinnerRewardAP} AP",
            MegaEventType.SpecialClass    => $"[gold]SPECIAL CLASS BONUS[default]: Special class player wins bonus AP if they achieve their objective",
            _                             => string.Empty
        };

        if (string.IsNullOrEmpty(desc)) return;

        BroadcastAll($" {_config.ChatPrefix}{happyTag} [gold]⚡ MEGA EVENT[default]: {desc}!");
    }

    private void AnnounceProgress()
    {
        if (!_config.EnableAnnouncements) return;
        var resolvedEvent = _isHappyHour ? _innerEvent : _activeEvent;

        switch (resolvedEvent)
        {
            case MegaEventType.InfectionRush:
            {
                var (name, count) = GetLeader(_infectionsThisRound);
                if (count > 0)
                    BroadcastAll($" {_config.ChatPrefix} [gold]Infection Rush[default] leader: [gold]{name}[default] ({count}/{_config.InfectionRush.TargetInfections})");
                break;
            }
            case MegaEventType.KillFrenzy:
            {
                var (name, count) = GetLeader(_killsThisRound);
                if (count > 0)
                    BroadcastAll($" {_config.ChatPrefix} [gold]Kill Frenzy[default] leader: [gold]{name}[default] ({count}/{_config.KillFrenzy.TargetKills})");
                break;
            }
            case MegaEventType.DamageMarathon:
            {
                var (name, dmg) = GetLeaderLong(_damageThisRound);
                if (dmg > 0)
                    BroadcastAll($" {_config.ChatPrefix} [gold]Damage Marathon[default] leader: [gold]{name}[default] ({dmg:N0}/{_config.DamageMarathon.TargetDamage:N0})");
                break;
            }
        }
    }

    private void AnnounceNoWinner()
    {
        if (!_config.EnableAnnouncements) return;
        var resolvedEvent = _isHappyHour ? _innerEvent : _activeEvent;
        string name = resolvedEvent switch
        {
            MegaEventType.InfectionRush  => "Infection Rush",
            MegaEventType.KillFrenzy     => "Kill Frenzy",
            MegaEventType.DamageMarathon => "Damage Marathon",
            _                            => string.Empty
        };
        if (!string.IsNullOrEmpty(name))
            BroadcastAll($" {_config.ChatPrefix} [grey]No winner for [gold]{name}[grey] this round.");
    }

    private void Announce(string msg)
    {
        if (!_config.EnableAnnouncements) return;
        BroadcastAll(msg);
    }

    private void BroadcastAll(string msg)
    {
        Core.Scheduler.NextWorldUpdate(() =>
        {
            foreach (var player in Core.PlayerManager.GetAllPlayers())
            {
                if (player != null && player.IsValid && !player.IsFakeClient)
                    player.SendMessage(MessageType.Chat, msg);
            }
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Utility helpers
    // ─────────────────────────────────────────────────────────────────────────

    private int ApplyHappyHour(int baseAP)
        => _isHappyHour ? (int)Math.Round(baseAP * _config.HappyHour.Multiplier) : baseAP;

    private IPlayer? FindPlayerBySteamId(ulong steamId)
    {
        foreach (var p in Core.PlayerManager.GetAllPlayers())
            if (p != null && p.IsValid && p.SteamID == steamId) return p;
        return null;
    }

    private (string name, int count) GetLeader(Dictionary<ulong, int> dict)
    {
        ulong bestSid   = 0;
        int   bestCount = 0;
        foreach (var (sid, c) in dict)
            if (c > bestCount) { bestCount = c; bestSid = sid; }
        if (bestSid == 0) return (string.Empty, 0);
        var p = FindPlayerBySteamId(bestSid);
        return (p?.Name ?? bestSid.ToString(), bestCount);
    }

    private (string name, long count) GetLeaderLong(Dictionary<ulong, long> dict)
    {
        ulong bestSid   = 0;
        long  bestCount = 0;
        foreach (var (sid, c) in dict)
            if (c > bestCount) { bestCount = c; bestSid = sid; }
        if (bestSid == 0) return (string.Empty, 0);
        var p = FindPlayerBySteamId(bestSid);
        return (p?.Name ?? bestSid.ToString(), bestCount);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  MySQL stats persistence
    // ─────────────────────────────────────────────────────────────────────────

    private void DbEnsureSchema(string connectionName)
    {
        _dbConnection = connectionName;
        try
        {
            using var conn = DbOpen();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS zpl_megaevents_stats (
                    steam_id          VARCHAR(32) NOT NULL,
                    events_completed  INT         NOT NULL DEFAULT 0,
                    total_ap_earned   INT         NOT NULL DEFAULT 0,
                    PRIMARY KEY (steam_id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                """;
            cmd.ExecuteNonQuery();
            _dbReady = true;
            _logger?.LogInformation("[MegaEvents] MySQL stats schema ready (connection='{Name}').", connectionName);
        }
        catch (Exception ex)
        {
            _logger?.LogError("[MegaEvents] Failed to initialise MySQL stats (connection='{Name}'): {Ex}", connectionName, ex.Message);
        }
    }

    private void DbRecordEvent(ulong steamId, int apEarned)
    {
        if (!_dbReady || steamId == 0) return;
        try
        {
            using var conn = DbOpen();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO zpl_megaevents_stats (steam_id, events_completed, total_ap_earned)
                VALUES (@sid, 1, @ap)
                ON DUPLICATE KEY UPDATE
                    events_completed = events_completed + 1,
                    total_ap_earned  = total_ap_earned  + VALUES(total_ap_earned)
                """;
            DbAddParam(cmd, "@sid", steamId.ToString());
            DbAddParam(cmd, "@ap",  apEarned);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("[MegaEvents] DbRecordEvent({SteamId}) failed: {Ex}", steamId, ex.Message);
        }
    }

    private IDbConnection DbOpen()
    {
        var conn = Core.Database.GetConnection(_dbConnection);
        conn.Open();
        return conn;
    }

    private static void DbAddParam(IDbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }
}
