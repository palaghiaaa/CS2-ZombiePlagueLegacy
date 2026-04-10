using Economy.Contract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Core.Menus.OptionsBase;
using ZombiePlagueLegacyCS2;

namespace ZPLTeamBets;

/// <summary>Which team the player bet on.</summary>
public enum BetSide { Humans, Zombies }

[PluginMetadata(
    Id = "ZPLTeamBets",
    Version = "1.0.0",
    Name = "ZombiePlagueLegacyCS2 Team Bets",
    Author = "DeadPoolCS2",
    Description = "Place Ammo Pack bets on Humans or Zombies winning the round. Bets open during freeze time and pay out at round end.")]
public class ZPLTeamBetsPlugin(ISwiftlyCore core) : BasePlugin(core)
{
    private ILogger<ZPLTeamBetsPlugin> _logger = null!;
    private IOptionsMonitor<ZPLTeamBetsConfig>? _cfgMonitor;
    private ZPLTeamBetsConfig _config = new();
    private ServiceProvider? _sp;

    private IZombiePlagueLegacyAPI? _zplApi;
    private IEconomyAPIv1? _economyApi;

    // ── Round state ───────────────────────────────────────────────────────────

    /// <summary>True between EventRoundStart and EventRoundFreezeEnd (bets accepted).</summary>
    private bool _betsOpen;

    /// <summary>Keyed by PlayerID → (side, amount). Only one active bet per player per round.</summary>
    private readonly Dictionary<int, (BetSide Side, int Amount)> _activeBets = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private const string ConfigFile = "ZPLTeamBets.jsonc";

    public override void Load(bool hotReload)
    {
        if (!hotReload)
            Core.Configuration.InitializeJsonWithModel<ZPLTeamBetsConfig>(ConfigFile, "ZPLTeamBets")
                .Configure(builder =>
                {
                    builder.AddJsonFile(ConfigFile, false, true);
                    builder.SetFileLoadExceptionHandler(ctx =>
                    {
                        Core.Logger.LogError("[ZPLTeamBets] Failed to load {File}: {Error}. Using last valid configuration.",
                            ConfigFile, ctx.Exception.Message);
                        ctx.Ignore = true;
                    });
                });

        var services = new ServiceCollection();
        services.AddSwiftly(Core);
        services.AddSingleton<ISwiftlyCore>(Core);
        services.AddOptions<ZPLTeamBetsConfig>().BindConfiguration("ZPLTeamBets");

        _sp = services.BuildServiceProvider();
        _logger = _sp.GetRequiredService<ILogger<ZPLTeamBetsPlugin>>();
        _cfgMonitor = _sp.GetRequiredService<IOptionsMonitor<ZPLTeamBetsConfig>>();
        _config = _cfgMonitor.CurrentValue;
        _cfgMonitor.OnChange(cfg => _config = cfg);

        // Game events
        Core.GameEvent.HookPre<EventRoundStart>(OnRoundStart);
        Core.GameEvent.HookPre<EventRoundFreezeEnd>(OnRoundFreezeEnd);
        Core.GameEvent.HookPre<EventRoundEnd>(OnRoundEnd);
        Core.Event.OnClientDisconnected += OnClientDisconnected;

        // Register all configured bet commands
        foreach (var cmd in _config.BetCommands)
            Core.Command.RegisterCommand(cmd, CmdBet, true);
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        // ZPL API — available for future zombie-state queries if needed
        if (interfaceManager.HasSharedInterface("ZombiePlagueLegacy"))
        {
            try
            {
                _zplApi = interfaceManager.GetSharedInterface<IZombiePlagueLegacyAPI>("ZombiePlagueLegacy");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("[ZPLTeamBets] ZPL API unavailable: {Error}", ex.Message);
            }
        }

        // Economy API — required for AP balance reads/writes
        if (interfaceManager.HasSharedInterface("Economy.API.v1"))
        {
            try
            {
                _economyApi = interfaceManager.GetSharedInterface<IEconomyAPIv1>("Economy.API.v1");
                if (_economyApi != null && !_economyApi.WalletKindExists(_config.WalletKind))
                    _economyApi.EnsureWalletKind(_config.WalletKind);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("[ZPLTeamBets] Economy API unavailable: {Error} – bets disabled.", ex.Message);
            }
        }
        else
        {
            _logger.LogWarning("[ZPLTeamBets] Economy API not found – bets disabled.");
        }
    }

