using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

namespace ZPLDarkFog;

[PluginMetadata(
    Id = "ZPLDarkFog",
    Version = "1.0.0",
    Name = "ZPL Dark Fog (Exposure)",
    Author = "H-AN / ZombiePlagueLegacy",
    Description = "Per-player post-processing exposure control for ZombiePlagueLegacy. Darkens/brightens each player's screen based on their role."
)]
public sealed class ZPLDarkFogPlugin : BasePlugin
{
    private const string ConfigFileName = "ZPLDarkFog.jsonc";
    private const string ConfigSectionName = "ZPLDarkFogCFG";
    private const string ZombiePlagueInterfaceName = "ZombiePlagueLegacy";
    private const string DefaultAdminCommandName = "fog_exposure";

    private readonly ILogger<ZPLDarkFogPlugin> _logger;

    private ServiceProvider? _serviceProvider;
    private IOptionsMonitor<ZPLDarkFog_Config>? _config;
    private IDisposable? _configChangeSubscription;
    private ZPLDarkFog_Service? _service;
    private IZombiePlagueLegacyAPI? _zpApi;

    private string? _registeredAdminCommandName;
    private string? _registeredHiddenCommandName;

    private readonly Dictionary<int, float> _manualExposureOverrideByPlayerId = [];
    private readonly Dictionary<string, float> _zombieGroupExposureByClassName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> _resolvedZombieExposureByClassCache = new(StringComparer.OrdinalIgnoreCase);

    public ZPLDarkFogPlugin(ISwiftlyCore core) : base(core)
    {
        _logger = NullLogger<ZPLDarkFogPlugin>.Instance;
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        if (!interfaceManager.HasSharedInterface(ZombiePlagueInterfaceName))
        {
            Core.Logger.LogWarning("[ZPLDarkFog] ZombiePlagueLegacy API not found – exposure will not react to zombie/human role changes.");
            return;
        }

        try
        {
            AttachZombiePlagueApi(interfaceManager.GetSharedInterface<IZombiePlagueLegacyAPI>(ZombiePlagueInterfaceName));
        }
        catch (Exception ex)
        {
            Core.Logger.LogWarning("[ZPLDarkFog] Failed to acquire ZombiePlagueLegacy API: {Error}", ex.Message);
        }
    }

    public override void Load(bool hotReload)
    {
        _logger.LogInformation("ZPLDarkFog loading. hotReload={HotReload}", hotReload);

        if (!hotReload)
        {
            Core.Configuration.InitializeJsonWithModel<ZPLDarkFog_Config>(ConfigFileName, ConfigSectionName).Configure(builder =>
            {
                builder.AddJsonFile(ConfigFileName, false, true);
            });
        }

        var collection = new ServiceCollection();
        collection.AddSwiftly(Core);
        collection
            .AddOptionsWithValidateOnStart<ZPLDarkFog_Config>()
            .BindConfiguration(ConfigSectionName);

        _serviceProvider = collection.BuildServiceProvider();
        _config = _serviceProvider.GetRequiredService<IOptionsMonitor<ZPLDarkFog_Config>>();
        _service = new ZPLDarkFog_Service(Core, NullLogger<ZPLDarkFog_Service>.Instance);
        _configChangeSubscription = _config.OnChange(OnConfigChanged);

        var currentConfig = _config.CurrentValue;
        RebuildZombieGroupExposureCache(currentConfig);
        RegisterConfiguredCommands(currentConfig);

        Core.Event.OnClientDisconnected += OnClientDisconnected;
        Core.Event.OnMapLoad += OnMapLoad;
        Core.Event.OnMapUnload += OnMapUnload;

        ApplyVisionForAllPlayersAfterDelay(hotReload ? 0.2f : 0.5f, hotReload ? "hot-reload" : "load");
    }

