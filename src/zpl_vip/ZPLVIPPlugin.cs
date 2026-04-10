using System.Drawing;
using Economy.Contract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;
using ZombiePlagueLegacyCS2;

namespace ZPLVIP;

[PluginMetadata(
    Id = "ZPLVIP",
    Version = "2.0.0",
    Name = "ZombiePlagueLegacyCS2 VIP",
    Author = "DeadPoolCS2",
    Description = "VIP perks for ZombiePlagueLegacyCS2: armor, multi-jump, no fall damage, damage bonus, AP rewards, Happy Hour.")]
public class ZPLVIPPlugin(ISwiftlyCore core) : BasePlugin(core)
{
    private ILogger<ZPLVIPPlugin> _logger = null!;
    private IOptionsMonitor<ZPLVIPConfig>? _cfgMonitor;
    private ZPLVIPConfig _config = new();
    private ServiceProvider? _sp;

    // ZPL API — zombie-state detection and infect events.
    private IZombiePlagueLegacyAPI? _zplApi;

    // Economy API — AP wallet management.
    private IEconomyAPIv1? _economyApi;

    // ── Per-round / per-session player state ──────────────────────────────────

    // Accumulated zombie damage toward the next AP reward, keyed by PlayerID.
    private readonly Dictionary<int, int> _damageAccumulator = new();
    // Remaining extra mid-air jumps this airborne sequence, keyed by PlayerID.
    private readonly Dictionary<int, int> _extraJumpsRemaining = new();
    // Whether SPACE was pressed last tick (rising-edge detection), keyed by PlayerID.
    private readonly Dictionary<int, bool> _prevJumpPressed = new();

    // ── Plugin lifecycle ──────────────────────────────────────────────────────

    private const string ConfigFile = "ZPLVIP.jsonc";

    public override void Load(bool hotReload)
    {
        // Bind config from configs/plugins/ZPLVIP/ZPLVIP.jsonc.
        // reloadOnChange: true ensures IOptionsMonitor reflects edits made to
        // the file at runtime (chat prefix, permissions, perk values, etc.).
        // Guard with !hotReload: SwiftlyS2's PluginConfigurationService.Manager is a
        // lazy singleton that is never reset between reloads.  Calling AddJsonFile on
        // it again on every Load() appends a new FileSystemWatcher thread to the same
        // ConfigurationManager, leaking one watcher thread per map change.
        if (!hotReload)
            Core.Configuration.InitializeJsonWithModel<ZPLVIPConfig>(ConfigFile, "ZPLVIP")
                .Configure(builder =>
                {
                    builder.AddJsonFile(ConfigFile, false, true);
                    builder.SetFileLoadExceptionHandler(ctx =>
                    {
                        Core.Logger.LogError("[ZPLVIP] Failed to load {File}: {Error}. Using last valid configuration.", ConfigFile, ctx.Exception.Message);
                        ctx.Ignore = true;
                    });
                });

        var services = new ServiceCollection();
        services.AddSwiftly(Core);
        services.AddSingleton<ISwiftlyCore>(Core);
        services.AddOptions<ZPLVIPConfig>().BindConfiguration("ZPLVIP");

        _sp = services.BuildServiceProvider();
        _logger = _sp.GetRequiredService<ILogger<ZPLVIPPlugin>>();

        _cfgMonitor = _sp.GetRequiredService<IOptionsMonitor<ZPLVIPConfig>>();
        _config = _cfgMonitor.CurrentValue;
        _cfgMonitor.OnChange(cfg =>
        {
            _config = cfg;
        });

        // Game-event hooks.
        Core.GameEvent.HookPre<EventPlayerSpawn>(OnPlayerSpawn);
        Core.GameEvent.HookPre<EventPlayerDeath>(OnPlayerDeath);
        Core.GameEvent.HookPre<EventRoundEnd>(OnRoundEnd);
        Core.Event.OnEntityTakeDamage += OnEntityTakeDamage;
        Core.Event.OnTick += OnTick;
        Core.Event.OnClientConnected += OnClientConnected;
        Core.Event.OnClientDisconnected += OnClientDisconnected;

        // Register chat commands.
        Core.Command.RegisterCommand(_config.VipMenuCommand, CmdVipMenu, true);
        Core.Command.RegisterCommand(_config.VipsListCommand, CmdVipsList, true);
    }

