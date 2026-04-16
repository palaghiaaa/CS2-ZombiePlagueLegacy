using Economy.Contract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
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
public class ZPLMegaEventsPlugin : BasePlugin
{
    private const string ConfigFile = "ZPLMegaEventsCFG.jsonc";
    private const string ConfigSection = "ZPLMegaEventsCFG";
    private const string StatusCommand = "sw_megaevents_status";

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

    // Keep explicit delegate targets so subscriptions can be cleanly removed.
    private readonly Action<IPlayer> _onNemesisSelected;
    private readonly Action<IPlayer> _onAssassinSelected;
    private readonly Action<IPlayer> _onSurvivorSelected;
    private readonly Action<IPlayer> _onSniperSelected;
    private bool _specialClassHooksSubscribed;

    // Map-level state
    private bool   _mapGiveAwayDone;

    // Random engine (deterministic per load)
    private static readonly Random _rng = new();

    // ── MySQL stats ───────────────────────────────────────────────────────────
    private bool   _dbReady;
    private string _dbConnection = string.Empty;

    // ─────────────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    public ZPLMegaEventsPlugin(ISwiftlyCore core) : base(core)
    {
        _onNemesisSelected  = OnNemesisSelected;
        _onAssassinSelected = OnAssassinSelected;
        _onSurvivorSelected = OnSurvivorSelected;
        _onSniperSelected   = OnSniperSelected;
    }

    public override void Load(bool hotReload)
    {
        // Guard with !hotReload: SwiftlyS2's PluginConfigurationService.Manager is a
        // lazy singleton that is never reset between hot-reloads (map changes).
        // Calling AddJsonFile on it again on every Load() appends a brand-new
        // FileSystemWatcher thread to the same ConfigurationManager, causing one
        // watcher thread to leak per map change.  Using !hotReload (not a static
        // flag) is correct because SwiftlyS2 loads each plugin into a fresh
        // AssemblyLoadContext on hot-reload, resetting all static variables.
        if (!hotReload)
        {
            Core.Configuration.InitializeJsonWithModel<ZPLMegaEventsCFG>(ConfigFile, ConfigSection)
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
        }

        var collection = new ServiceCollection();
        collection.AddSwiftly(Core);
        collection.AddOptions<ZPLMegaEventsCFG>().BindConfiguration(ConfigSection);

        _sp         = collection.BuildServiceProvider();
        _logger     = _sp.GetRequiredService<ILogger<ZPLMegaEventsPlugin>>();
        _cfgMonitor = _sp.GetRequiredService<IOptionsMonitor<ZPLMegaEventsCFG>>();
        _config     = _cfgMonitor.CurrentValue;
        _cfgMonitor.OnChange(cfg =>
        {
            _config = cfg;
            Core.Scheduler.NextWorldUpdate(SyncSpecialClassHookSubscription);
        });

        // MySQL stats
        if (!string.IsNullOrWhiteSpace(_config.DatabaseConnection))
            DbEnsureSchema(_config.DatabaseConnection);

        // Hook game events
        Core.GameEvent.HookPre<EventRoundFreezeEnd>(OnRoundFreezeEnd);
        Core.GameEvent.HookPre<EventRoundEnd>(OnRoundEnd);
        Core.GameEvent.HookPre<EventPlayerDeath>(OnPlayerDeath);
        Core.GameEvent.HookPre<EventPlayerHurt>(OnPlayerHurt);
        Core.Command.RegisterCommand(StatusCommand, CmdMegaEventsStatus, true);

        // Hook map lifecycle for map-level giveaway
        Core.Event.OnMapLoad   += OnMapLoad;
        Core.Event.OnMapUnload += OnMapUnload;
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
                    _zplApi.ZPL_OnHumanWin             += OnHumanWin;
                    SyncSpecialClassHookSubscription();
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
            if (_specialClassHooksSubscribed)
            {
                _zplApi.ZPL_OnNemesisSelected  -= _onNemesisSelected;
                _zplApi.ZPL_OnAssassinSelected -= _onAssassinSelected;
                _zplApi.ZPL_OnSurvivorSelected -= _onSurvivorSelected;
                _zplApi.ZPL_OnSniperSelected   -= _onSniperSelected;
                _specialClassHooksSubscribed = false;
            }
            _zplApi.ZPL_OnHumanWin             -= OnHumanWin;
        }

        Core.GameEvent.UnhookPre<EventRoundFreezeEnd>();
        Core.GameEvent.UnhookPre<EventRoundEnd>();
        Core.GameEvent.UnhookPre<EventPlayerDeath>();
        Core.GameEvent.UnhookPre<EventPlayerHurt>();
        Core.Command.UnregisterCommand(StatusCommand);

