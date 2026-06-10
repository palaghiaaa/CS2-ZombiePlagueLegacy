using Economy.Contract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.Services;

namespace ZombiePlagueLegacyCS2;

[PluginMetadata(
    Id = "ZombiePlagueLegacyCS2",
    Version = "1.0",
    Name = "CS2 僵尸瘟疫 for Sw2/CS2 ZombiePlague for Sw2",
    Author = "H-AN",
    Description = "CS2 僵尸瘟疫 SW2版本 CS2 ZombiePlague for SW2.")]

public partial class ZombiePlagueLegacyCS2(ISwiftlyCore core) : BasePlugin(core)
{

    private ServiceProvider? ServiceProvider { get; set; }
    private static readonly ZombiePlagueLegacyAPI _apiInstance = new();
    private ZPLMainCFG _ZPLMainCFG = null!;
    private ZPLGlobals _Globals = null!;
    private ZPLEvents _Events = null!;
    private ZPLCommands _Commands = null!;
    private ZPLPlayerPrefsService? _prefsService;

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
        interfaceManager.AddSharedInterface<IZombiePlagueLegacyAPI, ZombiePlagueLegacyAPI>("ZombiePlagueLegacy", _apiInstance);
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        if (ServiceProvider == null)
        {
            Core.Logger.LogError("[ZM] UseSharedInterface called but ServiceProvider is null! Load method may not have completed.");
            return;
        }

        var ammoPacks = ServiceProvider.GetRequiredService<AmmoPacksService>();