    /// <summary>
    /// Called by SwiftlyS2 after all plugins have loaded their shared interfaces.
    /// Connects to IZombiePlagueLegacyAPI (zombie state + infect events) and
    /// IEconomyAPIv1 (AP wallet management).
    /// </summary>
    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        // ── ZPL API ────────────────────────────────────────────────────────────
        if (interfaceManager.HasSharedInterface("ZombiePlagueLegacy"))
        {
            try
            {
                _zplApi = interfaceManager.GetSharedInterface<IZombiePlagueLegacyAPI>("ZombiePlagueLegacy");
                if (_zplApi != null)
                {
                    if (_config.InfectRewardsEnabled)
                        _zplApi.ZPL_OnPlayerInfect += OnZPLPlayerInfect;
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("[ZPLVIP] Failed to acquire ZombiePlagueLegacyCS2 API: {Error} – zombie detection falls back to team check (T = zombie).", ex.Message);
            }
        }
        else
        {
            _logger.LogWarning("[ZPLVIP] ZombiePlagueLegacyCS2 API not found – zombie detection falls back to team check (T = zombie).");
        }

        // ── Economy API ───────────────────────────────────────────────────────
        if (interfaceManager.HasSharedInterface("Economy.API.v1"))
        {
            try
            {
                _economyApi = interfaceManager.GetSharedInterface<IEconomyAPIv1>("Economy.API.v1");
                if (_economyApi != null)
                {
                    // Ensure the wallet kind exists (same as ZPL does).
                    if (!_economyApi.WalletKindExists(_config.WalletKind))
                        _economyApi.EnsureWalletKind(_config.WalletKind);
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("[ZPLVIP] Failed to acquire Economy API: {Error} – AP rewards will be announced in chat only.", ex.Message);
            }
        }
        else
        {
            _logger.LogWarning("[ZPLVIP] Economy API not found – AP rewards will be announced in chat only.");
        }
    }

    public override void Unload()
    {
        if (_zplApi != null)
            _zplApi.ZPL_OnPlayerInfect -= OnZPLPlayerInfect;

        // Close all open menus before unloading so that SwiftlyS2's per-player
        // render timers cannot fire on freed objects after hot-reload.
        foreach (var player in Core.PlayerManager.GetAllPlayers())
        {
            if (player != null && player.IsValid)
                Core.MenusAPI.CloseActiveMenu(player);
        }

        // Unregister all event hooks so that stale delegates from the previous
        // load do not accumulate on hot-reload (map change) and cause double
        // event processing or memory leaks.
        Core.GameEvent.UnhookPre<EventPlayerSpawn>();
        Core.GameEvent.UnhookPre<EventPlayerDeath>();
        Core.GameEvent.UnhookPre<EventRoundEnd>();
        Core.Event.OnEntityTakeDamage   -= OnEntityTakeDamage;
        Core.Event.OnTick               -= OnTick;
        Core.Event.OnClientConnected    -= OnClientConnected;
        Core.Event.OnClientDisconnected -= OnClientDisconnected;

        _sp?.Dispose();
        _sp = null;
    }

    // ── VIP & zombie-state helpers ────────────────────────────────────────────

    private static bool IsValidRealPlayer(IPlayer? player)
        => player != null && player.IsValid && !player.IsFakeClient && player.SteamID != 0;

    private bool IsVIP(IPlayer player)
    {
        if (player == null || !player.IsValid) return false;
        ulong steamId = player.SteamID;
        if (steamId == 0) return false;

        var permString = _config.VIPPermission;
        if (string.IsNullOrWhiteSpace(permString))
            return true; // Empty = everyone is VIP (testing mode).

        foreach (var perm in permString.Split(','))
        {
            var p = perm.Trim();
            if (p.Length > 0 && Core.Permission.PlayerHasPermission(steamId, p))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true when the player is on the zombie side.
    /// Uses IZombiePlagueLegacyAPI when available; falls back to team check.
    /// </summary>
    private bool IsZombie(int playerId)
    {
        if (_zplApi != null)
            return _zplApi.ZPL_IsZombie(playerId);

        // Fallback: T-side = zombie in a standard ZPL server setup.
        var player = Core.PlayerManager.GetPlayer(playerId);
        return player?.Controller?.Team == Team.T;
    }

    // ── Translation helper ────────────────────────────────────────────────────

    private string T(IPlayer player, string key, params object[] args)
    {
        var loc = Core.Translation.GetPlayerLocalizer(player);
        return args.Length == 0 ? loc[key] : loc[key, args];
    }

    // ── AP management ─────────────────────────────────────────────────────────

    private void AddAmmoPacks(int playerId, int amount, IPlayer? player = null)
{
    if (amount <= 0) return;

    player ??= Core.PlayerManager.GetPlayer(playerId);
    if (player == null || !player.IsValid) return;

    if (_zplApi != null)
    {
        try
        {
            _zplApi.ZPL_AddAmmoPacks(playerId, amount);
            int total = _zplApi.ZPL_GetAmmoPacks(playerId);
            SendChat(player, T(player, "VipApReward", amount, total));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[ZPLVIP] AddAmmoPacks({Id},{Amount}) failed: {Ex}", playerId, amount, ex.Message);
        }
    }
    else
    {
        SendChat(player, T(player, "VipApBridgeOffline", amount));
    }
}

    // ── Event: player spawn ───────────────────────────────────────────────────

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event)
    {
        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid) return HookResult.Continue;

        var controller = @event.UserIdController;
        if (controller == null || !controller.IsValid) return HookResult.Continue;

        int id = player.PlayerID;

        // Reset per-spawn jump state immediately.
        _extraJumpsRemaining.Remove(id);
        _prevJumpPressed.Remove(id);

        // Defer one world-update tick so pawn state is fully initialised.
        Core.Scheduler.NextWorldUpdate(() =>
        {
            if (player == null || !player.IsValid) return;
            if (IsZombie(id)) return;           // VIP perks apply to humans only.
            if (!IsVIP(player)) return;

            var pawn = player.PlayerPawn;
            if (pawn == null || !pawn.IsValid) return;

            // ── Armor ──────────────────────────────────────────────────────────
            if (_config.ArmorAmount > 0 && pawn.ArmorValue < _config.ArmorAmount)
            {
                pawn.ArmorValue = _config.ArmorAmount;
                pawn.ArmorValueUpdated();
            }

            // ── Multi-jump allowance ───────────────────────────────────────────
            if (_config.ExtraJumps > 0)
                _extraJumpsRemaining[id] = _config.ExtraJumps;
        });

        return HookResult.Continue;
    }

    // ── Event: player death ───────────────────────────────────────────────────

    private HookResult OnPlayerDeath(EventPlayerDeath @event)
    {
        var victim = @event.UserIdPlayer;
        if (victim == null || !victim.IsValid) return HookResult.Continue;

        int victimId = victim.PlayerID;

        // Clean up jump state for the dying player.
        _extraJumpsRemaining.Remove(victimId);
        _prevJumpPressed.Remove(victimId);

        // Kill reward fires only when a VIP human kills a zombie.
        if (!IsZombie(victimId)) return HookResult.Continue;

        var attacker = Core.PlayerManager.GetPlayer(@event.Attacker);
        if (attacker == null || !attacker.IsValid) return HookResult.Continue;
        if (IsZombie(attacker.PlayerID) || !IsVIP(attacker)) return HookResult.Continue;

        // Base kill reward.
        if (_config.KillRewardAmount > 0)
            AddAmmoPacks(attacker.PlayerID, _config.KillRewardAmount, attacker);

        // Happy-hour bonuses.
        if (IsHappyHour())
        {
            if (_config.KillRewardHappyHourBonus && _config.HappyHourBonusAP > 0)
                AddAmmoPacks(attacker.PlayerID, _config.HappyHourBonusAP, attacker);

            if (_config.HappyHourBonusFrags > 0)
            {
                var atkCtrl = attacker.Controller;
                if (atkCtrl != null && atkCtrl.IsValid)
                {
                    var ats = atkCtrl.ActionTrackingServices;
                    if (ats != null && ats.IsValid)
                    {
                        ats.MatchStats.Kills += _config.HappyHourBonusFrags;
                        atkCtrl.CompetitiveRankingPredicted_Win++;
                    }
                }
            }
        }

        return HookResult.Continue;
    }

    // ── Event: round end ──────────────────────────────────────────────────────

    private HookResult OnRoundEnd(EventRoundEnd @event)
    {
        _damageAccumulator.Clear();
        _extraJumpsRemaining.Clear();
        _prevJumpPressed.Clear();
        return HookResult.Continue;
    }

    // ── Event: client connected ───────────────────────────────────────────────

    /// <summary>
    /// Broadcasts a join announcement to all players when a VIP connects.
    /// Fires once per server connection (not every round).
    /// </summary>
    private void OnClientConnected(IOnClientConnectedEvent @event)
    {
        if (!_config.JoinAnnounceEnabled) return;

        int playerId = @event.PlayerId;

        // Defer one tick so the player object and permissions are fully initialised.
        Core.Scheduler.NextWorldUpdate(() =>
        {
            var player = Core.PlayerManager.GetPlayer(playerId);
            if (!IsValidRealPlayer(player)) return;
            if (!IsVIP(player!)) return;

            string name = player!.Controller?.PlayerName ?? player.Name ?? "Player";

            // Send to each connected player using their own locale.
            foreach (var p in Core.PlayerManager.GetAllPlayers())
            {
                if (p == null || !p.IsValid || p.IsFakeClient) continue;
                p.SendMessage(MessageType.Chat,
                    $" {_config.ChatPrefix} {T(p, "VipJoinAnnounce", name)}");
            }
        });
    }

    // ── Event: client disconnected ────────────────────────────────────────────

    private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        int id = @event.PlayerId;

        // Close any open menu immediately so SwiftlyS2's per-player render timer
        // cannot fire on an already-freed native player controller and crash the
        // server with SIGSEGV (BuildMenuHtml null-dereference).
        var player = Core.PlayerManager.GetPlayer(id);
        if (player != null && player.IsValid)
            Core.MenusAPI.CloseActiveMenu(player);

        _damageAccumulator.Remove(id);
        _extraJumpsRemaining.Remove(id);
        _prevJumpPressed.Remove(id);
    }

