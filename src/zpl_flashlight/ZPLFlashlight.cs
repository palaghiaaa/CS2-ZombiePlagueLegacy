using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using ZombiePlagueLegacyCS2;

namespace ZPLFlashlight;

[PluginMetadata(
    Id = "ZPLFlashlight",
    Version = "1.0.1",
    Name = "ZPL Flashlight Effects",
    Author = "H-AN / ZombiePlagueLegacy",
    Description = "Allows human players to use a flashlight; zombie players get a visual light effect. Adapted for ZombiePlagueLegacy."
)]
public sealed class ZPLFlashlightPlugin : BasePlugin
{
    private const string ConfigFileName = "ZPLFlashlight.jsonc";
    private const string ConfigSectionName = "ZPLFlashlightCFG";
    private const string PrimaryCommand = "flashlight";
    private const string BindFallbackCommand = "sw_forceflash";
    private const string AdminForceCommand = "flash";
    private const string DefaultAdminPermission = "admin.dex";
    private const string ZombiePlagueInterfaceName = "ZombiePlagueLegacy";

    private ServiceProvider? _serviceProvider;
    private ZPLFlashlight_Service? _service;
    private IZombiePlagueLegacyAPI? _zpApi;
    private ZPLFlashlight_Config _config = new();

    public ZPLFlashlightPlugin(ISwiftlyCore core) : base(core)
    {
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        if (!interfaceManager.HasSharedInterface(ZombiePlagueInterfaceName))
        {
            Core.Logger.LogWarning("[ZPLFlashlight] ZombiePlagueLegacy API not found – zombie faction detection will fall back to team number.");
            return;
        }

        AttachZombiePlagueApi(interfaceManager.GetSharedInterface<IZombiePlagueLegacyAPI>(ZombiePlagueInterfaceName));
    }

    public override void Load(bool hotReload)
    {
        if (!hotReload)
        {
            Core.Configuration.InitializeJsonWithModel<ZPLFlashlight_Config>(ConfigFileName, ConfigSectionName).Configure(builder =>
            {
                builder.AddJsonFile(ConfigFileName, false, false);
            });
        }

        var services = new ServiceCollection();
        services.AddSwiftly(Core);
        services
            .AddOptionsWithValidateOnStart<ZPLFlashlight_Config>()
            .BindConfiguration(ConfigSectionName);

        _serviceProvider = services.BuildServiceProvider();
        var configMonitor = _serviceProvider.GetRequiredService<IOptionsMonitor<ZPLFlashlight_Config>>();
        _config = configMonitor.CurrentValue.Clone();
        _service = new ZPLFlashlight_Service(Core, Core.LoggerFactory.CreateLogger<ZPLFlashlight_Service>());
        _service.SetZombiePlagueApi(_zpApi);
        _service.UpdateConfig(_config);

        Core.Event.OnClientKeyStateChanged += OnClientKeyStateChanged;
        Core.Event.OnClientDisconnected += OnClientDisconnected;
        Core.Event.OnMapLoad += OnMapLoad;
        Core.Event.OnMapUnload += OnMapUnload;
        Core.Event.OnTick += OnTick;

        Core.Command.RegisterCommand(PrimaryCommand, HandleSelfFlashlightToggleCommand, true);
        Core.Command.RegisterCommand(BindFallbackCommand, HandleSelfFlashlightToggleCommand, true);
        Core.Command.RegisterCommand(AdminForceCommand, HandleAdminFlashlightCommand, true);
    }

