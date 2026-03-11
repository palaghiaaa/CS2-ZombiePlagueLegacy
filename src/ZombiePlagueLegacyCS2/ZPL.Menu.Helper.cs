using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using System.Drawing;

namespace ZombiePlagueLegacyCS2;

public class ZPLMenuHelper
{
    private readonly ILogger<ZPLMenuHelper> _logger;
    private readonly ISwiftlyCore _core;

    public ZPLMenuHelper(ISwiftlyCore core, ILogger<ZPLMenuHelper> logger)
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

        var menu = _core.MenusAPI.CreateMenu(configuration, default, null, MenuOptionScrollStyle.LinearScroll);
        return menu;
    }

}
    