    // ── Event: entity take damage ─────────────────────────────────────────────

    private void OnEntityTakeDamage(IOnEntityTakeDamageEvent @event)
    {
        // 1. Resolve the victim as a player.
        var victimPawn = @event.Entity?.As<CCSPlayerPawn>();
        if (victimPawn == null || !victimPawn.IsValid) return;

        var victimController = victimPawn.Controller.Value?.As<CCSPlayerController>();
        if (victimController == null || !victimController.IsValid) return;

        var victimPlayer = Core.PlayerManager.GetPlayerFromController(victimController);
        if (victimPlayer == null || !victimPlayer.IsValid) return;

        int victimId = victimPlayer.PlayerID;
        bool victimIsZombie = IsZombie(victimId);

        // 2. No-fall-damage perk: zero world / environmental damage for VIP humans.
        //    World damage has no valid player-pawn attacker.
        //    AmmoType == -1 reliably identifies non-weapon (world/fall) damage.
        if (!victimIsZombie && _config.NoFallDamage && IsVIP(victimPlayer))
        {
            var attackerEnt = @event.Info.Attacker.Value;
            var attackerAsPawn = attackerEnt?.As<CCSPlayerPawn>();
            bool isWorldDamage = attackerAsPawn == null || !attackerAsPawn.IsValid
                                 || @event.Info.AmmoType == -1;
            if (isWorldDamage)
            {
                @event.Info.Damage = 0f;
                return;
            }
        }

        // 3. Resolve the attacker as a player.
        var attackerPawn = @event.Info.Attacker.Value?.As<CCSPlayerPawn>();
        if (attackerPawn == null || !attackerPawn.IsValid) return;

        var attackerController = attackerPawn.Controller.Value?.As<CCSPlayerController>();
        if (attackerController == null || !attackerController.IsValid) return;

        var attackerPlayer = Core.PlayerManager.GetPlayerFromController(attackerController);
        if (attackerPlayer == null || !attackerPlayer.IsValid) return;

        int attackerId = attackerPlayer.PlayerID;
        bool attackerIsZombie = IsZombie(attackerId);

        // Only process VIP human → zombie damage from this point on.
        if (attackerIsZombie || !victimIsZombie || !IsVIP(attackerPlayer)) return;

        // 4. Damage multiplier.
        float mult = _config.DamageMultiplier;
        if (mult > 1.0f)
        {
            bool applyMult = true;
            if (_config.ExcludeHEGrenade)
            {
                var inflictor = @event.Info.Inflictor.Value;
                if (inflictor != null && inflictor.IsValid
                    && inflictor.DesignerName.Contains("hegrenade", StringComparison.OrdinalIgnoreCase))
                    applyMult = false;
            }
            if (applyMult)
                @event.Info.Damage *= mult;
        }

        // 5. Damage-based AP reward.
        int threshold = _config.DamageRewardThreshold;
        int rewardAmt = _config.DamageRewardAmount;
        if (threshold > 0 && rewardAmt > 0)
        {
            int dmg = (int)@event.Info.Damage;
            _damageAccumulator.TryGetValue(attackerId, out int acc);
            acc += dmg;
            int packs = acc / threshold;
            if (packs > 0)
            {
                acc -= packs * threshold;
                AddAmmoPacks(attackerId, packs * rewardAmt, attackerPlayer);
            }
            _damageAccumulator[attackerId] = acc;
        }
    }