        // ── Economy plugin ────────────────────────────────────────────────────
        try
        {
            Core.Logger.LogInformation("[ZM] UseSharedInterface: Checking for Economy.API.v1...");
            
            if (interfaceManager.HasSharedInterface("Economy.API.v1"))
            {
                Core.Logger.LogInformation("[ZM] UseSharedInterface: Economy.API.v1 found, attempting to get interface...");
                
                var economyApi = interfaceManager.GetSharedInterface<IEconomyAPIv1>("Economy.API.v1");
                if (economyApi != null)
                {
                    Core.Logger.LogInformation("[ZM] UseSharedInterface: Economy API retrieved successfully. Setting up AmmoPacksService...");
                    
                    ammoPacks.SetApi(economyApi);
                    ammoPacks.EnsureWalletKind();

                    // If the Economy API becomes available while players are already connected,
                    // make sure their economy data is loaded and cached.
                    int playerCount = 0;
                    foreach (var player in Core.PlayerManager.GetAllPlayers())
                    {
                        if (player != null && player.IsValid && !player.IsFakeClient)
                        {
                            playerCount++;
                            Core.Logger.LogDebug("[ZM] UseSharedInterface: Loading economy data for player {PlayerId}", player.PlayerID);
                            ammoPacks.LoadData(player);
                            ammoPacks.RefreshBalance(player);
                        }
                    }
                    
                    Core.Logger.LogInformation("[ZM] UseSharedInterface: Economy setup complete. Reloaded {PlayerCount} connected players.", playerCount);
                }
                else
                {
                    Core.Logger.LogError("[ZM] UseSharedInterface: Economy.API.v1 interface returned null!");
                }
            }
            else
            {
                Core.Logger.LogWarning("[ZM] Economy plugin not found – ammo packs will not function until the Economy plugin is loaded.");
            }
        }
        catch (Exception ex)
        {
            Core.Logger.LogError("[ZM] Economy API lookup failed: {Ex}\n{StackTrace}", ex.Message, ex.StackTrace);
        }
    }

    public override void Load(bool hotReload)
    {
        Core.Logger.LogInformation("[ZM] Load() called. hotReload={HotReload}. Initializing configurations...", hotReload);
        
        // Guard Configure() calls with !hotReload.
        // SwiftlyS2's PluginConfigurationService.Manager is a lazy singleton that is
        // never reset between hot-reloads (map changes).  Calling AddJsonFile on it
        // again on every Load() appends a brand-new FileSystemWatcher thread to the
        // same ConfigurationManager, causing one set of watcher threads to leak per
        // map change.  9 configs × N map changes = the 31 file-watcher threads seen
        // in the managedtrace.txt crash.  Skipping Configure() on hot-reload keeps
        // the already-registered watchers without adding duplicates.
        if (!hotReload)
        {
            Core.Configuration.InitializeJsonWithModel<ZPLMainCFG>("ZombiePlagueLegacyCFG.jsonc", "ZPLMainCFG").Configure(builder =>
            {
                builder.AddJsonFile("ZombiePlagueLegacyCFG.jsonc", false, true);
                builder.SetFileLoadExceptionHandler(ctx =>
                {
                    Core.Logger.LogError("[ZM] Failed to load ZombiePlagueLegacyCFG.jsonc (ZPLMainCFG): {Error}. Using last valid configuration.", ctx.Exception.Message);
                    ctx.Ignore = true;
                });
            });
            Core.Configuration.InitializeJsonWithModel<ZPLVoxCFG>("ZombiePlagueLegacyCFG.jsonc", "ZPLVoxCFG").Configure(builder =>
            {
                builder.AddJsonFile("ZombiePlagueLegacyCFG.jsonc", false, true);
                builder.SetFileLoadExceptionHandler(ctx =>
                {
                    Core.Logger.LogError("[ZM] Failed to load ZombiePlagueLegacyCFG.jsonc (ZPLVoxCFG): {Error}. Using last valid configuration.", ctx.Exception.Message);
                    ctx.Ignore = true;
                });
            });
            Core.Configuration.InitializeJsonWithModel<ZPLZombieClassCFG>("ZombieClassesCFG.jsonc", "ZPLZombieClassCFG").Configure(builder =>
            {
                builder.AddJsonFile("ZombieClassesCFG.jsonc", false, true);
                builder.SetFileLoadExceptionHandler(ctx =>
                {
                    Core.Logger.LogError("[ZM] Failed to load ZombieClassesCFG.jsonc (ZPLZombieClassCFG): {Error}. Using last valid configuration.", ctx.Exception.Message);
                    ctx.Ignore = true;
                });
            });
            Core.Configuration.InitializeJsonWithModel<ZPLSpecialClassCFG>("ZombiePlagueLegacyCFG.jsonc", "ZPLSpecialClassCFG").Configure(builder =>
            {
                builder.AddJsonFile("ZombiePlagueLegacyCFG.jsonc", false, true);
                builder.SetFileLoadExceptionHandler(ctx =>
                {
                    Core.Logger.LogError("[ZM] Failed to load ZombiePlagueLegacyCFG.jsonc (ZPLSpecialClassCFG): {Error}. Using last valid configuration.", ctx.Exception.Message);
                    ctx.Ignore = true;
                });
            });
            Core.Configuration.InitializeJsonWithModel<ZPLWeaponsCFG>("ZombiePlagueLegacyCFG.jsonc", "ZPLWeaponsCFG").Configure(builder =>
            {
                builder.AddJsonFile("ZombiePlagueLegacyCFG.jsonc", false, true);
                builder.SetFileLoadExceptionHandler(ctx =>
                {
                    Core.Logger.LogError("[ZM] Failed to load ZombiePlagueLegacyCFG.jsonc (ZPLWeaponsCFG): {Error}. Using last valid configuration.", ctx.Exception.Message);
                    ctx.Ignore = true;
                });
            });
            Core.Configuration.InitializeJsonWithModel<ZPLExtraItemsCFG>("ExtraItemsCFG.jsonc", "ZPLExtraItemsCFG").Configure(builder =>
            {
                builder.AddJsonFile("ExtraItemsCFG.jsonc", false, true);
                builder.SetFileLoadExceptionHandler(ctx =>
                {
                    Core.Logger.LogError("[ZM] Failed to load ExtraItemsCFG.jsonc (ZPLExtraItemsCFG): {Error}. Using last valid configuration.", ctx.Exception.Message);
                    ctx.Ignore = true;
                });
            });
            Core.Configuration.InitializeJsonWithModel<ZPLMineCFG>("ZombiePlagueLegacyCFG.jsonc", "ZPLMineCFG").Configure(builder =>
            {
                builder.AddJsonFile("ZombiePlagueLegacyCFG.jsonc", false, true);
                builder.SetFileLoadExceptionHandler(ctx =>
                {
                    Core.Logger.LogError("[ZM] Failed to load ZombiePlagueLegacyCFG.jsonc (ZPLMineCFG): {Error}. Using last valid configuration.", ctx.Exception.Message);
                    ctx.Ignore = true;
                });
            });
        }

        var collection = new ServiceCollection();
        collection.AddSwiftly(Core);
        collection.AddSingleton<ISwiftlyCore>(Core);
        collection.AddSingleton<IZombiePlagueLegacyAPI>(_apiInstance);
        collection.AddSingleton(_apiInstance);

        collection
            .AddOptionsWithValidateOnStart<ZPLMainCFG>()
            .BindConfiguration("ZPLMainCFG");

        collection
            .AddOptionsWithValidateOnStart<ZPLVoxCFG>()
            .BindConfiguration("ZPLVoxCFG");

        collection
            .AddOptionsWithValidateOnStart<ZPLZombieClassCFG>()
            .BindConfiguration("ZPLZombieClassCFG");

        collection
            .AddOptionsWithValidateOnStart<ZPLSpecialClassCFG>()
            .BindConfiguration("ZPLSpecialClassCFG");

        collection
            .AddOptionsWithValidateOnStart<ZPLWeaponsCFG>()
            .BindConfiguration("ZPLWeaponsCFG");

        collection
            .AddOptionsWithValidateOnStart<ZPLExtraItemsCFG>()
            .BindConfiguration("ZPLExtraItemsCFG");

        collection
            .AddOptionsWithValidateOnStart<ZPLMineCFG>()
            .BindConfiguration("ZPLMineCFG");

        collection.AddSingleton<ZPLGlobals>();

        // ── Ammo Packs service (Economy-only) ─────────────────────────────────
        collection.AddSingleton<AmmoPacksService>();

        // ── Mine service and menu ─────────────────────────────────────────────
        collection.AddSingleton<ZPLMineService>();

        // ── MySQL zombie class preference service ─────────────────────────────
        collection.AddSingleton<ZPLPlayerPrefsService>();

        collection.AddSingleton<ZPLEvents>();
        collection.AddSingleton<ZPLHelpers>();
        collection.AddSingleton<ZPLClassAbilities>();
        collection.AddSingleton<ZPLServices>();
        collection.AddSingleton<ZPLCommands>();
        collection.AddSingleton<PlayerZombieState>();
        collection.AddSingleton<ZPLMenuHelper>();
        collection.AddSingleton<ZPLZombieClassMenu>();
        collection.AddSingleton<ZPLAdminItemMenu>();
        collection.AddSingleton<ZPLGameMode>();
        collection.AddSingleton<ZPLWeaponsMenu>();
        collection.AddSingleton<ZPLExtraItemsMenu>();
        collection.AddSingleton<ZPLGameMenu>();
        ServiceProvider = collection.BuildServiceProvider();

        Core.Logger.LogInformation("[ZM] Load: ServiceProvider built successfully. Retrieving registered services...");

        // Break circular dependency: inject ZPLServices into ZPLExtraItemsMenu post-build
        ServiceProvider.GetRequiredService<ZPLExtraItemsMenu>()
            .SetServices(ServiceProvider.GetRequiredService<ZPLServices>());

        // Wire ZPLExtraItemsMenu into ZPLServices (needed for active player reward)
        ServiceProvider.GetRequiredService<ZPLServices>()
            .SetExtraItemsMenu(ServiceProvider.GetRequiredService<ZPLExtraItemsMenu>());

        _apiInstance.Initialize(
            Core,
            ServiceProvider.GetRequiredService<ILogger<ZombiePlagueLegacyAPI>>(),
            ServiceProvider.GetRequiredService<ZPLGlobals>(),
            ServiceProvider.GetRequiredService<ZPLHelpers>(),
            ServiceProvider.GetRequiredService<ZPLServices>(),
            ServiceProvider.GetRequiredService<IOptionsMonitor<ZPLMainCFG>>(),
            ServiceProvider.GetRequiredService<PlayerZombieState>(),
            ServiceProvider.GetRequiredService<IOptionsMonitor<ZPLZombieClassCFG>>(),
            ServiceProvider.GetRequiredService<IOptionsMonitor<ZPLSpecialClassCFG>>(),
            ServiceProvider.GetRequiredService<ZPLGameMode>()
        );

        _Globals = ServiceProvider.GetRequiredService<ZPLGlobals>();
        _Events = ServiceProvider.GetRequiredService<ZPLEvents>();
        _Commands = ServiceProvider.GetRequiredService<ZPLCommands>();

        var ZriotCFGMonitor = ServiceProvider.GetRequiredService<IOptionsMonitor<ZPLMainCFG>>();
        _ZPLMainCFG = ZriotCFGMonitor.CurrentValue;
        ZriotCFGMonitor.OnChange(newConfig =>
        {
            _ZPLMainCFG = newConfig;
        });

        // ── MySQL preference persistence ──────────────────────────────────────
        // Only initialise when a connection name is configured.
        if (!string.IsNullOrWhiteSpace(_ZPLMainCFG.DatabaseConnection))
        {
            _prefsService = ServiceProvider.GetRequiredService<ZPLPlayerPrefsService>();
            _prefsService.EnsureSchema(_ZPLMainCFG.DatabaseConnection);
            _prefsService.LoadAll(ServiceProvider.GetRequiredService<PlayerZombieState>());
            _apiInstance.ZPL_OnPreferenceChanged += _prefsService.OnPreferenceChanged;
        }

        _Events.HookEvents();
        _Events.HookZombieSoundEvents();
        _Commands.Command();
        _Commands.MenuCommands();
        
        Core.Logger.LogInformation("[ZM] Load() completed successfully. Plugin ready. Waiting for UseSharedInterface() for Economy integration...");
    }

    public override void Unload()
    {
        // Close all open menus BEFORE unhooking events and disposing services.
        // On hot-reload (map change) SwiftlyS2 does not automatically cancel
        // per-player menu render timers, so they would continue to fire after
        // the ServiceProvider is disposed and access freed managed objects,
        // crashing the server with SIGSEGV (MenuAPI.BuildMenuHtml at 0x0).
        foreach (var player in Core.PlayerManager.GetAllPlayers())
        {
            if (player != null && player.IsValid)
                Core.MenusAPI.CloseActiveMenu(player);
        }

        // Cancel all global scheduled timers before disposing services.
        // StopOnMapChange() only fires on actual map changes; on a same-map
        // hot-reload the timers would continue to run after ServiceProvider
        // is disposed and dereference freed objects, crashing the server.
        _Globals?.g_hCountdown?.Cancel();
        _Globals?.g_hRoundEndTimer?.Cancel();
        _Globals?.g_ZombieRegenTimer?.Cancel();
        _Globals?.g_IdleTimer?.Cancel();
        _Globals?.g_hAmbMusic?.Cancel();
        _Globals?.AssassinTimer?.Cancel();

        // Dezabonare de la evenimentele Economy
        ServiceProvider?.GetRequiredService<AmmoPacksService>().Dispose();

        // Unsubscribe MySQL preference listener before disposing the API.
        if (_prefsService != null)
        {
            _apiInstance.ZPL_OnPreferenceChanged -= _prefsService.OnPreferenceChanged;
            _prefsService = null;
        }

        _Events?.UnhookZombieSoundEvents();
        _Events?.UnhookEvents();
        _apiInstance!.Dispose();
        ServiceProvider!.Dispose();
    }
}
