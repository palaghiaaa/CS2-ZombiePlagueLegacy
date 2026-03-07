using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using System.Drawing;

namespace ZombieOutstandingCS2;

public class ZOMenuHelper
{
    private readonly ILogger<ZOMenuHelper> _logger;
    private readonly ISwiftlyCore _core;

    public ZOMenuHelper(ISwiftlyCore core, ILogger<ZOMenuHelper> logger)
    {
        _core = core;
        _logger = logger;
    }

    public IMenuAPI CreateMenu(string title)
    {
        MenuConfiguration configuration = new()
        {
            Title = HtmlGradient.GenerateGradientText(title, Color.LightGreen),
            FreezePlayer = false,
            MaxVisibleItems = 5,
            PlaySound = true,
            AutoIncreaseVisibleItems = false,
            HideFooter = false
        };

        return _core.MenusAPI.CreateMenu(configuration, default);
    }

}
    