    // ── Event: per-tick (multi-jump) ──────────────────────────────────────────

    private void OnTick()
    {
        if (_config.ExtraJumps <= 0) return;

        foreach (var player in Core.PlayerManager.GetAlive())
        {
            if (player == null || !player.IsValid) continue;

            int id = player.PlayerID;
            if (IsZombie(id) || !IsVIP(player)) continue;

            var pawn = player.PlayerPawn;
            if (pawn == null || !pawn.IsValid) continue;

            bool onGround = pawn.GroundEntity.IsValid;
            if (onGround)
            {
                // Replenish jump allowance when touching ground.
                _extraJumpsRemaining[id] = _config.ExtraJumps;
                _prevJumpPressed[id] = (player.PressedButtons & GameButtonFlags.Space) != 0;
                continue;
            }

            if (!_extraJumpsRemaining.TryGetValue(id, out int jumpsLeft) || jumpsLeft <= 0)
                continue;

            bool jumpNow = (player.PressedButtons & GameButtonFlags.Space) != 0;
            _prevJumpPressed.TryGetValue(id, out bool jumpPrev);
            _prevJumpPressed[id] = jumpNow;

            // Rising edge: button just pressed this tick.
            if (!jumpNow || jumpPrev) continue;

            _extraJumpsRemaining[id] = jumpsLeft - 1;
            var vel = pawn.AbsVelocity;
            pawn.Teleport(null, null,
                new SwiftlyS2.Shared.Natives.Vector(vel.X, vel.Y, _config.JumpVelocity));
        }
    }

