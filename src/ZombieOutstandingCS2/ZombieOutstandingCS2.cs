using Economy.Contract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.Services;

namespace ZombieOutstandingCS2;

[PluginMetadata(
    Id = "ZombieOutstandingCS2",
    Version = "1.0",
    Name = "CS2 僵尸瘟疫 for Sw2/CS2 ZombiePlague for Sw2",
    Author = "H-AN",
    Description = "CS2 僵尸瘟疫 SW2版本 CS2 ZombiePlague for SW2.")]

public partial class ZombieOutstandingCS2(ISwiftlyCore core) : BasePlugin(core)
{

    private ServiceProvider? ServiceProvider { get; set; }
    private static readonly ZombieOutstandingAPI _apiInstance = new();
    private ZOMainCFG _ZOMainCFG = null!;
    private ZOGlobals _Globals = null!;
    private ZOEvents _Events = null!;
    private ZOCommands _Commands = null!;

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
        interfaceManager.AddSharedInterface<IZombieOutstandingAPI, ZombieOutstandingAPI>("ZombieOutstanding", _apiInstance);
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        if (ServiceProvider == null) return;

        var ammoPacks = ServiceProvider.GetRequiredService<AmmoPacksService>();

        // ── Economy plugin ────────────────────────────────────────────────────
        try
        {
            if (interfaceManager.HasSharedInterface("Economy.API.v1"))
            {
                var economyApi = interfaceManager.GetSharedInterface<IEconomyAPIv1>("Economy.API.v1");
                if (economyApi != null)
                {
                    ammoPacks.SetApi(economyApi);
                    ammoPacks.EnsureWalletKind();
                    Core.Logger.LogInformation("[ZO] Economy API resolved successfully.");
                }
            }
            else
            {
                Core.Logger.LogWarning("[ZO] Economy plugin not found – ammo packs will not function until the Economy plugin is loaded.");
            }
        }
        catch (Exception ex)
        {
            Core.Logger.LogWarning("[ZO] Economy API lookup failed: {Ex}", ex.Message);
        }
    }

    public override void Load(bool hotReload)
    {
        Core.Configuration.InitializeJsonWithModel<ZOMainCFG>("ZombieOutstandingCFG.jsonc", "ZOMainCFG").Configure(builder =>
        {
            builder.AddJsonFile("ZombieOutstandingCFG.jsonc", false, true);
            builder.SetFileLoadExceptionHandler(ctx =>
            {
                Core.Logger.LogError("[ZO] Failed to load ZombieOutstandingCFG.jsonc (ZOMainCFG): {Error}. Using last valid configuration.", ctx.Exception.Message);
                ctx.Ignore = true;
            });
        });
        Core.Configuration.InitializeJsonWithModel<ZOVoxCFG>("ZombieOutstandingCFG.jsonc", "ZOVoxCFG").Configure(builder =>
        {
            builder.AddJsonFile("ZombieOutstandingCFG.jsonc", false, true);
            builder.SetFileLoadExceptionHandler(ctx =>
            {
                Core.Logger.LogError("[ZO] Failed to load ZombieOutstandingCFG.jsonc (ZOVoxCFG): {Error}. Using last valid configuration.", ctx.Exception.Message);
                ctx.Ignore = true;
            });
        });
        Core.Configuration.InitializeJsonWithModel<ZOZombieClassCFG>("ZombieClassesCFG.jsonc", "ZOZombieClassCFG").Configure(builder =>
        {
            builder.AddJsonFile("ZombieClassesCFG.jsonc", false, true);
            builder.SetFileLoadExceptionHandler(ctx =>
            {
                Core.Logger.LogError("[ZO] Failed to load ZombieClassesCFG.jsonc (ZOZombieClassCFG): {Error}. Using last valid configuration.", ctx.Exception.Message);
                ctx.Ignore = true;
            });
        });
        Core.Configuration.InitializeJsonWithModel<ZOSpecialClassCFG>("ZombieOutstandingCFG.jsonc", "ZOSpecialClassCFG").Configure(builder =>
        {
            builder.AddJsonFile("ZombieOutstandingCFG.jsonc", false, true);
            builder.SetFileLoadExceptionHandler(ctx =>
            {
                Core.Logger.LogError("[ZO] Failed to load ZombieOutstandingCFG.jsonc (ZOSpecialClassCFG): {Error}. Using last valid configuration.", ctx.Exception.Message);
                ctx.Ignore = true;
            });
        });
        Core.Configuration.InitializeJsonWithModel<ZOWeaponsCFG>("ZombieOutstandingCFG.jsonc", "ZOWeaponsCFG").Configure(builder =>
        {
            builder.AddJsonFile("ZombieOutstandingCFG.jsonc", false, true);
            builder.SetFileLoadExceptionHandler(ctx =>
            {
                Core.Logger.LogError("[ZO] Failed to load ZombieOutstandingCFG.jsonc (ZOWeaponsCFG): {Error}. Using last valid configuration.", ctx.Exception.Message);
                ctx.Ignore = true;
            });
        });
        Core.Configuration.InitializeJsonWithModel<ZOExtraItemsCFG>("ExtraItemsCFG.jsonc", "ZOExtraItemsCFG").Configure(builder =>
        {
            builder.AddJsonFile("ExtraItemsCFG.jsonc", false, true);
            builder.SetFileLoadExceptionHandler(ctx =>
            {
                Core.Logger.LogError("[ZO] Failed to load ExtraItemsCFG.jsonc (ZOExtraItemsCFG): {Error}. Using last valid configuration.", ctx.Exception.Message);
                ctx.Ignore = true;
            });
        });
        Core.Configuration.InitializeJsonWithModel<ZOMineCFG>("ZombieOutstandingCFG.jsonc", "ZOMineCFG").Configure(builder =>
        {
            builder.AddJsonFile("ZombieOutstandingCFG.jsonc", false, true);
            builder.SetFileLoadExceptionHandler(ctx =>
            {
                Core.Logger.LogError("[ZO] Failed to load ZombieOutstandingCFG.jsonc (ZOMineCFG): {Error}. Using last valid configuration.", ctx.Exception.Message);
                ctx.Ignore = true;
            });
        });

        var collection = new ServiceCollection();
        collection.AddSwiftly(Core);
        collection.AddSingleton<ISwiftlyCore>(Core);
        collection.AddSingleton<IZombieOutstandingAPI>(_apiInstance);
        collection.AddSingleton(_apiInstance);

        collection
            .AddOptionsWithValidateOnStart<ZOMainCFG>()
            .BindConfiguration("ZOMainCFG");

        collection
            .AddOptionsWithValidateOnStart<ZOVoxCFG>()
            .BindConfiguration("ZOVoxCFG");

        collection
            .AddOptionsWithValidateOnStart<ZOZombieClassCFG>()
            .BindConfiguration("ZOZombieClassCFG");

        collection
            .AddOptionsWithValidateOnStart<ZOSpecialClassCFG>()
            .BindConfiguration("ZOSpecialClassCFG");

        collection
            .AddOptionsWithValidateOnStart<ZOWeaponsCFG>()
            .BindConfiguration("ZOWeaponsCFG");

        collection
            .AddOptionsWithValidateOnStart<ZOExtraItemsCFG>()
            .BindConfiguration("ZOExtraItemsCFG");

        collection
            .AddOptionsWithValidateOnStart<ZOMineCFG>()
            .BindConfiguration("ZOMineCFG");

        collection.AddSingleton<ZOGlobals>();

        // ── Ammo Packs service (Economy-only) ─────────────────────────────────
        collection.AddSingleton<AmmoPacksService>();

        // ── Mine service and menu ─────────────────────────────────────────────
        collection.AddSingleton<ZOMineService>();
        collection.AddSingleton<ZOMineMenu>();

        collection.AddSingleton<ZOEvents>();
        collection.AddSingleton<ZOHelpers>();
        collection.AddSingleton<ZOServices>();
        collection.AddSingleton<ZOCommands>();
        collection.AddSingleton<PlayerZombieState>();
        collection.AddSingleton<ZOMenuHelper>();
        collection.AddSingleton<ZOZombieClassMenu>();
        collection.AddSingleton<ZOAdminItemMenu>();
        collection.AddSingleton<ZOGameMode>();
        collection.AddSingleton<ZOWeaponsMenu>();
        collection.AddSingleton<ZOExtraItemsMenu>();
        collection.AddSingleton<ZOGameMenu>();
        ServiceProvider = collection.BuildServiceProvider();

        // Break circular dependency: inject ZOServices into ZOExtraItemsMenu post-build
        ServiceProvider.GetRequiredService<ZOExtraItemsMenu>()
            .SetServices(ServiceProvider.GetRequiredService<ZOServices>());

        _apiInstance.Initialize(
            Core,
            ServiceProvider.GetRequiredService<ILogger<ZombieOutstandingAPI>>(),
            ServiceProvider.GetRequiredService<ZOGlobals>(),
            ServiceProvider.GetRequiredService<ZOHelpers>(),
            ServiceProvider.GetRequiredService<ZOServices>(),
            ServiceProvider.GetRequiredService<IOptionsMonitor<ZOMainCFG>>(),
            ServiceProvider.GetRequiredService<PlayerZombieState>(),
            ServiceProvider.GetRequiredService<IOptionsMonitor<ZOZombieClassCFG>>(),
            ServiceProvider.GetRequiredService<IOptionsMonitor<ZOSpecialClassCFG>>(),
            ServiceProvider.GetRequiredService<ZOGameMode>()
        );

        _Globals = ServiceProvider.GetRequiredService<ZOGlobals>();
        _Events = ServiceProvider.GetRequiredService<ZOEvents>();
        _Commands = ServiceProvider.GetRequiredService<ZOCommands>();

        var ZriotCFGMonitor = ServiceProvider.GetRequiredService<IOptionsMonitor<ZOMainCFG>>();
        _ZOMainCFG = ZriotCFGMonitor.CurrentValue;
        ZriotCFGMonitor.OnChange(newConfig =>
        {
            _ZOMainCFG = newConfig;
            Core.Logger.LogInformation(Core.Localizer["ServerInfoHotReload"]);
        });

        _Events.HookEvents();
        _Events.HookZombieSoundEvents();
        _Commands.Command();
        _Commands.MenuCommands();
    }

    public override void Unload()
    {
        _apiInstance!.Dispose();
        ServiceProvider!.Dispose();
    }
}
