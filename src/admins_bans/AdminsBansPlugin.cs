using Admins.Bans.Manager;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.ProtobufDefinitions;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;

namespace Admins.Bans;

[PluginMetadata(
    Id          = "AdminsBans",
    Version     = "1.0.1",
    Name        = "Admins Bans",
    Author      = "DeadPoolCS2",
    Description = "Server-ban enforcement: checks each connecting player against a " +
                  "SQLite ban database by SteamID64 and IP address, and kicks banned players.")]
public sealed class AdminsBansPlugin(ISwiftlyCore core) : BasePlugin(core)
{
    private const string ConfigFile    = "AdminsBansCFG.jsonc";
    private const string ConfigSection = "AdminsBansCFG";

    private ServiceProvider?              _sp;
    private ILogger<AdminsBansPlugin>?    _logger;
    private IOptionsMonitor<AdminsBansCFG>? _cfgMonitor;
    private ServerBans?                   _serverBans;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void Load(bool hotReload)
    {
        if (!hotReload)
            Core.Configuration
                .InitializeJsonWithModel<AdminsBansCFG>(ConfigFile, ConfigSection)
                .Configure(builder =>
                {
                    builder.AddJsonFile(ConfigFile, optional: false, reloadOnChange: false);
                    builder.SetFileLoadExceptionHandler(ctx =>
                    {
                        Core.Logger.LogError(
                            "[AdminsBans] Failed to load {File}: {Error}. Using defaults.",
                            ConfigFile, ctx.Exception.Message);
                        ctx.Ignore = true;
                    });
                });

        var services = new ServiceCollection();
        services.AddSwiftly(Core);
        services.AddOptions<AdminsBansCFG>().BindConfiguration(ConfigSection);
        services.AddSingleton<ServerBans>();
        _sp = services.BuildServiceProvider();

        _logger      = _sp.GetRequiredService<ILogger<AdminsBansPlugin>>();
        _cfgMonitor  = _sp.GetRequiredService<IOptionsMonitor<AdminsBansCFG>>();
        _serverBans  = _sp.GetRequiredService<ServerBans>();

        _serverBans.EnsureSchema(_cfgMonitor.CurrentValue.DatabasePath);

        Core.Event.OnClientConnected += OnClientConnected;

        _logger.LogInformation("[AdminsBans] Plugin loaded (hotReload={HotReload}).", hotReload);
    }

    public override void Unload()
    {
        Core.Event.OnClientConnected -= OnClientConnected;

        _serverBans  = null;
        _cfgMonitor  = null;
        _logger      = null;
        _sp?.Dispose();
        _sp = null;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by SwiftlyS2 when a player finishes connecting.
    /// Launches an async ban-check and, if a ban is found, schedules a kick
    /// on the game thread via <c>Core.Scheduler.NextWorldUpdate</c>.
    ///
    /// <para><b>Unobserved-task fix:</b>
    /// The original code used a raw fire-and-forget pattern
    /// (<c>_ = FindActiveBanAsync(…)</c>) which caused the server to abort
    /// with "Unobserved task exception" whenever the async method threw
    /// (e.g. the NullReferenceException that occurred when
    /// <c>_connectionString</c> was null).
    /// The task is now observed via <c>ContinueWith</c>: any fault is logged,
    /// and a non-null result triggers a scheduled kick.</para>
    /// </summary>
    private void OnClientConnected(IOnClientConnectedEvent @event)
    {
        var bans     = _serverBans;
        var logger   = _logger;
        if (bans is null || logger is null) return;

        var playerId = @event.PlayerId;
        var player   = Core.PlayerManager.GetPlayer(playerId);
        if (!IsValidRealPlayer(player)) return;

        var steamId = (long)player!.SteamID;
        var ip      = player.IPAddress ?? string.Empty;

        // FIX: Use ContinueWith to observe the task exception.
        // Previously: _ = bans.FindActiveBanAsync(steamId, ip);
        // That fire-and-forget pattern left exceptions unobserved, causing
        // "Unobserved task exception. Aborting." and a server crash.
        _ = bans.FindActiveBanAsync(steamId, ip)
            .ContinueWith(
                t =>
                {
                    if (t.IsFaulted)
                    {
                        logger.LogError(
                            t.Exception,
                            "[AdminsBans] Unhandled exception in ban check for player {SteamId}.",
                            steamId);
                        return;
                    }

                    var ban = t.Result;
                    if (ban is null) return;

                    // Schedule the kick on the game thread.
                    Core.Scheduler.NextWorldUpdate(() =>
                    {
                        var p = Core.PlayerManager.GetPlayer(playerId);
                        if (p is null || !p.IsValid) return;

                        var reason = string.IsNullOrWhiteSpace(ban.Reason)
                            ? "You are banned from this server."
                            : ban.Reason;

                        p.Kick(reason, ENetworkDisconnectionReason.NETWORK_DISCONNECT_KICKBANADDED);

                        logger.LogInformation(
                            "[AdminsBans] Kicked banned player {Name} (SteamID={SteamId}, BanId={BanId}): {Reason}",
                            p.Name, steamId, ban.Id, reason);
                    });
                },
                TaskScheduler.Default);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsValidRealPlayer(IPlayer? player)
        => player != null && player.IsValid && !player.IsFakeClient && player.SteamID != 0;
}

/// <summary>
/// Configuration model bound from <c>AdminsBansCFG.jsonc</c>.
/// </summary>
public sealed class AdminsBansCFG
{
    /// <summary>Path to the SQLite database file, relative to the game root.</summary>
    public string DatabasePath { get; set; } = "addons/swiftly/data/AdminsBans.db";
}