    // ── ZPL infect-reward callback ─────────────────────────────────────────────

    /// <summary>
    /// Fires via IZombiePlagueLegacyAPI.ZPL_OnPlayerInfect when a player is infected.
    /// Awards AP and/or health to the VIP infector.
    /// </summary>
    private void OnZPLPlayerInfect(IPlayer attacker, IPlayer victim, bool grenade, string zombieClass)
    {
        if (!_config.InfectRewardsEnabled) return;
        if (attacker == null || !attacker.IsValid) return;
        if (!IsVIP(attacker)) return;

        int attackerId = attacker.PlayerID;

        if (_config.InfectRewardAP > 0)
            AddAmmoPacks(attackerId, _config.InfectRewardAP, attacker);

        if (_config.InfectRewardHealth > 0)
        {
            Core.Scheduler.NextWorldUpdate(() =>
            {
                if (attacker == null || !attacker.IsValid) return;
                var pawn = attacker.PlayerPawn;
                if (pawn == null || !pawn.IsValid) return;
                pawn.Health = Math.Min(pawn.Health + _config.InfectRewardHealth, pawn.MaxHealth);
                pawn.HealthUpdated();
            });
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>!vip – opens a SwiftlyS2 menu listing VIP benefits.</summary>
    private void CmdVipMenu(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null || !player.IsValid) return;

        bool isVip    = IsVIP(player);
        bool happyNow = IsHappyHour();

        var menuCfg = new MenuConfiguration
        {
            Title        = HtmlGradient.GenerateGradientText(_config.VipMenuTitle, Color.Gold, Color.Orange),
            FreezePlayer = false,
            MaxVisibleItems = 5,
            PlaySound    = false,
            AutoIncreaseVisibleItems = false,
            HideFooter   = false
        };

        IMenuAPI menu = Core.MenusAPI.CreateMenu(menuCfg, default, null, MenuOptionScrollStyle.LinearScroll);

        var lines = _config.BenefitLines;
        if (lines == null || lines.Count == 0)
        {
            // Auto-generate benefit lines from config values.
            AddBenefitLine(menu, T(player, "VipAutoLineArmor", _config.ArmorAmount));
            AddBenefitLine(menu, T(player, "VipAutoLineJumps", _config.ExtraJumps));
            string fallState = _config.NoFallDamage
                ? T(player, "VipAutoLineFallDmgDisabled")
                : T(player, "VipAutoLineFallDmgNormal");
            AddBenefitLine(menu, T(player, "VipAutoLineFallDmg", fallState));
            AddBenefitLine(menu, T(player, "VipAutoLineDmgMult", _config.DamageMultiplier.ToString("F1")));
            if (_config.KillRewardAmount > 0)
                AddBenefitLine(menu, T(player, "VipAutoLineKillReward", _config.KillRewardAmount));
            if (_config.DamageRewardThreshold > 0)
                AddBenefitLine(menu, T(player, "VipAutoLineDmgReward", _config.DamageRewardAmount, _config.DamageRewardThreshold));
            if (_config.HappyHourEnabled)
            {
                string tag = happyNow ? T(player, "VipAutoLineHappyHourActiveTag") : string.Empty;
                AddBenefitLine(menu, T(player, "VipAutoLineHappyHour", _config.HappyHourStart, _config.HappyHourEnd, tag));
                AddBenefitLine(menu, T(player, "VipAutoLineHappyHourBonus", _config.HappyHourBonusAP, _config.HappyHourBonusFrags));
            }
        }
        else
        {
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    AddBenefitLine(menu, line);
            }
        }

        // Status footer — colours come from translation tags.
        AddColoredLine(menu,
            isVip ? T(player, "VipMenuIsVip") : T(player, "VipMenuNotVip"));
        if (_config.HappyHourEnabled && happyNow)
            AddColoredLine(menu, T(player, "VipMenuHappyHourActive"));

        Core.MenusAPI.OpenMenuForPlayer(player, menu);
    }