    public override void Unload()
    {
        _logger.LogInformation("ZPLDarkFog unloading.");

        Core.Event.OnClientDisconnected -= OnClientDisconnected;
        Core.Event.OnMapLoad -= OnMapLoad;
        Core.Event.OnMapUnload -= OnMapUnload;

        UnregisterConfiguredCommands();
        DetachZombiePlagueApi();

        _configChangeSubscription?.Dispose();
        _service?.ClearAll();
        _serviceProvider?.Dispose();

        _manualExposureOverrideByPlayerId.Clear();
        _zombieGroupExposureByClassName.Clear();
        _resolvedZombieExposureByClassCache.Clear();
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event)
    {
        ApplyVisionForPlayerAfterDelay(@event.UserId, 0.15f, "spawn");
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerTeam(EventPlayerTeam @event)
    {
        ApplyVisionForPlayerAfterDelay(@event.UserId, 0.15f, "team-change");
        return HookResult.Continue;
    }

    public void HandleFogExposureCommand(ICommandContext context)
    {
        if (_service is null)
        {
            context.Reply(T(context, "DarkFog.ErrorServiceUnavailable"));
            return;
        }

        if (context.Args.Length < 2)
        {
            context.Reply(T(context, "DarkFog.Admin.Usage", context.CommandName));
            return;
        }

        var targetInput = string.Join(" ", context.Args[..^1]).Trim();
        var exposureInput = context.Args[^1].Trim();

        if (!TryResolveFogTarget(context, targetInput, out var target, out var errorMessage))
        {
            context.Reply(errorMessage);
            return;
        }

        var targetName = GetPlayerDisplayName(target);

        if (IsResetKeyword(exposureInput))
        {
            RemoveManualExposureOverride(target.PlayerID);
            var resetApplied = ApplyVisionForCurrentRole(target);
            if (!resetApplied)
            {
                context.Reply(T(context, "DarkFog.Admin.ApplyFailed", targetName, target.PlayerID));
                return;
            }

            context.Reply(T(context, "DarkFog.Admin.ResetSuccess", targetName, target.PlayerID));
            return;
        }

        if (!TryParseExposure(exposureInput, out var parsedExposure))
        {
            context.Reply(T(context, "DarkFog.Admin.InvalidExposure", exposureInput, context.CommandName));
            return;
        }

        var appliedExposure = MathF.Max(0.0f, parsedExposure);
        SetManualExposureOverride(target.PlayerID, appliedExposure);

        var applied = ApplyVisionForCurrentRole(target);
        if (!applied)
        {
            context.Reply(T(context, "DarkFog.Admin.ApplyFailed", targetName, target.PlayerID));
            return;
        }

        context.Reply(
            T(
                context,
                "DarkFog.Admin.SetSuccess",
                targetName,
                target.PlayerID,
                appliedExposure.ToString("0.###", CultureInfo.InvariantCulture)));
    }

    public void HandleHiddenExposureCommand(ICommandContext context)
    {
        if (_service is null)
        {
            return;
        }

        if (context.Sender is not IPlayer sender || !sender.IsValid)
        {
            return;
        }

        if (context.Args.Length != 1)
        {
            return;
        }

        var exposureInput = context.Args[0].Trim();

        if (IsResetKeyword(exposureInput))
        {
            _ = ApplyVisionForCurrentRole(sender);
            return;
        }

        if (!TryParseExposure(exposureInput, out var parsedExposure))
        {
            return;
        }

        var appliedExposure = MathF.Max(0.0f, parsedExposure);
        _ = _service.ApplyExposure(sender, appliedExposure);
    }

    private void OnConfigChanged(ZPLDarkFog_Config config)
    {
        _logger.LogInformation(
            "ZPLDarkFog config changed. Enable={Enable} HumanExposure={HumanExposure} ZombieExposure={ZombieExposure} ZombieGroupCount={ZombieGroupCount}",
            config.Enable,
            config.HumanExposure,
            config.ZombieExposure,
            config.ZombieGroups?.Count ?? 0);

        Core.Scheduler.NextWorldUpdate(() =>
        {
            var latestConfig = _config?.CurrentValue ?? config;
            RebuildZombieGroupExposureCache(latestConfig);
            RegisterConfiguredCommands(latestConfig);
            ApplyVisionForAllPlayersAfterDelay(0.1f, "config-change");
        });
    }

    private void ApplyVisionForPlayerAfterDelay(int playerId, float delaySeconds, string reason)
    {
        Core.Scheduler.DelayBySeconds(delaySeconds, () =>
        {
            var player = Core.PlayerManager.GetPlayer(playerId);
            if (player is null || !player.IsValid || player.IsFakeClient)
            {
                return;
            }

            var applied = ApplyVisionForCurrentRole(player);
            _logger.LogDebug(
                "Delayed dark fog apply finished for player {PlayerId}. Reason={Reason} Applied={Applied}",
                playerId,
                reason,
                applied);
        });
    }

    private void ApplyVisionForAllPlayersAfterDelay(float delaySeconds, string reason)
    {
        _logger.LogDebug(
            "Scheduling dark fog apply for all players after {DelaySeconds} seconds. Reason={Reason}",
            delaySeconds,
            reason);

        Core.Scheduler.DelayBySeconds(delaySeconds, ApplyVisionForAllPlayers);
    }

    private void ApplyVisionForAllPlayers()
    {
        if (_service is null)
        {
            return;
        }

        var players = Core.PlayerManager.GetAllValidPlayers()
            .Where(static player => !player.IsFakeClient)
            .ToArray();

        foreach (var player in players)
        {
            ApplyVisionForCurrentRole(player);
        }
    }

    private bool ApplyVisionForCurrentRole(IPlayer? player)
    {
        if (_service is null || _config is null)
        {
            return false;
        }

        if (player is null || !player.IsValid || player.IsFakeClient)
        {
            return false;
        }

        var controller = player.Controller;
        if (controller is null || !controller.IsValid)
        {
            return false;
        }

        var config = _config.CurrentValue;
        if (!config.Enable)
        {
            _service.ResetPlayer(player);
            return true;
        }

        if (_zpApi != null && !_zpApi.ZPL_GetUserPreference(player, ZPLUserPreferenceKeys.Fog, true))
        {
            _service.ResetPlayer(player);
            return true;
        }

        if (_manualExposureOverrideByPlayerId.TryGetValue(player.PlayerID, out var manualExposure))
        {
            return _service.ApplyExposure(player, MathF.Max(0.0f, manualExposure));
        }

        if (_zpApi is null)
        {
            _service.ResetPlayer(player);
            return true;
        }

        bool isZombie;
        try
        {
            isZombie = _zpApi.ZPL_IsZombie(player.PlayerID);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query zombie state for player {PlayerId}.", player.PlayerID);
            _service.ResetPlayer(player);
            return true;
        }

        if (!isZombie)
        {
            return _service.ApplyExposure(player, MathF.Max(0.0f, config.HumanExposure));
        }

        var zombieClassName = ResolveZombieClassName(player);
        var exposure = ResolveZombieExposure(zombieClassName, MathF.Max(0.0f, config.ZombieExposure));
        return _service.ApplyExposure(player, exposure);
    }

    private string ResolveZombieClassName(IPlayer player)
    {
        if (_zpApi is null)
        {
            return string.Empty;
        }

        try
        {
            var rawClassName = _zpApi.ZPL_GetZombieClassname(player);
            var className = rawClassName?.Trim() ?? string.Empty;
            return className;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query zombie class name for player {PlayerId}.", player.PlayerID);
            return string.Empty;
        }
    }

    private float ResolveZombieExposure(string zombieClassName, float fallbackExposure)
    {
        if (string.IsNullOrWhiteSpace(zombieClassName))
        {
            return fallbackExposure;
        }

        if (_resolvedZombieExposureByClassCache.TryGetValue(zombieClassName, out var cachedExposure))
        {
            return cachedExposure;
        }

        var exposure = _zombieGroupExposureByClassName.TryGetValue(zombieClassName, out var groupedExposure)
            ? groupedExposure
            : fallbackExposure;

        _resolvedZombieExposureByClassCache[zombieClassName] = exposure;
        return exposure;
    }

    private void RebuildZombieGroupExposureCache(ZPLDarkFog_Config config)
    {
        _zombieGroupExposureByClassName.Clear();
        _resolvedZombieExposureByClassCache.Clear();

        foreach (var group in config.ZombieGroups ?? [])
        {
            if (group is null || !group.Enable)
            {
                continue;
            }

            var className = group.ZombieClassName?.Trim();
            if (string.IsNullOrWhiteSpace(className))
            {
                continue;
            }

            _zombieGroupExposureByClassName[className] = MathF.Max(0.0f, group.Exposure);
        }

        _logger.LogInformation(
            "Rebuilt zombie exposure group cache. GroupCount={GroupCount}",
            _zombieGroupExposureByClassName.Count);
    }

    private void RegisterConfiguredCommands(ZPLDarkFog_Config config)
    {
        UnregisterConfiguredCommands();

        var adminCommandName = NormalizeCommandName(config.AdminCommandName, DefaultAdminCommandName);
        var adminPermission = NormalizePermission(config.AdminCommandPermission);

        if (string.IsNullOrWhiteSpace(adminPermission))
        {
            Core.Command.RegisterCommand(adminCommandName, HandleFogExposureCommand, true);
            _logger.LogWarning(
                "Admin command '{CommandName}' is registered without permission restriction. Consider setting AdminCommandPermission.",
                adminCommandName);
        }
        else
        {
            Core.Command.RegisterCommand(adminCommandName, HandleFogExposureCommand, true, adminPermission);
        }

        _registeredAdminCommandName = adminCommandName;

        var hiddenCommandName = string.Empty;
        if (config.HiddenExposureCommandEnabled)
        {
            hiddenCommandName = NormalizeCommandName(config.HiddenExposureCommandName, string.Empty);
            if (string.IsNullOrWhiteSpace(hiddenCommandName))
            {
                _logger.LogWarning("Hidden exposure command is enabled, but HiddenExposureCommandName is empty.");
            }
            else if (string.Equals(hiddenCommandName, adminCommandName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Hidden command '{HiddenCommandName}' conflicts with admin command '{AdminCommandName}'. Hidden command registration skipped.",
                    hiddenCommandName,
                    adminCommandName);
            }
            else
            {
                Core.Command.RegisterCommand(hiddenCommandName, HandleHiddenExposureCommand, true);
                _registeredHiddenCommandName = hiddenCommandName;
            }
        }
    }

    private void UnregisterConfiguredCommands()
    {
        UnregisterCommandIfPresent(ref _registeredAdminCommandName);
        UnregisterCommandIfPresent(ref _registeredHiddenCommandName);
    }

    private void UnregisterCommandIfPresent(ref string? commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            return;
        }

        try
        {
            Core.Command.UnregisterCommand(commandName);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to unregister command '{CommandName}'.", commandName);
        }

        commandName = null;
    }

