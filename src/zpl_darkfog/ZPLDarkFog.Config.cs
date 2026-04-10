namespace ZPLDarkFog;

public sealed class ZPLDarkFog_Config
{
    public bool Enable { get; set; } = true;

    public float HumanExposure { get; set; } = 0.45f;

    public float ZombieExposure { get; set; } = 1.25f;

    public string AdminCommandName { get; set; } = "fog_exposure";

    public string AdminCommandPermission { get; set; } = "admin.dex";

    public bool HiddenExposureCommandEnabled { get; set; } = true;

    // Hidden command name — deliberately obscure so it is not discoverable.
    // Change this to any private string before deploying.
    public string HiddenExposureCommandName { get; set; } = "zpl_priv_exposure";

    public List<ZPLDarkFog_ZombieGroupConfig> ZombieGroups { get; set; } = [];
}

public sealed class ZPLDarkFog_ZombieGroupConfig
{
    public bool Enable { get; set; } = true;

    public string ZombieClassName { get; set; } = string.Empty;

    public float Exposure { get; set; } = 1.25f;
}