        // Unhook map lifecycle
        Core.Event.OnMapLoad   -= OnMapLoad;
        Core.Event.OnMapUnload -= OnMapUnload;

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

        int onlinePlayers = CountEligiblePlayers();
        if (!HasMinimumPlayersForEventStart(onlinePlayers))
        {
            _activeEvent = MegaEventType.None;
            _innerEvent = MegaEventType.None;
            _isHappyHour = false;
            AnnounceMinimumPlayersNotMet(false, onlinePlayers);
            _logger?.LogDebug(
                "[MegaEvents] Skipping round event start: online players {OnlinePlayers} below minimum {MinimumPlayers}.",
                onlinePlayers,
                _config.MinimumPlayersToStart);
            return HookResult.Continue;
        }

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
                    Announce("WinKillFrenzy", attacker.Name, newCount, target, ap);
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
                    Announce("WinHeadshotKing", attacker.Name, newCount, target, ap);
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
                Announce("WinKnifeKill", attacker.Name, ap);
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
                    Announce("WinGrenadeKing", attacker.Name, newCount, target, ap);
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
        if (_activeEvent == MegaEventType.None) return HookResult.Continue;
        var resolvedEvent = _isHappyHour ? _innerEvent : _activeEvent;

        var attacker = @event.AttackerPlayer;
        if (attacker == null || !attacker.IsValid || attacker.SteamID == 0) return HookResult.Continue;

        int dmg = @event.DmgHealth;
        if (dmg <= 0) return HookResult.Continue;

        // ── Damage Marathon ────────────────────────────────────────────────────
        if (resolvedEvent == MegaEventType.DamageMarathon && !_eventCompleted)
        {
            _damageThisRound.TryGetValue(attacker.SteamID, out long prevDmg);
            long newDmg   = prevDmg + dmg;
            _damageThisRound[attacker.SteamID] = newDmg;

            long dmgTarget = _config.DamageMarathon.TargetDamage;
            if (newDmg >= dmgTarget)
            {
                _eventCompleted = true;
                int ap = ApplyHappyHour(_config.DamageMarathon.WinnerRewardAP);
                GiveAP(attacker, ap);
                DbRecordEvent(attacker.SteamID, ap);
                WriteLog("DamageMarathon", attacker.SteamID, attacker.Name, ap);
                Announce("WinDamageMarathon", attacker.Name, newDmg.ToString("N0"), ((long)dmgTarget).ToString("N0"), ap);
            }
        }

        // ── Zombie Kingpin (damage dealt by zombies to humans) ────────────────
        if (resolvedEvent == MegaEventType.ZombieKingpin)
        {
            bool attackerIsZombie = _zplApi != null && _zplApi.ZPL_IsZombie(attacker.PlayerID);
            var  victim           = @event.UserIdPlayer;
            bool victimIsHuman    = victim != null && victim.IsValid && _zplApi != null && !_zplApi.ZPL_IsZombie(victim.PlayerID);

            if (attackerIsZombie && victimIsHuman)
            {
                _zombieDmgThisRound.TryGetValue(attacker.SteamID, out long prevZDmg);
                _zombieDmgThisRound[attacker.SteamID] = prevZDmg + dmg;
            }
        }

        return HookResult.Continue;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  ZPL API callbacks
    // ─────────────────────────────────────────────────────────────────────────

    private void OnZPLPlayerInfect(IPlayer? attacker, IPlayer victim, bool byGrenade, string zombieClass)
    {
        try
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
                        WriteLog("InfectionRush", attacker.SteamID, attacker.Name, ap);
                        Announce("WinInfectionRush", attacker.Name, newInf, infTarget, ap);
                    }
                    break;