    private static string NormalizeCommandName(string? rawCommandName, string fallback)
    {
        var commandName = string.IsNullOrWhiteSpace(rawCommandName)
            ? fallback
            : rawCommandName.Trim();

        return commandName.TrimStart('!', '/').Trim();
    }

    private static string NormalizePermission(string? rawPermission)
    {
        return string.IsNullOrWhiteSpace(rawPermission)
            ? string.Empty
            : rawPermission.Trim();
    }

    private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        _service?.RemovePlayer(@event.PlayerId);
        RemoveManualExposureOverride(@event.PlayerId);
    }

    private void OnMapLoad(IOnMapLoadEvent @event)
    {
        ApplyVisionForAllPlayersAfterDelay(1.0f, "map-load");
    }

    private void OnMapUnload(IOnMapUnloadEvent @event)
    {
        _service?.ClearAllVolumes();
        _manualExposureOverrideByPlayerId.Clear();
    }

    private bool TryResolveFogTarget(ICommandContext context, string input, out IPlayer target, out string errorMessage)
    {
        target = null!;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            errorMessage = T(context, "DarkFog.Admin.Usage", context.CommandName);
            return false;
        }

        if (string.Equals(input, "@me", StringComparison.OrdinalIgnoreCase))
        {
            if (context.Sender is IPlayer sender && sender.IsValid)
            {
                target = sender;
                return true;
            }

            errorMessage = T(context, "DarkFog.Target.MePlayerOnly");
            return false;
        }

        var players = Core.PlayerManager
            .GetAllPlayers()
            .Where(static player => player is not null && player.IsValid && !player.IsFakeClient)
            .ToList();

        if (players.Count == 0)
        {
            errorMessage = T(context, "DarkFog.Target.NoPlayersAvailable");
            return false;
        }

        if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var playerId))
        {
            var byPlayerId = players.FirstOrDefault(player => player.PlayerID == playerId);
            if (byPlayerId is not null)
            {
                target = byPlayerId;
                return true;
            }
        }

        var exactMatches = players
            .Where(player => string.Equals(GetPlayerDisplayName(player), input, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (exactMatches.Count == 1)
        {
            target = exactMatches[0];
            return true;
        }

        if (exactMatches.Count > 1)
        {
            errorMessage = T(context, "DarkFog.Target.MultipleMatches", input, FormatPlayerMatchList(exactMatches));
            return false;
        }

        var partialMatches = players
            .Where(player => GetPlayerDisplayName(player).Contains(input, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (partialMatches.Count == 1)
        {
            target = partialMatches[0];
            return true;
        }

        if (partialMatches.Count > 1)
        {
            errorMessage = T(context, "DarkFog.Target.MultipleMatches", input, FormatPlayerMatchList(partialMatches));
            return false;
        }

        errorMessage = T(context, "DarkFog.Target.NotFound", input);
        return false;
    }

    private static bool TryParseExposure(string input, out float exposure)
    {
        if (float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out exposure))
        {
            return true;
        }

        return float.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out exposure);
    }

    private static string FormatPlayerMatchList(IEnumerable<IPlayer> players)
    {
        return string.Join(
            ", ",
            players
                .Take(5)
                .Select(player => $"{GetPlayerDisplayName(player)}(ID {player.PlayerID})"));
    }

    private static string GetPlayerDisplayName(IPlayer player)
    {
        var controller = player.Controller;
        if (controller is not null
            && controller.IsValid
            && !string.IsNullOrWhiteSpace(controller.PlayerName))
        {
            return controller.PlayerName;
        }

        return $"#{player.PlayerID}";
    }

    private void SetManualExposureOverride(int playerId, float exposure)
    {
        _manualExposureOverrideByPlayerId[playerId] = MathF.Max(0.0f, exposure);
    }

    private void RemoveManualExposureOverride(int playerId)
    {
        _manualExposureOverrideByPlayerId.Remove(playerId);
    }

    private static bool IsResetKeyword(string input)
    {
        return input.Equals("reset", StringComparison.OrdinalIgnoreCase)
            || input.Equals("clear", StringComparison.OrdinalIgnoreCase)
            || input.Equals("off", StringComparison.OrdinalIgnoreCase);
    }

    private void AttachZombiePlagueApi(IZombiePlagueLegacyAPI? zpApi)
    {
        if (ReferenceEquals(_zpApi, zpApi))
        {
            return;
        }

        DetachZombiePlagueApi();
        _zpApi = zpApi;

        if (_zpApi is null)
        {
            _logger.LogWarning("ZombiePlagueLegacy API is unavailable.");
            return;
        }

        _zpApi.ZPL_OnGameStart += OnZombieGameStart;
        _zpApi.ZPL_OnPlayerInfect += OnZombiePlayerInfect;
        _zpApi.ZPL_OnMotherZombieSelected += OnZombieRoleSelected;
        _zpApi.ZPL_OnNemesisSelected += OnZombieRoleSelected;
        _zpApi.ZPL_OnAssassinSelected += OnZombieRoleSelected;
        _zpApi.ZPL_OnHeroSelected += OnZombieRoleSelected;
        _zpApi.ZPL_OnSurvivorSelected += OnZombieRoleSelected;
        _zpApi.ZPL_OnSniperSelected += OnZombieRoleSelected;
        _zpApi.ZPL_OnUserPreferenceChanged += OnZombieUserPreferenceChanged;

        _logger.LogInformation("Attached ZombiePlagueLegacy API.");
    }

    private void DetachZombiePlagueApi()
    {
        if (_zpApi is null)
        {
            return;
        }

        _zpApi.ZPL_OnGameStart -= OnZombieGameStart;
        _zpApi.ZPL_OnPlayerInfect -= OnZombiePlayerInfect;
        _zpApi.ZPL_OnMotherZombieSelected -= OnZombieRoleSelected;
        _zpApi.ZPL_OnNemesisSelected -= OnZombieRoleSelected;
        _zpApi.ZPL_OnAssassinSelected -= OnZombieRoleSelected;
        _zpApi.ZPL_OnHeroSelected -= OnZombieRoleSelected;
        _zpApi.ZPL_OnSurvivorSelected -= OnZombieRoleSelected;
        _zpApi.ZPL_OnSniperSelected -= OnZombieRoleSelected;
        _zpApi.ZPL_OnUserPreferenceChanged -= OnZombieUserPreferenceChanged;

        _zpApi = null;
    }

    private void OnZombieUserPreferenceChanged(ulong steamId, string key, bool enabled)
    {
        if (!string.Equals(key, ZPLUserPreferenceKeys.Fog, StringComparison.Ordinal))
            return;

        Core.Scheduler.NextWorldUpdate(() =>
        {
            var player = Core.PlayerManager.GetAllValidPlayers()
                .FirstOrDefault(p => !p.IsFakeClient && p.SteamID == steamId);
            if (player == null)
                return;

            if (!enabled)
                _service?.ResetPlayer(player);
            else
                ApplyVisionForCurrentRole(player);
        });
    }

    private void OnZombieGameStart(bool gameStart)
    {
        ApplyVisionForAllPlayersAfterDelay(0.2f, gameStart ? "zpl-game-start" : "zpl-game-end");
    }

    private void OnZombiePlayerInfect(IPlayer attacker, IPlayer infectedPlayer, bool grenade, string zombieClassName)
    {
        if (infectedPlayer is null || !infectedPlayer.IsValid)
        {
            return;
        }

        ApplyVisionForPlayerAfterDelay(infectedPlayer.PlayerID, 0.1f, "zpl-infect");
    }

    private void OnZombieRoleSelected(IPlayer player)
    {
        if (player is null || !player.IsValid)
        {
            return;
        }

        ApplyVisionForPlayerAfterDelay(player.PlayerID, 0.1f, "zpl-role-selected");
    }

    private string T(ICommandContext context, string key, params object[] args)
    {
        if (context.Sender is IPlayer player && player.IsValid)
        {
            return Core.Translation.GetPlayerLocalizer(player)[key, args];
        }

        var template = Core.Localizer[key];
        if (string.IsNullOrWhiteSpace(template)
            || string.Equals(template, key, StringComparison.Ordinal))
        {
            template = key;
        }

        if (args.Length == 0)
        {
            return template;
        }

        try
        {
            return string.Format(CultureInfo.InvariantCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }
}