    public override void Unload()
    {
        // Close all open menus before unloading to prevent stale render timers (SIGSEGV).
        foreach (var player in Core.PlayerManager.GetAllPlayers())
        {
            if (player != null && player.IsValid)
                Core.MenusAPI.CloseActiveMenu(player);
        }

        Core.GameEvent.UnhookPre<EventRoundStart>();
        Core.GameEvent.UnhookPre<EventRoundFreezeEnd>();
        Core.GameEvent.UnhookPre<EventRoundEnd>();
        Core.Event.OnClientDisconnected -= OnClientDisconnected;

        foreach (var cmd in _config.BetCommands)
            Core.Command.UnregisterCommand(cmd);

        _activeBets.Clear();
        _betsOpen = false;
        _sp?.Dispose();
        _sp = null;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private HookResult OnRoundStart(EventRoundStart @event)
    {
        _activeBets.Clear();
        _betsOpen = true;
        BroadcastChatT("BetsOpenAnnounce", _config.BetCommands.FirstOrDefault() ?? "bet");
        return HookResult.Continue;
    }

    private HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event)
    {
        _betsOpen = false;
        if (_activeBets.Count > 0)
            BroadcastChatT("BetsLocked", _activeBets.Count);
        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event)
    {
        _betsOpen = false;

        if (_activeBets.Count == 0)
            return HookResult.Continue;

        // winner = 2 → T team (zombies in ZPL); winner = 3 → CT team (humans)
        int winner = @event.Winner;
        BetSide winningSide = winner == 3 ? BetSide.Humans : BetSide.Zombies;

        int winnersCount = 0;
        int totalPaid = 0;

        foreach (var kvp in _activeBets)
        {
            int playerId = kvp.Key;
            var (side, amount) = kvp.Value;

            var player = Core.PlayerManager.GetPlayer(playerId);
            if (player == null || !player.IsValid || player.IsFakeClient) continue;

            if (side == winningSide)
            {
                int payout = (int)(amount * _config.WinMultiplier);
                bool awarded = TryAddBalance(player, payout);
                if (awarded)
                {
                    winnersCount++;
                    totalPaid += payout;
                    SendChatT(player, "BetWon", amount, payout);
                }
            }
            else
            {
                SendChatT(player, "BetLost", amount);
            }
        }

        _activeBets.Clear();

        if (winnersCount > 0)
        {
            string teamKey = winningSide == BetSide.Humans ? "TeamHumans" : "TeamZombies";
            foreach (var p in Core.PlayerManager.GetAllPlayers())
            {
                if (p == null || !p.IsValid || p.IsFakeClient) continue;
                string teamName = T(p, teamKey);
                p.SendMessage(MessageType.Chat, $" {_config.ChatPrefix} {T(p, "RoundEndSummary", teamName, winnersCount, totalPaid)}");
            }
        }

        return HookResult.Continue;
    }