    public override void Unload()
    {
        Core.Event.OnClientKeyStateChanged -= OnClientKeyStateChanged;
        Core.Event.OnClientDisconnected -= OnClientDisconnected;
        Core.Event.OnMapLoad -= OnMapLoad;
        Core.Event.OnMapUnload -= OnMapUnload;
        Core.Event.OnTick -= OnTick;

        Core.Command.UnregisterCommand(PrimaryCommand);
        Core.Command.UnregisterCommand(BindFallbackCommand);
        Core.Command.UnregisterCommand(AdminForceCommand);

        DetachZombiePlagueApi();
        _service?.ClearAll(clearState: true);

        _serviceProvider?.Dispose();
        _serviceProvider = null;
        _service = null;
        _config = new ZPLFlashlight_Config();
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event)
    {
        _service?.HandlePlayerSpawn(@event.UserId);
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerDeath(EventPlayerDeath @event)
    {
        _service?.HandlePlayerDeath(@event.UserId);
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnRoundStart(EventRoundStart @event)
    {
        _service?.HandleRoundReset();
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnRoundEnd(EventRoundEnd @event)
    {
        _service?.HandleRoundReset();
        return HookResult.Continue;
    }

    private void OnClientKeyStateChanged(IOnClientKeyStateChangedEvent @event)
    {
        _service?.HandleKeyStateChanged(@event);
    }

    private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        _service?.HandleClientDisconnected(@event.PlayerId);
    }

    private void OnMapLoad(IOnMapLoadEvent @event)
    {
        _service?.HandleMapLoad();
    }

    private void OnMapUnload(IOnMapUnloadEvent @event)
    {
        _service?.HandleMapUnload();
    }

    private void OnTick()
    {
        _service?.OnTick();
    }

    private void OnZombiePlayerInfect(IPlayer attacker, IPlayer infectedPlayer, bool grenade, string zombieClassName)
    {
        _service?.HandlePlayerInfected(infectedPlayer, zombieClassName);
    }

    private void HandleSelfFlashlightToggleCommand(ICommandContext context)
    {
        if (_service is null)
        {
            context.Reply(T(context, "Flashlight.ErrorServiceUnavailable"));
            return;
        }

        if (!TryResolveSelfCommandSender(context, out var sender, out var errorMessage))
        {
            context.Reply(errorMessage);
            return;
        }

        if (!_service.TryToggleForPlayer(sender, out _, out var message))
        {
            context.Reply(T(context, message));
            return;
        }

        context.Reply(T(context, "Flashlight.ReplyPlayerState", GetPlayerDisplayName(sender), StripZmPrefix(T(context, message))));
    }

    private void HandleAdminFlashlightCommand(ICommandContext context)
    {
        if (_service is null)
        {
            context.Reply(T(context, "Flashlight.ErrorServiceUnavailable"));
            return;
        }

        if (!HasAdminCommandPermission(context, out var permissionError))
        {
            context.Reply(permissionError);
            return;
        }

        if (!TryResolveAdminTarget(context, out var targetPlayer, out var action, out var errorMessage))
        {
            context.Reply(errorMessage);
            return;
        }

        if (action == AdminFlashAction.Toggle)
        {
            if (!_service.TryToggleForPlayer(targetPlayer, out _, out var message))
            {
                context.Reply(T(context, message));
                return;
            }

            context.Reply(T(context, "Flashlight.ReplyPlayerState", GetPlayerDisplayName(targetPlayer), StripZmPrefix(T(context, message))));
            return;
        }

        var shouldEnable = action == AdminFlashAction.On;
        if (!_service.TrySetForPlayer(targetPlayer, shouldEnable, out _, out var setMessage))
        {
            context.Reply(T(context, setMessage));
            return;
        }

        context.Reply(T(context, "Flashlight.ReplyPlayerState", GetPlayerDisplayName(targetPlayer), StripZmPrefix(T(context, setMessage))));
    }

    private bool HasAdminCommandPermission(ICommandContext context, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!context.IsSentByPlayer)
        {
            return true;
        }

        if (context.Sender is not IPlayer player || !player.IsValid)
        {
            errorMessage = T(context, "Flashlight.ErrorPermissionSenderUnavailable");
            return false;
        }

        var steamId = player.SteamID;
        if (steamId == 0)
        {
            errorMessage = T(context, "Flashlight.ErrorPermissionInvalidSteamId");
            return false;
        }

        var permissions = GetConfiguredAdminPermissions();
        foreach (var permission in permissions)
        {
            if (Core.Permission.PlayerHasPermission(steamId, permission))
            {
                return true;
            }
        }

        errorMessage = T(context, "Flashlight.ErrorNoPermissionAny", string.Join(", ", permissions));
        return false;
    }

    private IReadOnlyList<string> GetConfiguredAdminPermissions()
    {
        var raw = _config.AdminCommandPermission;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [DefaultAdminPermission];
        }

        var permissions = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static permission => permission.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (permissions.Count == 0)
        {
            permissions.Add(DefaultAdminPermission);
        }

        return permissions;
    }

    private bool TryResolveSelfCommandSender(
        ICommandContext context,
        out IPlayer sender,
        out string errorMessage)
    {
        sender = null!;
        errorMessage = string.Empty;

        if (context.Args.Length != 0)
        {
            errorMessage = T(context, "Flashlight.ErrorUsageSelf", context.CommandName);
            return false;
        }

        if (context.Sender is IPlayer player && player.IsValid)
        {
            sender = player;
            return true;
        }

        errorMessage = T(context, "Flashlight.ErrorPlayerOnly");
        return false;
    }

    private bool TryResolveAdminTarget(
        ICommandContext context,
        out IPlayer targetPlayer,
        out AdminFlashAction action,
        out string errorMessage)
    {
        targetPlayer = null!;
        action = AdminFlashAction.On;
        errorMessage = string.Empty;

        if (context.Args.Length == 0)
        {
            errorMessage = T(context, "Flashlight.ErrorUsageAdmin", AdminForceCommand);
            return false;
        }

        var args = context.Args;
        var hasAction = args.Length >= 2 && TryParseAdminAction(args[^1], out action);
        var targetPartLength = hasAction ? args.Length - 1 : args.Length;
        var rawTarget = string.Join(' ', args.Take(targetPartLength)).Trim();

        if (string.IsNullOrWhiteSpace(rawTarget))
        {
            errorMessage = T(context, "Flashlight.ErrorUsageAdmin", AdminForceCommand);
            return false;
        }

        if (TryResolveTargetById(context, rawTarget, out targetPlayer, out errorMessage))
        {
            return true;
        }

        var matches = FindPlayersByFuzzyName(rawTarget);
        if (matches.Count == 0)
        {
            errorMessage = T(context, "Flashlight.ErrorNoPlayerMatched", rawTarget);
            return false;
        }

        if (matches.Count > 1)
        {
            var preview = string.Join(", ", matches.Take(5).Select(player => $"{GetPlayerDisplayName(player)}(#{player.PlayerID})"));
            errorMessage = T(context, "Flashlight.ErrorMultiplePlayersMatched", rawTarget, preview);
            return false;
        }

        targetPlayer = matches[0];
        errorMessage = string.Empty;
        return true;
    }

