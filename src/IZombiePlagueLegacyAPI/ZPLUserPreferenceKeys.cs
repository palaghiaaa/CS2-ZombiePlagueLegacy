namespace ZombiePlagueLegacyCS2;

/// <summary>
/// Well-known boolean user preference keys stored by ZombiePlagueLegacyCS2.
/// </summary>
public static class ZPLUserPreferenceKeys
{
    /// <summary>Per-player toggle for Zombie Plague VOX/countdown sounds.</summary>
    public const string VoxSounds = "vox_sounds";

    /// <summary>Per-player toggle for Zombie Plague fog/dark atmosphere.</summary>
    public const string Fog = "fog_enabled";

    /// <summary>Per-player toggle for ZPLFlashlight usage.</summary>
    public const string Flashlight = "flashlight_enabled";

    /// <summary>Per-player toggle for ZPLTags chat/score tags.</summary>
    public const string Tags = "tags_enabled";

    /// <summary>Per-player toggle for MessagePulse scheduled ad broadcasts.</summary>
    public const string Ads = "ads_enabled";

    /// <summary>Saved preference for external hide-player integrations.</summary>
    public const string HidePlayers = "hide_players";

    /// <summary>Per-player toggle for VIP reward chat notifications.</summary>
    public const string VipRewardMessages = "vip_reward_messages";
}
