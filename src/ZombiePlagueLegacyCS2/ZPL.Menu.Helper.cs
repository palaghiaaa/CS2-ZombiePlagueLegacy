using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using System.Drawing;

namespace ZombiePlagueLegacyCS2;

/// <summary>
/// Central menu factory for the entire ZPL plugin.
/// All menus share one palette for visual consistency:
///
///   Title prefix / markers : #E8A020  (warm amber)
///   Title text             : #E8E8E8  (near-white)
///   Hint / sub-text        : #8899AA  (muted blue-grey)
///   Button text            : #D4D4D4  (soft white)
///   AP cost                : #F0A030  (amber — stands out, not harsh)
///   Selected / checkmark   : #50E890  (mint green)
///   Zombie-only items      : #E06050  (coral — distinct but not painful)
///   Disabled               : #555555  (dark grey)
///   Guide lines            : #3A3A3A  (near-invisible — subtle frame)
/// </summary>
public class ZPLMenuHelper
{
    private readonly ILogger<ZPLMenuHelper> _logger;
    private readonly ISwiftlyCore _core;

    // ── Shared palette ────────────────────────────────────────────────────────
    public const string ColAmber     = "#E8A020";   // title prefix, markers, AP cost hint
    public const string ColTitle     = "#E8E8E8";   // title text
    public const string ColHint      = "#8899AA";   // subtitle / scrolling hint
    public const string ColButton    = "#D4D4D4";   // standard button text
    public const string ColCost      = "#F0A030";   // AP cost value
    public const string ColSelected  = "#50E890";   // ✓ checkmark / selected
    public const string ColZombie    = "#E06050";   // zombie-only items
    public const string ColDisabled  = "#555555";   // disabled options
    public const string ColGuide     = "#3A3A3A";   // guide lines (subtle)

    public ZPLMenuHelper(ISwiftlyCore core, ILogger<ZPLMenuHelper> logger)
    {
        _core   = core;
        _logger = logger;
    }

    // ── Helpers available to all menu files ──────────────────────────────────

    /// <summary>Wraps a button label with the standard colour scheme: item name + AP cost.</summary>
    public static string ItemLabel(string name, int price, bool isZombie = false)
    {
        string nameColor = isZombie ? ColZombie : ColButton;
        return $"<span color=\"{nameColor}\">{name}</span>  <span color=\"{ColCost}\">[{price} AP]</span>";
    }

    /// <summary>Wraps a button label with a checkmark when selected.</summary>
    public static string ClassLabel(string name, bool selected)
    {
        string check = selected ? $" <span color=\"{ColSelected}\">✓</span>" : "";
        return $"<span color=\"{ColButton}\">{name}</span>{check}";
    }

    // ── Single unified menu factory ───────────────────────────────────────────

    /// <summary>
    /// Creates every menu in ZPL with the unified palette.
    /// Pass a plain string; the factory wraps it in the styled title HTML.
    /// </summary>
    public IMenuAPI CreateMenu(string title)
    {
        // Title: amber prefix dot + near-white text, larger font
        string styledTitle =
            $"<span color=\"{ColAmber}\">◈</span> " +
            $"<span color=\"{ColTitle}\" class='fontSize-l'><b>{title}</b></span>";

        var builder = _core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(styledTitle);
        builder.Design.SetMaxVisibleItems(5);
        builder.Design.SetGlobalScrollStyle(MenuOptionScrollStyle.LinearScroll);
        builder.Design.SetNavigationMarkerColor(ColAmber);
        builder.Design.SetMenuFooterColor(ColHint);
        builder.Design.SetVisualGuideLineColor(ColGuide);
        builder.Design.SetDisabledColor(ColDisabled);
        return builder.Build();
    }

    // Keep named aliases so existing calls like CreateShopMenu / CreateZombieClassMenu
    // still compile without touching every call site.
    public IMenuAPI CreateShopMenu(string title)       => CreateMenu(title);
    public IMenuAPI CreateZombieClassMenu(string title) => CreateMenu(title);
}
