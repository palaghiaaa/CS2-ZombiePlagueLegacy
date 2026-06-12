using Microsoft.Extensions.Logging;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;

namespace ZombiePlagueLegacyCS2;

public class ZPLUserSettingsMenu
{
    private readonly ILogger<ZPLUserSettingsMenu> _logger;
    private readonly ISwiftlyCore _core;
    private readonly ZPLGlobals _globals;
    private readonly ZPLHelpers _helpers;
    private readonly ZPLMenuHelper _menuHelper;
    private readonly ZPLPlayerPrefsService _prefsService;
    private readonly ZombiePlagueLegacyAPI _api;

    public ZPLUserSettingsMenu(
        ISwiftlyCore core,
        ILogger<ZPLUserSettingsMenu> logger,
        ZPLGlobals globals,
        ZPLHelpers helpers,
        ZPLMenuHelper menuHelper,
        ZPLPlayerPrefsService prefsService,
        ZombiePlagueLegacyAPI api)
    {
        _core = core;
        _logger = logger;
        _globals = globals;
        _helpers = helpers;
        _menuHelper = menuHelper;
        _prefsService = prefsService;
        _api = api;
    }

    public void OpenUserSettingsMenu(IPlayer player)
    {
        if (player == null || !player.IsValid || player.IsFakeClient)
            return;

        IMenuAPI menu = _menuHelper.CreateMenu(_helpers.T(player, "UserSettingsMenuTitle"));
        menu.AddOption(ZPLMenuHelper.LargeText(_helpers.T(player, "UserSettingsMenuHint")));

        AddToggle(menu, player, ZPLUserPreferenceKeys.VoxSounds, "UserSettingsVoxSounds");
        AddToggle(menu, player, ZPLUserPreferenceKeys.Fog, "UserSettingsFog");
        AddToggle(menu, player, ZPLUserPreferenceKeys.Flashlight, "UserSettingsFlashlight");
        AddToggle(menu, player, ZPLUserPreferenceKeys.Tags, "UserSettingsTags");
        AddToggle(menu, player, ZPLUserPreferenceKeys.Ads, "UserSettingsAds");
        AddToggle(menu, player, ZPLUserPreferenceKeys.HidePlayers, "UserSettingsHidePlayers", defaultValue: false);
        AddToggle(menu, player, ZPLUserPreferenceKeys.VipRewardMessages, "UserSettingsVipRewards");

        _core.MenusAPI.OpenMenuForPlayer(player, menu);
    }

    private void AddToggle(IMenuAPI menu, IPlayer player, string key, string labelKey, bool defaultValue = true)
    {
        var preferences = GetOrCreate(player.SteamID);
        bool enabled = preferences.Get(key, defaultValue);
        string state = enabled ? _helpers.T(player, "CommonOn") : _helpers.T(player, "CommonOff");
        string color = enabled ? ZPLMenuHelper.ColSelected : ZPLMenuHelper.ColZombie;

        var btn = ZPLMenuHelper.LargeButton(
            $"{_helpers.T(player, labelKey)}: {state}",
            color,
            closeAfterClick: false);

        btn.Click += async (_, args) =>
        {
            var clicker = args.Player;
            _core.Scheduler.NextTick(() =>
            {
                if (clicker == null || !clicker.IsValid || clicker.IsFakeClient)
                    return;

                var clickerPreferences = GetOrCreate(clicker.SteamID);
                bool current = clickerPreferences.Get(key, defaultValue);
                bool next = !current;
                if (!clickerPreferences.Set(key, next))
                {
                    _logger.LogWarning("[ZPLUserSettings] Unknown preference key '{Key}'.", key);
                    return;
                }

                _prefsService.SaveUserPreferences(clicker.SteamID, clickerPreferences);
                _api.NotifyUserPreferenceChanged(clicker.SteamID, key, next);
                ApplyImmediateEffect(clicker, key, next);

                _helpers.SendChatT(clicker, "UserSettingsToggleSaved",
                    _helpers.T(clicker, labelKey),
                    next ? _helpers.T(clicker, "CommonOn") : _helpers.T(clicker, "CommonOff"));

                OpenUserSettingsMenu(clicker);
            });
        };

        menu.AddOption(btn);
    }

    private UserPreferenceSettings GetOrCreate(ulong steamId)
    {
        if (!_globals.UserPreferences.TryGetValue(steamId, out var preferences))
        {
            preferences = new UserPreferenceSettings();
            _globals.UserPreferences[steamId] = preferences;
        }

        return preferences;
    }

    private void ApplyImmediateEffect(IPlayer player, string key, bool enabled)
    {
        switch (key)
        {
            case ZPLUserPreferenceKeys.Fog:
                if (enabled)
                    _helpers.ApplyFogToPlayer(player);
                else
                    _helpers.ClearFogForPlayer(player);
                break;

            case ZPLUserPreferenceKeys.HidePlayers:
                _helpers.SendChatT(player, "UserSettingsHidePlayersHint");
                break;
        }
    }
}
