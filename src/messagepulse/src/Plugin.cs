namespace MsgPulse;

using Services;
using SwDevtools;
using SwDevtools.Logging;
using PlaceholderAPI.Contract;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;
using Microsoft.Extensions.DependencyInjection;
using ZombiePlagueLegacyCS2;

[PluginMetadata(Id = "MessagePulse", Version = "1.0.0", Name = "MessagePulse", Author = "itsAudio",
    Description = "Advertisments, Custom commands, Event based messages and more...")]
public sealed class MessagePulse : BasePlugin
{
    private IPluginLogger Logger { get; }
    private IServiceProvider Provider { get; }

    private MessageProcessor Processor { get; }
    private DynamicEvents Events { get; }
    private MessageScheduler Scheduler { get; }
    private CustomCommandsHandler CustomCommands { get; }
    private DeadShowImage DeadShowImage { get; }

    public MessagePulse(ISwiftlyCore core) : base(core)
    {
        var services = new ServiceCollection();

        services.AddSwiftly(core);

        services.AddSwDevtoolsCore(core, this)
            .AddSwDevtoolsLogging()
            .AddSwDevtoolsConfig()
            .AddSwDevtoolsTranslation();

        services.AddMessageProcessor()
            .AddMessageScheduler()
            .AddDynamicEvents()
            .AddCustomCommands()
            .AddDeadShowImage();

        this.Provider = services.BuildServiceProvider();

        this.Logger = Provider.GetService<IPluginLogger>()!;
        this.Processor = Provider.GetRequiredService<MessageProcessor>();
        this.Events = Provider.GetRequiredService<DynamicEvents>();
        this.Scheduler = Provider.GetRequiredService<MessageScheduler>();
        this.CustomCommands = Provider.GetRequiredService<CustomCommandsHandler>();
        this.DeadShowImage = Provider.GetRequiredService<DeadShowImage>();
    }

    // Register PlaceholderAPI.v1 interface
    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        if (interfaceManager.HasSharedInterface("PlaceholderAPI.v1"))
        {
            var api = interfaceManager.GetSharedInterface<IPlaceholderAPIv1>("PlaceholderAPI.v1");
            Processor.AttachPlaceholderApi(api);
        }

        if (interfaceManager.HasSharedInterface("ZombiePlagueLegacy"))
        {
            Processor.ZombiePlagueApi = interfaceManager.GetSharedInterface<IZombiePlagueLegacyAPI>("ZombiePlagueLegacy");
        }
    }

    public override void Load(bool hotReload)
    {
        Logger.Info("[MessagePulse] Plugin Loading...");

        this.Events.Initialize(hotReload);
        this.Scheduler.Initialize(hotReload);
        this.CustomCommands.Initialize(hotReload);
        this.DeadShowImage.Initialize(hotReload);

        Logger.Info("[MessagePulse] Successfully loaded.");
    }

    public override void Unload()
    {
        // Release all services
        this.Events.Release();
        this.Scheduler.Release();
        this.Processor.Release();
        this.CustomCommands.Release();
        this.DeadShowImage.Release();

        Logger.Info("[MessagePulse] Successfully unloaded.");
    }
}
