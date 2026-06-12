using Microsoft.Extensions.Logging;
using SwiftlyS2.Core.Menus.OptionsBase;
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
        return $"<span color=\"{nameColor}\" class=\"fontSize-xxl fontWeight-bold\">{name}</span>  " +
               $"<span color=\"{ColCost}\" class=\"fontSize-xxl fontWeight-bold\">[{price} AP]</span>";
    }

    /// <summary>Wraps a button label with a checkmark when selected.</summary>
    public static string ClassLabel(string name, bool selected)
    {
        string check = selected ? $" <span color=\"{ColSelected}\" class=\"fontSize-xxl fontWeight-bold\">*</span>" : "";
        return $"<span color=\"{ColButton}\" class=\"fontSize-xxl fontWeight-bold\">{name}</span>{check}";
    }

    public static string OptionLabel(string text, string? color = null)
    {
        return $"<span color=\"{color ?? ColButton}\" class=\"fontSize-xxl fontWeight-bold\">{text}</span>";
    }

    public static string HintLabel(string text)
    {
        return $"<span color=\"{ColHint}\" class=\"fontSize-xxl\">{text}</span>";
    }

    public static ButtonMenuOption LargeButton(string text, string color = ColButton, bool closeAfterClick = true)
    {
        return ApplyLargeFormat(new ButtonMenuOption(text)
        {
            CloseAfterClick = closeAfterClick
        }, color, bold: true);
    }

    public static TextMenuOption LargeText(string text, string color = ColHint, bool bold = false)
    {
        return ApplyLargeFormat(new TextMenuOption(text), color, bold);
    }

    public static T ApplyLargeFormat<T>(T option, string color = ColButton, bool bold = true)
        where T : MenuOptionBase
    {
        option.TextStyle = MenuOptionTextStyle.TruncateEnd;
        option.TextSize = MenuOptionTextSize.ExtraLarge;
        option.AfterFormat += (_, args) =>
        {
            string text = args.CustomText ?? args.Option.Text ?? string.Empty;
            string weight = bold ? " fontWeight-bold" : "";
            args.CustomText = $"<span color=\"{color}\" class=\"fontSize-xxl{weight}\">{text}</span>";
        };

        return option;
    }

    // ── Single unified menu factory ───────────────────────────────────────────

    /// <summary>
    /// Creates every menu in ZPL with the unified palette.
    /// Pass a plain string; the factory wraps it in the styled title HTML.
    /// </summary>
    public IMenuAPI CreateMenu(string title)
    {
        // Title: amber marker + near-white text, larger font
        string styledTitle =
            $"<span color=\"{ColAmber}\" class=\"fontSize-xxl fontWeight-bold\">></span> " +
            $"<span color=\"{ColTitle}\" class=\"fontSize-xxl fontWeight-bold\">{title}</span>";

        var builder = _core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(styledTitle);
        builder.Design.SetMaxVisibleItems(4);
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