    private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        // Close any open menu immediately to prevent SIGSEGV from stale render timers.
        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player != null && player.IsValid)
            Core.MenusAPI.CloseActiveMenu(player);

        _activeBets.Remove(@event.PlayerId);
    }

    // ── Command: !bet ─────────────────────────────────────────────────────────

    private void CmdBet(ICommandContext context)
    {
        var caller = context.Sender;
        if (caller == null || !caller.IsValid || caller.IsFakeClient) return;

        if (_economyApi == null)
        {
            SendChatT(caller, "EconomyOffline");
            return;
        }

        var args = context.Args;

        // !bet <amount> <humans|zombies>  — direct form
        if (args.Length >= 2)
        {
            if (!int.TryParse(args[0], out int amount))
            {
                SendChatT(caller, "InvalidAmount");
                return;
            }

            string sideArg = args[1].ToLowerInvariant();
            BetSide? side = sideArg is "humans" or "human" or "ct" or "h" ? BetSide.Humans
                          : sideArg is "zombies" or "zombie" or "t" or "z" ? BetSide.Zombies
                          : null;

            if (!side.HasValue)
            {
                SendChatT(caller, "InvalidSide");
                return;
            }

            PlaceBet(caller, side.Value, amount);
            return;
        }

        // No args → open the bet menu
        OpenBetMenu(caller);
    }

    // ── Bet menu ──────────────────────────────────────────────────────────────

    private void OpenBetMenu(IPlayer player)
    {
        if (!_betsOpen)
        {
            SendChatT(player, "BetsClosed");
            return;
        }

        if (_activeBets.TryGetValue(player.PlayerID, out var existing))
        {
            SendChatT(player, "AlreadyBet",
                existing.Side == BetSide.Humans ? T(player, "TeamHumans") : T(player, "TeamZombies"),
                existing.Amount);
            return;
        }

        var menuCfg = new MenuConfiguration
        {
            Title           = T(player, "MenuTitle"),
            FreezePlayer    = false,
            MaxVisibleItems = 6,
            PlaySound       = false,
            HideFooter      = false
        };

        var menu = Core.MenusAPI.CreateMenu(menuCfg, default, null, MenuOptionScrollStyle.LinearScroll);

        // Build a button for each side × quick-bet amount combination
        foreach (var side in new[] { BetSide.Humans, BetSide.Zombies })
        {
            string sideLabel = side == BetSide.Humans ? T(player, "TeamHumans") : T(player, "TeamZombies");
            foreach (int preset in _config.QuickBetAmounts)
            {
                int capturedAmount = preset;
                BetSide capturedSide = side;

                var btn = new ButtonMenuOption(T(player, "MenuBetEntry", sideLabel, capturedAmount))
                {
                    TextStyle = MenuOptionTextStyle.ScrollLeftLoop,
                    CloseAfterClick = true,
                };
                btn.Click += async (_, args) =>
                {
                    var p = args.Player;
                    Core.Scheduler.NextTick(() =>
                    {
                        if (!p.IsValid) return;
                        PlaceBet(p, capturedSide, capturedAmount);
                    });
                };
                menu.AddOption(btn);
            }
        }

        Core.MenusAPI.OpenMenuForPlayer(player, menu);
    }

    // ── Core bet logic ────────────────────────────────────────────────────────

    private void PlaceBet(IPlayer player, BetSide side, int amount)
    {
        if (!_betsOpen)
        {
            SendChatT(player, "BetsClosed");
            return;
        }

        if (_activeBets.ContainsKey(player.PlayerID))
        {
            SendChatT(player, "AlreadyBetShort");
            return;
        }

        if (amount < _config.MinBet)
        {
            SendChatT(player, "BetTooSmall", _config.MinBet);
            return;
        }

        if (_config.MaxBet > 0 && amount > _config.MaxBet)
        {
            SendChatT(player, "BetTooBig", _config.MaxBet);
            return;
        }

        int balance = GetBalance(player);
        if (balance < amount)
        {
            SendChatT(player, "NotEnoughAP", balance, amount);
            return;
        }

        if (!TrySpendBalance(player, amount))
        {
            SendChatT(player, "EconomyOffline");
            return;
        }

        _activeBets[player.PlayerID] = (side, amount);
        string sideLabel = side == BetSide.Humans ? T(player, "TeamHumans") : T(player, "TeamZombies");
        SendChatT(player, "BetPlaced", amount, sideLabel);
    }

    // ── Economy helpers ───────────────────────────────────────────────────────

    private int GetBalance(IPlayer player)
    {
        if (_zplApi == null) return 0;
        try { return _zplApi.ZPL_GetAmmoPacks(player.PlayerID); }
        catch { return 0; }
    }

    private bool TrySpendBalance(IPlayer player, int amount)
    {
        if (_zplApi == null) return false;
        try { return _zplApi.ZPL_SpendAmmoPacks(player.PlayerID, amount); }
        catch { return false; }
    }

    private bool TryAddBalance(IPlayer player, int amount)
    {
        if (_zplApi == null) return false;
        try
        {
            _zplApi.ZPL_AddAmmoPacks(player.PlayerID, amount);
            return true;
        }
        catch { return false; }
    }

    // ── Chat / translation helpers ────────────────────────────────────────────

    private void SendChatT(IPlayer player, string key, params object[] args)
        => player.SendMessage(MessageType.Chat, $" {_config.ChatPrefix} {T(player, key, args)}");

    private void BroadcastChatT(string key, params object[] args)
    {
        foreach (var p in Core.PlayerManager.GetAllPlayers())
        {
            if (p == null || !p.IsValid || p.IsFakeClient) continue;
            p.SendMessage(MessageType.Chat, $" {_config.ChatPrefix} {T(p, key, args)}");
        }
    }

    private string T(IPlayer player, string key, params object[] args)
    {
        var loc = Core.Translation.GetPlayerLocalizer(player);
        return args.Length == 0 ? loc[key] : loc[key, args];
    }
}