    /// <summary>!vips – opens a SwiftlyS2 menu listing all VIP players currently online.</summary>
    private void CmdVipsList(ICommandContext context)
    {
        var caller = context.Sender;
        if (caller == null || !caller.IsValid) return;

        var vipNames = Core.PlayerManager.GetAllPlayers()
            .Where(p => p != null && p.IsValid && !p.IsFakeClient && IsVIP(p))
            .Select(p => p.Controller?.PlayerName ?? p.Name ?? "?")
            .ToList();

        var menuCfg = new MenuConfiguration
        {
            Title        = HtmlGradient.GenerateGradientText(T(caller, "VipsMenuTitle", vipNames.Count), Color.Gold, Color.Orange),
            FreezePlayer = false,
            MaxVisibleItems = 5,
            PlaySound    = false,
            AutoIncreaseVisibleItems = false,
            HideFooter   = false
        };

        IMenuAPI menu = Core.MenusAPI.CreateMenu(menuCfg, default, null, MenuOptionScrollStyle.LinearScroll);

        if (vipNames.Count == 0)
        {
            AddLine(menu, T(caller, "VipsMenuNoVips"));
        }
        else
        {
            foreach (var name in vipNames)
                AddLine(menu, T(caller, "VipsMenuEntry", name));
        }

        Core.MenusAPI.OpenMenuForPlayer(caller, menu);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void AddLine(IMenuAPI menu, string text)
        => menu.AddOption(new TextMenuOption(text, updateIntervalMs: 600, pauseIntervalMs: 100)
            { TextStyle = MenuOptionTextStyle.ScrollLeftLoop });

    /// <summary>Adds a benefit line with left-scrolling marquee text.</summary>
    private static void AddBenefitLine(IMenuAPI menu, string text)
        => menu.AddOption(new TextMenuOption(text, updateIntervalMs: 600, pauseIntervalMs: 100)
            { TextStyle = MenuOptionTextStyle.ScrollLeftLoop });

    /// <summary>Adds a status line with left-scrolling marquee text.</summary>
    private static void AddColoredLine(IMenuAPI menu, string text)
        => menu.AddOption(new TextMenuOption(text, updateIntervalMs: 600, pauseIntervalMs: 100)
            { TextStyle = MenuOptionTextStyle.ScrollLeftLoop });

    private bool IsHappyHour()
    {
        if (!_config.HappyHourEnabled) return false;
        int hour  = DateTime.Now.Hour;
        int start = _config.HappyHourStart;
        int end   = _config.HappyHourEnd;
        return start <= end
            ? hour >= start && hour < end
            : hour >= start || hour < end;
    }

    private void SendChat(IPlayer player, string msg)
        => player.SendMessage(MessageType.Chat, $" {_config.ChatPrefix} {msg}");

    private void BroadcastChat(string msg)
    {
        foreach (var p in Core.PlayerManager.GetAllPlayers())
        {
            if (p == null || !p.IsValid || p.IsFakeClient) continue;
            p.SendMessage(MessageType.Chat, msg);
        }
    }
}