                case MegaEventType.ZombieArmada:
                    // Track infections to detect "all humans wiped" — handled via OnHumanWin(false)
                    break;
            }
        }
        catch (Exception ex)
        {
            LogExternalCallbackFailure(nameof(OnZPLPlayerInfect), ex);
        }
    }

    private void OnMotherZombieSelected(IPlayer player)
    {
        try
        {
            // Mother zombie is always the first zombie — track for infection rush purposes
            if (_activeEvent == MegaEventType.None) return;
            // No special handling needed; infection tracking fires on ZPL_OnPlayerInfect
        }
        catch (Exception ex)
        {
            LogExternalCallbackFailure(nameof(OnMotherZombieSelected), ex);
        }
    }

    private void TrackSpecialClass(IPlayer player, string className, int apReward)
    {
        if (_specialClassSteamId != 0) return; // only track first special class
        if (!player.IsValid || player.SteamID == 0) return;
        _specialClassSteamId = player.SteamID;
        _specialClassName    = className;
        _specialClassAP      = apReward;
    }

    private void OnNemesisSelected(IPlayer player)
    {
        try
        {
            TrackSpecialClass(player, "Nemesis", _config.SpecialClassBonus.NemesisWinAP);
        }
        catch (Exception ex)
        {
            LogExternalCallbackFailure(nameof(OnNemesisSelected), ex);
        }
    }

    private void OnAssassinSelected(IPlayer player)
    {
        try
        {
            TrackSpecialClass(player, "Assassin", _config.SpecialClassBonus.AssassinWinAP);
        }
        catch (Exception ex)
        {
            LogExternalCallbackFailure(nameof(OnAssassinSelected), ex);
        }
    }

    private void OnSurvivorSelected(IPlayer player)
    {
        try
        {
            TrackSpecialClass(player, "Survivor", _config.SpecialClassBonus.SurvivorWinAP);
        }
        catch (Exception ex)
        {
            LogExternalCallbackFailure(nameof(OnSurvivorSelected), ex);
        }
    }

    private void OnSniperSelected(IPlayer player)
    {
        try
        {
            TrackSpecialClass(player, "Sniper", _config.SpecialClassBonus.SniperWinAP);
        }
        catch (Exception ex)
        {
            LogExternalCallbackFailure(nameof(OnSniperSelected), ex);
        }
    }

    private void OnHumanWin(bool humansWon)
    {
        try
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
                                WriteLog("SpecialClass", _specialClassSteamId, player.Name, ap);
                                Announce("WinSpecialClass", player.Name, _specialClassName, ap);
                            }
                        }
                    }
                    break;
            }

            _activeEvent = MegaEventType.None;
        }
        catch (Exception ex)
        {
            LogExternalCallbackFailure(nameof(OnHumanWin), ex);
        }
    }

    private void SyncSpecialClassHookSubscription()
    {
        if (_zplApi == null)
        {
            _specialClassHooksSubscribed = false;
            return;
        }

        bool shouldSubscribe = _config.SpecialClassBonus.Enable && _config.SpecialClassBonus.EnableSelectionHooks;
        if (shouldSubscribe == _specialClassHooksSubscribed) return;

        try
        {
            if (shouldSubscribe)
            {
                _zplApi.ZPL_OnNemesisSelected  += _onNemesisSelected;
                _zplApi.ZPL_OnAssassinSelected += _onAssassinSelected;
                _zplApi.ZPL_OnSurvivorSelected += _onSurvivorSelected;
                _zplApi.ZPL_OnSniperSelected   += _onSniperSelected;
            }
            else
            {
                _zplApi.ZPL_OnNemesisSelected  -= _onNemesisSelected;
                _zplApi.ZPL_OnAssassinSelected -= _onAssassinSelected;
                _zplApi.ZPL_OnSurvivorSelected -= _onSurvivorSelected;
                _zplApi.ZPL_OnSniperSelected   -= _onSniperSelected;
            }

            _specialClassHooksSubscribed = shouldSubscribe;
            _logger?.LogInformation(
                "[MegaEvents] Special-class selection hooks {State} (Enable={Enable}, EnableSelectionHooks={Toggle}).",
                shouldSubscribe ? "enabled" : "disabled",
                _config.SpecialClassBonus.Enable,
                _config.SpecialClassBonus.EnableSelectionHooks);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("[MegaEvents] Failed to sync special-class hook subscriptions: {Ex}", ex.Message);
        }
    }

    private void LogExternalCallbackFailure(string callbackName, Exception ex)
        => _logger?.LogWarning("[MegaEvents] External callback {Callback} failed: {Ex}", callbackName, ex.Message);

    private bool IsSpecialClassSelectable()
        => _config.SpecialClassBonus.Enable
           && _config.SpecialClassBonus.EnableSelectionHooks
           && _zplApi != null
           && _specialClassHooksSubscribed;

    private void CmdMegaEventsStatus(ICommandContext context)
    {
        try
        {
            string activeName = (_isHappyHour ? _innerEvent : _activeEvent).ToString();
            int onlinePlayers = CountEligiblePlayers();
            context.Reply($"[MegaEvents] status: ActiveEvent={activeName}, HappyRound={_isHappyHour}, OnlinePlayers={onlinePlayers}, MinPlayers={_config.MinimumPlayersToStart}, ZPLAPI={(_zplApi != null)}, EconomyAPI={(_economyApi != null)}, SpecialHooksCfg={_config.SpecialClassBonus.EnableSelectionHooks}, SpecialHooksSubscribed={_specialClassHooksSubscribed}, SpecialClassSelectable={IsSpecialClassSelectable()}, DBReady={_dbReady}");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("[MegaEvents] Status command failed: {Ex}", ex.Message);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Reward helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void RewardSurvivingHumans()
    {
        if (_economyApi == null) return;
        int ap    = ApplyHappyHour(_config.FortressDefense.RewardAP);
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
            Announce("WinFortressDefense", count, ap);
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
            Announce("WinZombieArmada", count, ap);
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

            if (entry.EventType == MegaEventType.SpecialClass && !IsSpecialClassSelectable())
            {
                _logger?.LogInformation(
                    "[MegaEvents] Scheduled SpecialClass skipped because selection hooks are not operational (Enable={Enable}, EnableSelectionHooks={Toggle}, ZPLAPI={HasApi}, Subscribed={Subscribed}).",
                    _config.SpecialClassBonus.Enable,
                    _config.SpecialClassBonus.EnableSelectionHooks,
                    _zplApi != null,
                    _specialClassHooksSubscribed);
                continue;
            }

            return entry.EventType;
        }

        return null;
    }

    private List<(MegaEventType evt, int weight)> BuildEventPool()
    {
        var pool = new List<(MegaEventType, int)>(13);
        if (_config.InfectionRush.Enable    && _config.InfectionRush.Weight    > 0) pool.Add((MegaEventType.InfectionRush,   _config.InfectionRush.Weight));
        if (_config.KillFrenzy.Enable       && _config.KillFrenzy.Weight       > 0) pool.Add((MegaEventType.KillFrenzy,      _config.KillFrenzy.Weight));
        if (_config.FortressDefense.Enable  && _config.FortressDefense.Weight  > 0) pool.Add((MegaEventType.FortressDefense, _config.FortressDefense.Weight));
        if (_config.ZombieArmada.Enable     && _config.ZombieArmada.Weight     > 0) pool.Add((MegaEventType.ZombieArmada,    _config.ZombieArmada.Weight));
        if (_config.DamageMarathon.Enable   && _config.DamageMarathon.Weight   > 0) pool.Add((MegaEventType.DamageMarathon,  _config.DamageMarathon.Weight));
        if (IsSpecialClassSelectable() && _config.SpecialClassBonus.Weight > 0) pool.Add((MegaEventType.SpecialClass,  _config.SpecialClassBonus.Weight));
        if (_config.GiveAway.Enable         && _config.GiveAway.Weight         > 0) pool.Add((MegaEventType.GiveAway,        _config.GiveAway.Weight));
        if (_config.HeadshotKing.Enable     && _config.HeadshotKing.Weight     > 0) pool.Add((MegaEventType.HeadshotKing,    _config.HeadshotKing.Weight));
        if (_config.KnifeKill.Enable        && _config.KnifeKill.Weight        > 0) pool.Add((MegaEventType.KnifeKill,       _config.KnifeKill.Weight));
        if (_config.GrenadeKing.Enable      && _config.GrenadeKing.Weight      > 0) pool.Add((MegaEventType.GrenadeKing,     _config.GrenadeKing.Weight));
        if (_config.MVPRound.Enable         && _config.MVPRound.Weight         > 0) pool.Add((MegaEventType.MVPRound,        _config.MVPRound.Weight));
        if (_config.ZombieKingpin.Enable    && _config.ZombieKingpin.Weight    > 0) pool.Add((MegaEventType.ZombieKingpin,   _config.ZombieKingpin.Weight));
        if (_config.DoubleDown.Enable       && _config.DoubleDown.Weight       > 0) pool.Add((MegaEventType.DoubleDown,      _config.DoubleDown.Weight));
        return pool;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Announcement helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void AnnounceEventStart()
    {
        if (!_config.EnableAnnouncements && !_config.EnableCenterAnnouncements) return;
        var resolvedEvent = _isHappyHour ? _innerEvent : _activeEvent;

        // Happy hour bonus announcement
        if (_isHappyHour)
            BroadcastAnnouncementT("HappyHourActive", _config.HappyHour.Multiplier.ToString("F1"));

        // Per-event start message
        switch (resolvedEvent)
        {
            case MegaEventType.InfectionRush:
                BroadcastAnnouncementT("StartInfectionRush",   _config.InfectionRush.TargetInfections, _config.InfectionRush.WinnerRewardAP);   break;
            case MegaEventType.KillFrenzy:
                BroadcastAnnouncementT("StartKillFrenzy",      _config.KillFrenzy.TargetKills,         _config.KillFrenzy.WinnerRewardAP);      break;
            case MegaEventType.FortressDefense:
                BroadcastAnnouncementT("StartFortressDefense", _config.FortressDefense.RewardAP);                                               break;
            case MegaEventType.ZombieArmada:
                BroadcastAnnouncementT("StartZombieArmada",    _config.ZombieArmada.RewardAP);                                                  break;
            case MegaEventType.DamageMarathon:
                BroadcastAnnouncementT("StartDamageMarathon",  ((long)_config.DamageMarathon.TargetDamage).ToString("N0"), _config.DamageMarathon.WinnerRewardAP); break;
            case MegaEventType.SpecialClass:
                BroadcastAnnouncementT("StartSpecialClass");                                                                                     break;
            case MegaEventType.GiveAway:
                BroadcastAnnouncementT("StartGiveAway",        _config.GiveAway.WinnerRewardAP,    _config.GiveAway.ConsolationAP);             break;
            case MegaEventType.HeadshotKing:
                BroadcastAnnouncementT("StartHeadshotKing",    _config.HeadshotKing.TargetHeadshots, _config.HeadshotKing.WinnerRewardAP);      break;
            case MegaEventType.KnifeKill:
                BroadcastAnnouncementT("StartKnifeKill",       _config.KnifeKill.WinnerRewardAP);                                              break;
            case MegaEventType.GrenadeKing:
                BroadcastAnnouncementT("StartGrenadeKing",     _config.GrenadeKing.TargetGrenadeKills, _config.GrenadeKing.WinnerRewardAP);    break;
            case MegaEventType.MVPRound:
                BroadcastAnnouncementT("StartMVPRound",        _config.MVPRound.WinnerRewardAP);                                               break;
            case MegaEventType.ZombieKingpin:
                BroadcastAnnouncementT("StartZombieKingpin",   _config.ZombieKingpin.WinnerRewardAP);                                          break;
            case MegaEventType.DoubleDown:
                BroadcastAnnouncementT("StartDoubleDown",      _config.DoubleDown.RewardAP);                                                   break;
        }
    }

    private void AnnounceMinimumPlayersNotMet(bool isMapGiveAway, int onlinePlayers)
    {
        if (!_config.EnableAnnouncements && !_config.EnableCenterAnnouncements) return;

        BroadcastAnnouncementT(
            isMapGiveAway ? "SkipMapGiveAwayMinimumPlayers" : "SkipRoundMinimumPlayers",
            _config.MinimumPlayersToStart,
            onlinePlayers);
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
                    BroadcastChatT("ProgressInfectionRush", name, count, _config.InfectionRush.TargetInfections);
                break;
            }
            case MegaEventType.KillFrenzy:
            {
                var (name, count) = GetLeader(_killsThisRound);
                if (count > 0)
                    BroadcastChatT("ProgressKillFrenzy", name, count, _config.KillFrenzy.TargetKills);
                break;
            }
            case MegaEventType.DamageMarathon:
            {
                var (name, dmg) = GetLeaderLong(_damageThisRound);
                if (dmg > 0)
                    BroadcastChatT("ProgressDamageMarathon", name, dmg.ToString("N0"), ((long)_config.DamageMarathon.TargetDamage).ToString("N0"));
                break;
            }
            case MegaEventType.HeadshotKing:
            {
                var (name, count) = GetLeader(_headshotsThisRound);
                if (count > 0)
                    BroadcastChatT("ProgressHeadshotKing", name, count, _config.HeadshotKing.TargetHeadshots);
                break;
            }
            case MegaEventType.GrenadeKing:
            {
                var (name, count) = GetLeader(_grenadeKillsRound);
                if (count > 0)
                    BroadcastChatT("ProgressGrenadeKing", name, count, _config.GrenadeKing.TargetGrenadeKills);
                break;
            }
            case MegaEventType.MVPRound:
            {
                var (name, count) = GetLeader(_totalKillsThisRound);
                if (count > 0)
                    BroadcastChatT("ProgressMVPRound", name, count);
                break;
            }
            case MegaEventType.ZombieKingpin:
            {
                var (name, dmg) = GetLeaderLong(_zombieDmgThisRound);
                if (dmg > 0)
                    BroadcastChatT("ProgressZombieKingpin", name, dmg.ToString("N0"));
                break;
            }
        }
    }

    private void AnnounceNoWinner()
    {
        if (!_config.EnableAnnouncements) return;
        var resolvedEvent = _isHappyHour ? _innerEvent : _activeEvent;
        string? key = resolvedEvent switch
        {
            MegaEventType.InfectionRush  => "NoWinnerInfectionRush",
            MegaEventType.KillFrenzy     => "NoWinnerKillFrenzy",
            MegaEventType.DamageMarathon => "NoWinnerDamageMarathon",
            MegaEventType.HeadshotKing   => "NoWinnerHeadshotKing",
            MegaEventType.GrenadeKing    => "NoWinnerGrenadeKing",
            MegaEventType.KnifeKill      => "NoWinnerKnifeKill",
            _                            => null
        };
        if (key != null) BroadcastChatT(key);
    }

    private void Announce(string key, params object[] args)
    {
        if (!_config.EnableAnnouncements) return;
        BroadcastChatT(key, args);
    }

    private void BroadcastChatT(string key, params object[] args)
    {
        foreach (var p in Core.PlayerManager.GetAllPlayers())
        {
            if (p == null || !p.IsValid || p.IsFakeClient) continue;
            p.SendMessage(MessageType.Chat, $" {_config.ChatPrefix} {T(p, key, args)}");
        }
    }

    private void BroadcastAnnouncementT(string key, params object[] args)
    {
        if (_config.EnableAnnouncements)
            BroadcastChatT(key, args);

        if (_config.EnableCenterAnnouncements)
            BroadcastCenterT(key, args);
    }

    private void BroadcastCenterT(string key, params object[] args)
    {
        foreach (var p in Core.PlayerManager.GetAllPlayers())
        {
            if (p == null || !p.IsValid || p.IsFakeClient) continue;
            p.SendMessage(MessageType.Center, StripColorTags(T(p, key, args)));
        }
    }

    private string T(IPlayer player, string key, params object[] args)
    {
        var loc = Core.Translation.GetPlayerLocalizer(player);
        return args.Length == 0 ? loc[key] : loc[key, args];
    }

    private static string StripColorTags(string text)
        => System.Text.RegularExpressions.Regex.Replace(text, @"\[[^\]]+\]", string.Empty);

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

    private int CountEligiblePlayers()
    {
        int count = 0;
        foreach (var player in Core.PlayerManager.GetAllPlayers())
        {
            if (player == null || !player.IsValid || player.IsFakeClient || player.SteamID == 0)
                continue;

            count++;
        }

        return count;
    }

    private bool HasMinimumPlayersForEventStart(int onlinePlayers)
        => _config.MinimumPlayersToStart <= 1 || onlinePlayers >= _config.MinimumPlayersToStart;

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

    // ─────────────────────────────────────────────────────────────────────────
    //  Map lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void OnMapLoad(IOnMapLoadEvent @event)
    {
        _mapGiveAwayDone = false;

        if (!_config.MapGiveAway.Enable) return;

        // Respect optional scheduled-window restriction
        if (_config.MapGiveAway.OnlyDuringScheduledWindow && FindScheduledEventType() == null)
            return;

        float delay = Math.Max(1f, _config.MapGiveAway.DelaySeconds);
        Core.Scheduler.DelayBySeconds(delay, RunMapGiveAway);
    }

    private void OnMapUnload(IOnMapUnloadEvent @event)
    {
        _roundCts?.Cancel();
        _roundCts?.Dispose();
        _roundCts = null;
        _activeEvent = MegaEventType.None;
        _mapGiveAwayDone = false;
    }

    private void RunMapGiveAway()
    {
        if (_mapGiveAwayDone) return;
        _mapGiveAwayDone = true;

        int onlinePlayers = CountEligiblePlayers();
        if (!HasMinimumPlayersForEventStart(onlinePlayers))
        {
            AnnounceMinimumPlayersNotMet(true, onlinePlayers);
            _logger?.LogDebug(
                "[MegaEvents] Skipping map giveaway: online players {OnlinePlayers} below minimum {MinimumPlayers}.",
                onlinePlayers,
                _config.MinimumPlayersToStart);
            return;
        }

        var players = Core.PlayerManager.GetAllPlayers()
            .Where(p => p != null && p.IsValid && !p.IsFakeClient && p.SteamID != 0)
            .ToList();

        if (players.Count == 0) return;

        int winnerIndex = _rng.Next(players.Count);
        var winner      = players[winnerIndex];

        int winAP   = _config.MapGiveAway.WinnerRewardAP;
        int consAP  = _config.MapGiveAway.ConsolationAP;

        GiveAP(winner, winAP);
        DbRecordEvent(winner.SteamID, winAP);
        WriteLog("MapGiveAway", winner.SteamID, winner.Name, winAP);

        foreach (var p in players)
        {
            if (p.SteamID == winner.SteamID) continue;
            GiveAP(p, consAP);
        }

        Announce("WinMapGiveAway", winner.Name, winAP, consAP);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Immediate-resolution event helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void ResolveGiveAway()
    {
        var players = Core.PlayerManager.GetAllPlayers()
            .Where(p => p != null && p.IsValid && !p.IsFakeClient && p.SteamID != 0)
            .ToList();

        if (players.Count == 0) return;

        int winnerIndex = _rng.Next(players.Count);
        var winner      = players[winnerIndex];

        int winAP  = ApplyHappyHour(_config.GiveAway.WinnerRewardAP);
        int consAP = ApplyHappyHour(_config.GiveAway.ConsolationAP);

        GiveAP(winner, winAP);
        DbRecordEvent(winner.SteamID, winAP);
        WriteLog("GiveAway", winner.SteamID, winner.Name, winAP);

        foreach (var p in players)
        {
            if (p.SteamID == winner.SteamID) continue;
            GiveAP(p, consAP);
        }

        _eventCompleted = true;
        Announce("WinGiveAway", winner.Name, winAP, consAP);
    }

    private void ResolveDoubleDown()
    {
        int ap    = ApplyHappyHour(_config.DoubleDown.RewardAP);
        int count = 0;
        foreach (var p in Core.PlayerManager.GetAllPlayers())
        {
            if (p == null || !p.IsValid || p.IsFakeClient) continue;
            GiveAP(p, ap);
            count++;
        }
        _eventCompleted = true;
        if (count > 0)
            Announce("WinDoubleDown", count, ap);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Round-end resolution helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void ResolveMVPRound()
    {
        if (_totalKillsThisRound.Count == 0) return;

        var (winnerName, winnerKills, winnerSid) = GetLeaderWithSteamId(_totalKillsThisRound);
        if (winnerSid == 0 || winnerKills == 0) return;

        int winAP  = ApplyHappyHour(_config.MVPRound.WinnerRewardAP);
        var winner = FindPlayerBySteamId(winnerSid);
        if (winner != null)
        {
            GiveAP(winner, winAP);
            DbRecordEvent(winnerSid, winAP);
            WriteLog("MVPRound", winnerSid, winnerName, winAP);
        }

        int consAP = ApplyHappyHour(_config.MVPRound.ParticipantRewardAP);
        GiveParticipantAP(_totalKillsThisRound, consAP, excludeLeader: true);

        Announce("WinMVPRound", winnerName, winnerKills, winAP);
    }

    private void ResolveZombieKingpin()
    {
        if (_zombieDmgThisRound.Count == 0) return;

        var (winnerName, winnerDmg, winnerSid) = GetLeaderLongWithSteamId(_zombieDmgThisRound);
        if (winnerSid == 0 || winnerDmg == 0) return;

        int winAP  = ApplyHappyHour(_config.ZombieKingpin.WinnerRewardAP);
        var winner = FindPlayerBySteamId(winnerSid);
        if (winner != null)
        {
            GiveAP(winner, winAP);
            DbRecordEvent(winnerSid, winAP);
            WriteLog("ZombieKingpin", winnerSid, winnerName, winAP);
        }

        int consAP = ApplyHappyHour(_config.ZombieKingpin.ParticipantRewardAP);
        GiveParticipantAP(_zombieDmgThisRound, consAP, excludeLeader: true);

        Announce("WinZombieKingpin", winnerName, winnerDmg.ToString("N0"), winAP);
    }

    private void ResolveHeadshotKing()
    {
        if (_headshotsThisRound.Count == 0) { AnnounceNoWinner(); return; }

        var (winnerName, winnerCount, winnerSid) = GetLeaderWithSteamId(_headshotsThisRound);
        if (winnerSid == 0 || winnerCount == 0) { AnnounceNoWinner(); return; }

        int winAP  = ApplyHappyHour(_config.HeadshotKing.WinnerRewardAP);
        var winner = FindPlayerBySteamId(winnerSid);
        if (winner != null)
        {
            GiveAP(winner, winAP);
            DbRecordEvent(winnerSid, winAP);
            WriteLog("HeadshotKing", winnerSid, winnerName, winAP);
        }

        GiveParticipantAP(_headshotsThisRound, ApplyHappyHour(_config.HeadshotKing.ParticipantRewardAP), excludeLeader: true);
        Announce("WinHeadshotKing", winnerName, winnerCount, _config.HeadshotKing.TargetHeadshots, winAP);
    }

    private void ResolveGrenadeKing()
    {
        if (_grenadeKillsRound.Count == 0) { AnnounceNoWinner(); return; }

        var (winnerName, winnerCount, winnerSid) = GetLeaderWithSteamId(_grenadeKillsRound);
        if (winnerSid == 0 || winnerCount == 0) { AnnounceNoWinner(); return; }

        int winAP  = ApplyHappyHour(_config.GrenadeKing.WinnerRewardAP);
        var winner = FindPlayerBySteamId(winnerSid);
        if (winner != null)
        {
            GiveAP(winner, winAP);
            DbRecordEvent(winnerSid, winAP);
            WriteLog("GrenadeKing", winnerSid, winnerName, winAP);
        }

        GiveParticipantAP(_grenadeKillsRound, ApplyHappyHour(_config.GrenadeKing.ParticipantRewardAP), excludeLeader: true);
        Announce("WinGrenadeKing", winnerName, winnerCount, _config.GrenadeKing.TargetGrenadeKills, winAP);
    }

    /// <summary>
    /// Gives <paramref name="ap"/> to every player who has an entry (count &gt; 0) in
    /// <paramref name="dict"/>, optionally excluding the leader (highest count).
    /// </summary>
    private void GiveParticipantAP(Dictionary<ulong, int> dict, int ap, bool excludeLeader)
    {
        if (ap <= 0 || dict.Count == 0) return;

        ulong leaderSid = 0;
        if (excludeLeader)
        {
            int best = 0;
            foreach (var (sid, c) in dict)
                if (c > best) { best = c; leaderSid = sid; }
        }

        foreach (var (sid, count) in dict)
        {
            if (count <= 0) continue;
            if (excludeLeader && sid == leaderSid) continue;
            var p = FindPlayerBySteamId(sid);
            if (p != null) GiveAP(p, ap);
        }
    }

    /// <summary>Same as <see cref="GiveParticipantAP(Dictionary{ulong,int},int,bool)"/> but for long-keyed dicts.</summary>
    private void GiveParticipantAP(Dictionary<ulong, long> dict, int ap, bool excludeLeader)
    {
        if (ap <= 0 || dict.Count == 0) return;

        ulong leaderSid = 0;
        if (excludeLeader)
        {
            long best = 0;
            foreach (var (sid, c) in dict)
                if (c > best) { best = c; leaderSid = sid; }
        }

        foreach (var (sid, count) in dict)
        {
            if (count <= 0) continue;
            if (excludeLeader && sid == leaderSid) continue;
            var p = FindPlayerBySteamId(sid);
            if (p != null) GiveAP(p, ap);
        }
    }

    private static bool IsGrenadeWeapon(string weapon) =>
        weapon is "hegrenade" or "weapon_hegrenade"
               or "molotov"   or "weapon_molotov"
               or "incgrenade" or "weapon_incgrenade"
               or "flashbang" or "weapon_flashbang"
               or "smokegrenade" or "weapon_smokegrenade";

    // ─────────────────────────────────────────────────────────────────────────
    //  File logging
    // ─────────────────────────────────────────────────────────────────────────

    private void WriteLog(string eventName, ulong steamId, string playerName, int ap)
    {
        if (!_config.Logging.Enable) return;
        try
        {
            string dir  = _config.Logging.LogDirectory;
            if (string.IsNullOrWhiteSpace(dir)) return;
            if (!Path.IsPathRooted(dir))
                dir = Path.Combine(AppContext.BaseDirectory, dir);
            Directory.CreateDirectory(dir);

            string month = DateTime.UtcNow.ToString("yyyy-MM");
            string file  = Path.Combine(dir, $"ZPLMegaEvents_{month}.log");
            string line  = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [{eventName}] {playerName} (STEAM:{steamId}) +{ap} AP";
            File.AppendAllText(file, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("[MegaEvents] WriteLog failed: {Ex}", ex.Message);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Extended leader helpers (return SteamID alongside name+count)
    // ─────────────────────────────────────────────────────────────────────────

    private (string name, int count, ulong steamId) GetLeaderWithSteamId(Dictionary<ulong, int> dict)
    {
        ulong bestSid   = 0;
        int   bestCount = 0;
        foreach (var (sid, c) in dict)
            if (c > bestCount) { bestCount = c; bestSid = sid; }
        if (bestSid == 0) return (string.Empty, 0, 0);
        var p = FindPlayerBySteamId(bestSid);
        return (p?.Name ?? bestSid.ToString(), bestCount, bestSid);
    }

    private (string name, long count, ulong steamId) GetLeaderLongWithSteamId(Dictionary<ulong, long> dict)
    {
        ulong bestSid   = 0;
        long  bestCount = 0;
        foreach (var (sid, c) in dict)
            if (c > bestCount) { bestCount = c; bestSid = sid; }
        if (bestSid == 0) return (string.Empty, 0, 0);
        var p = FindPlayerBySteamId(bestSid);
        return (p?.Name ?? bestSid.ToString(), bestCount, bestSid);
    }
}
