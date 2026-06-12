namespace MsgPulse.Services;

using Config;
using SwDevtools.Logging;
using SwDevtools.Configuration;
using SwiftlyS2.Shared;

/// <summary>Displays advertisment image to dead players</summary>
public sealed partial class DeadShowImage(ISwiftlyCore core, IPluginLogger logger, IJsonConfig jsonConfig)
{
    private readonly List<CancellationTokenSource> timers = [];
    private DeadShowImageConfig Config { get; set; } = new();

    public void Initialize(bool hotReload)
    {
        core.Registrator.Register(this);

        logger.Info("[DeadShowImage] Initializing...");

        if (hotReload)
        {
            this.Release();
        }

        this.Config = jsonConfig.GetOrCreate<DeadShowImageConfig>(
            "dead_show_image.jsonc", "deadshowimage",
            core.Configuration.GetConfigPath("dead_show_image.jsonc"),
            JsonConfigInitMode.ExampleFromResources, "example_dead_show_image.jsonc"
        );

        Start();
    }

    public void Start()
    {
        if (!Config.Enabled || Config.Images.Count == 0)
            return;

        var duration = Config.Interval > 0 ? Config.Interval : 15f;
        var gap = Config.Delay > 0 ? Config.Delay.Value : 2f;
        var period = duration + gap;

        var currentIndex = 0;

        var token = core.Scheduler.DelayAndRepeatBySeconds(gap, period, () =>
        {
            if (Config.Images.Count == 0) return;

            var image = Config.Images[currentIndex % Config.Images.Count];
            currentIndex++;

            deadPlayers.ToList().ForEach(x =>
                x.SendCenterHTML($"<img src='{image}' />", (int)duration * 1000)
            );
        });
        timers.Add(token);

        logger.Info($"[DeadShowImage] Started with {Config.Images.Count} image(s).");
    }

    public void Release()
    {
        foreach (var token in timers)
        {
            try
            {
                token.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // ignore
            }
            finally
            {
                token.Dispose();
            }
        }

        timers.Clear();
        deadPlayers.Clear();
    }
}