    private bool TryResolveTargetById(
        ICommandContext context,
        string rawTarget,
        out IPlayer targetPlayer,
        out string errorMessage)
    {
        targetPlayer = null!;
        errorMessage = string.Empty;

        if (!int.TryParse(rawTarget, out var playerId))
        {
            return false;
        }

        var player = Core.PlayerManager.GetPlayer(playerId);
        if (player is null || !player.IsValid)
        {
            errorMessage = T(context, "Flashlight.ErrorPlayerNotAvailable", playerId);
            return false;
        }

        targetPlayer = player;
        return true;
    }

    private List<IPlayer> FindPlayersByFuzzyName(string rawTarget)
    {
        var target = rawTarget.Trim();
        if (target.Length == 0)
        {
            return [];
        }

        var exactMatches = new List<IPlayer>();
        var containsMatches = new List<IPlayer>();

        foreach (var player in Core.PlayerManager.GetAllValidPlayers())
        {
            if (player is null || !player.IsValid)
            {
                continue;
            }

            var displayName = GetPlayerDisplayName(player);
            if (displayName.Equals(target, StringComparison.OrdinalIgnoreCase))
            {
                exactMatches.Add(player);
                continue;
            }

            if (displayName.Contains(target, StringComparison.OrdinalIgnoreCase))
            {
                containsMatches.Add(player);
            }
        }

        return exactMatches.Count > 0 ? exactMatches : containsMatches;
    }

    private static bool TryParseAdminAction(string input, out AdminFlashAction action)
    {
        switch (input.Trim().ToLowerInvariant())
        {
            case "on":
            case "open":
            case "enable":
            case "1":
                action = AdminFlashAction.On;
                return true;
            case "off":
            case "close":
            case "disable":
            case "0":
                action = AdminFlashAction.Off;
                return true;
            case "toggle":
            case "switch":
            case "t":
                action = AdminFlashAction.Toggle;
                return true;
            default:
                action = AdminFlashAction.On;
                return false;
        }
    }

    private void AttachZombiePlagueApi(IZombiePlagueLegacyAPI? zpApi)
    {
        if (ReferenceEquals(_zpApi, zpApi))
        {
            _service?.SetZombiePlagueApi(_zpApi);
            return;
        }

        DetachZombiePlagueApi();

        _zpApi = zpApi;
        _service?.SetZombiePlagueApi(_zpApi);

        if (_zpApi is null)
        {
            return;
        }

        _zpApi.ZPL_OnPlayerInfect += OnZombiePlayerInfect;
        _zpApi.ZPL_OnUserPreferenceChanged += OnZombieUserPreferenceChanged;
    }

    private void DetachZombiePlagueApi()
    {
        if (_zpApi is null)
        {
            return;
        }

        _zpApi.ZPL_OnPlayerInfect -= OnZombiePlayerInfect;
        _zpApi.ZPL_OnUserPreferenceChanged -= OnZombieUserPreferenceChanged;
        _zpApi = null;
        _service?.SetZombiePlagueApi(null);
    }

    private void OnZombieUserPreferenceChanged(ulong steamId, string key, bool enabled)
    {
        _service?.HandleUserPreferenceChanged(steamId, key, enabled);
    }

    private static string GetPlayerDisplayName(IPlayer player)
    {
        if (player.Controller is not null
            && player.Controller.IsValid
            && !string.IsNullOrWhiteSpace(player.Controller.PlayerName))
        {
            return player.Controller.PlayerName;
        }

        return $"#{player.PlayerID}";
    }

    private string T(ICommandContext context, string key, params object[] args)
    {
        if (context.Sender is IPlayer player && player.IsValid)
        {
            return T(player, key, args);
        }

        return args.Length > 0
            ? string.Format(System.Globalization.CultureInfo.InvariantCulture, key, args)
            : key;
    }

    private static string StripZmPrefix(string message)
    {
        const string prefix = "[red][ZM][default] ";
        return message.StartsWith(prefix, StringComparison.Ordinal)
            ? message[prefix.Length..]
            : message;
    }

    private string T(IPlayer? player, string key, params object[] args)
    {
        if (player is null || !player.IsValid)
        {
            return args.Length > 0
                ? string.Format(System.Globalization.CultureInfo.InvariantCulture, key, args)
                : key;
        }

        var localizer = Core.Translation.GetPlayerLocalizer(player);
        return localizer[key, args];
    }

    private enum AdminFlashAction
    {
        On = 1,
        Off = 2,
        Toggle = 3
    }
}
