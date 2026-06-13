using System.Net;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared.Menus;

namespace ZombiePlagueLegacyCS2.SharedUi;

public static class ZPLMenuStyle
{
    public const string ColAmber = "#E8A020";
    public const string ColTitle = "#E8E8E8";
    public const string ColHint = "#8899AA";
    public const string ColButton = "#D4D4D4";
    public const string ColCost = "#F0A030";
    public const string ColSelected = "#50E890";
    public const string ColZombie = "#E06050";
    public const string ColDisabled = "#555555";

    public static MenuConfiguration MenuConfig(string title, bool playSound = false)
    {
        return new MenuConfiguration
        {
            Title = Title(title),
            FreezePlayer = false,
            MaxVisibleItems = 4,
            PlaySound = playSound,
            AutoIncreaseVisibleItems = false,
            HideFooter = false
        };
    }

    public static string Title(string title)
    {
        return $"<span color=\"{ColAmber}\" class=\"fontSize-xxl fontWeight-bold\">></span> " +
               $"<span color=\"{ColTitle}\" class=\"fontSize-xxl fontWeight-bold\">{WebUtility.HtmlEncode(title)}</span>";
    }

    public static ButtonMenuOption Button(string text, string color = ColButton, bool closeAfterClick = true)
    {
        return ApplyLargeFormat(new ButtonMenuOption(text)
        {
            CloseAfterClick = closeAfterClick
        }, color, bold: true);
    }

    public static TextMenuOption Text(string text, string color = ColHint, bool bold = false)
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
            args.CustomText = WrapText(text, color, bold);
        };

        return option;
    }

    public static string WrapText(string text, string color = ColButton, bool bold = true)
    {
        if (text.Contains("<span", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("<font", StringComparison.OrdinalIgnoreCase))
            return text;

        string weight = bold ? " fontWeight-bold" : string.Empty;
        string encoded = WebUtility.HtmlEncode(text);
        encoded = ConvertChatTags(encoded, color, weight);
        return $"<span color=\"{color}\" class=\"fontSize-xxl{weight}\">{encoded}</span>";
    }

    private static string ConvertChatTags(string text, string defaultColor, string weightClass)
    {
        return text
            .Replace("[default]", $"</span><span color=\"{defaultColor}\" class=\"fontSize-xxl{weightClass}\">", StringComparison.OrdinalIgnoreCase)
            .Replace("[red]", $"</span><span color=\"{ColZombie}\" class=\"fontSize-xxl{weightClass}\">", StringComparison.OrdinalIgnoreCase)
            .Replace("[green]", $"</span><span color=\"{ColSelected}\" class=\"fontSize-xxl{weightClass}\">", StringComparison.OrdinalIgnoreCase)
            .Replace("[lime]", $"</span><span color=\"{ColSelected}\" class=\"fontSize-xxl{weightClass}\">", StringComparison.OrdinalIgnoreCase)
            .Replace("[gold]", $"</span><span color=\"{ColCost}\" class=\"fontSize-xxl{weightClass}\">", StringComparison.OrdinalIgnoreCase)
            .Replace("[yellow]", $"</span><span color=\"{ColCost}\" class=\"fontSize-xxl{weightClass}\">", StringComparison.OrdinalIgnoreCase)
            .Replace("[orange]", $"</span><span color=\"{ColAmber}\" class=\"fontSize-xxl{weightClass}\">", StringComparison.OrdinalIgnoreCase)
            .Replace("[blue]", $"</span><span color=\"#6EA8FF\" class=\"fontSize-xxl{weightClass}\">", StringComparison.OrdinalIgnoreCase)
            .Replace("[white]", $"</span><span color=\"{ColTitle}\" class=\"fontSize-xxl{weightClass}\">", StringComparison.OrdinalIgnoreCase)
            .Replace("[grey]", $"</span><span color=\"{ColHint}\" class=\"fontSize-xxl{weightClass}\">", StringComparison.OrdinalIgnoreCase)
            .Replace("[gray]", $"</span><span color=\"{ColHint}\" class=\"fontSize-xxl{weightClass}\">", StringComparison.OrdinalIgnoreCase);
    }
}
