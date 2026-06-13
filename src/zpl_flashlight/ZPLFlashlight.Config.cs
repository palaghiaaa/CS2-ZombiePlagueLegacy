namespace ZPLFlashlight;

public sealed class ZPLFlashlight_Config
{
    public bool Enable { get; set; } = true;

    public bool AllowBots { get; set; } = false;

    public int ToggleDebounceMs { get; set; } = 150;

    public string FlashlightKey { get; set; } = "F";

    public string NightVisionKey { get; set; } = "N";

    public bool NightVisionButtonComboEnable { get; set; } = true;

    public string AdminCommandPermission { get; set; } = "admin.dex";

    public ZPLFlashlight_ProfileConfig Human { get; set; } = ZPLFlashlight_ProfileConfig.CreateHumanDefaults();

    public ZPLFlashlight_ProfileConfig Zombie { get; set; } = ZPLFlashlight_ProfileConfig.CreateZombieDefaults();

    public ZPLNightVision_Config NightVision { get; set; } = new();

    public List<ZPLFlashlight_SpecialZombieConfig> SpecialZombies { get; set; } = [];

    public ZPLFlashlight_Config Clone()
    {
        return new ZPLFlashlight_Config
        {
            Enable = Enable,
            AllowBots = AllowBots,
            ToggleDebounceMs = ToggleDebounceMs,
            FlashlightKey = FlashlightKey,
            NightVisionKey = NightVisionKey,
            NightVisionButtonComboEnable = NightVisionButtonComboEnable,
            AdminCommandPermission = AdminCommandPermission,
            Human = (Human ?? ZPLFlashlight_ProfileConfig.CreateHumanDefaults()).Clone(),
            Zombie = (Zombie ?? ZPLFlashlight_ProfileConfig.CreateZombieDefaults()).Clone(),
            NightVision = (NightVision ?? new ZPLNightVision_Config()).Clone(),
            SpecialZombies = SpecialZombies?.Select(group => group.Clone()).ToList() ?? []
        };
    }
}

public sealed class ZPLFlashlight_ProfileConfig
{
    public bool Enable { get; set; } = true;

    public float Brightness { get; set; } = 6.0f;

    public float Distance { get; set; } = 900.0f;

    public float AttachmentDistance { get; set; } = 54.0f;

    public float FovOrConeAngle { get; set; } = 55.0f;

    public bool Shadows { get; set; } = true;

    public string Attachment { get; set; } = "clip_limit";

    public int ColorR { get; set; } = 255;

    public int ColorG { get; set; } = 255;

    public int ColorB { get; set; } = 255;

    public int ColorA { get; set; } = 255;

    public bool VisibleToTeammates { get; set; } = true;

    public ZPLFlashlight_ProfileConfig Clone()
    {
        return new ZPLFlashlight_ProfileConfig
        {
            Enable = Enable,
            Brightness = Brightness,
            Distance = Distance,
            AttachmentDistance = AttachmentDistance,
            FovOrConeAngle = FovOrConeAngle,
            Shadows = Shadows,
            Attachment = Attachment,
            ColorR = ColorR,
            ColorG = ColorG,
            ColorB = ColorB,
            ColorA = ColorA,
            VisibleToTeammates = VisibleToTeammates
        };
    }

    public static ZPLFlashlight_ProfileConfig CreateHumanDefaults()
    {
        return new ZPLFlashlight_ProfileConfig();
    }

    public static ZPLFlashlight_ProfileConfig CreateZombieDefaults()
    {
        return new ZPLFlashlight_ProfileConfig
        {
            Brightness = 5.0f,
            Distance = 850.0f,
            AttachmentDistance = 54.0f,
            FovOrConeAngle = 55.0f,
            ColorR = 255,
            ColorG = 255,
            ColorB = 255,
            ColorA = 255,
            VisibleToTeammates = true
        };
    }

    public static ZPLFlashlight_ProfileConfig CreateNormalNightVisionDefaults()
    {
        return new ZPLFlashlight_ProfileConfig
        {
            Brightness = 9.0f,
            Distance = 2600.0f,
            AttachmentDistance = 8.0f,
            FovOrConeAngle = 360.0f,
            Shadows = false,
            Attachment = "clip_limit",
            ColorR = 0,
            ColorG = 255,
            ColorB = 80,
            ColorA = 255,
            VisibleToTeammates = false
        };
    }

    public static ZPLFlashlight_ProfileConfig CreateSpecialNightVisionDefaults()
    {
        return new ZPLFlashlight_ProfileConfig
        {
            Brightness = 10.0f,
            Distance = 2800.0f,
            AttachmentDistance = 8.0f,
            FovOrConeAngle = 360.0f,
            Shadows = false,
            Attachment = "clip_limit",
            ColorR = 255,
            ColorG = 40,
            ColorB = 32,
            ColorA = 255,
            VisibleToTeammates = false
        };
    }
}

public sealed class ZPLNightVision_Config
{
    public bool Enable { get; set; } = true;

    public List<string> RedClassNames { get; set; } = ["Nemesis", "Assassin"];

    public ZPLFlashlight_ProfileConfig NormalZombie { get; set; } = ZPLFlashlight_ProfileConfig.CreateNormalNightVisionDefaults();

    public ZPLFlashlight_ProfileConfig SpecialZombie { get; set; } = ZPLFlashlight_ProfileConfig.CreateSpecialNightVisionDefaults();

    public ZPLNightVision_Config Clone()
    {
        return new ZPLNightVision_Config
        {
            Enable = Enable,
            RedClassNames = RedClassNames?
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Select(static name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? ["Nemesis", "Assassin"],
            NormalZombie = (NormalZombie ?? ZPLFlashlight_ProfileConfig.CreateNormalNightVisionDefaults()).Clone(),
            SpecialZombie = (SpecialZombie ?? ZPLFlashlight_ProfileConfig.CreateSpecialNightVisionDefaults()).Clone()
        };
    }
}

public sealed class ZPLFlashlight_SpecialZombieConfig
{
    public bool Enable { get; set; } = true;

    public string Name { get; set; } = string.Empty;

    public bool? FlashlightEnable { get; set; }

    public float? Brightness { get; set; }

    public float? Distance { get; set; }

    public float? AttachmentDistance { get; set; }

    public float? FovOrConeAngle { get; set; }

    public bool? Shadows { get; set; }

    public string? Attachment { get; set; }

    public int? ColorR { get; set; }

    public int? ColorG { get; set; }

    public int? ColorB { get; set; }

    public int? ColorA { get; set; }

    public bool? VisibleToTeammates { get; set; }

    public ZPLFlashlight_SpecialZombieConfig Clone()
    {
        return new ZPLFlashlight_SpecialZombieConfig
        {
            Enable = Enable,
            Name = Name,
            FlashlightEnable = FlashlightEnable,
            Brightness = Brightness,
            Distance = Distance,
            AttachmentDistance = AttachmentDistance,
            FovOrConeAngle = FovOrConeAngle,
            Shadows = Shadows,
            Attachment = Attachment,
            ColorR = ColorR,
            ColorG = ColorG,
            ColorB = ColorB,
            ColorA = ColorA,
            VisibleToTeammates = VisibleToTeammates
        };
    }

    public void ApplyTo(ZPLFlashlight_ProfileConfig profile)
    {
        if (FlashlightEnable.HasValue)
        {
            profile.Enable = FlashlightEnable.Value;
        }

        if (Brightness.HasValue)
        {
            profile.Brightness = Brightness.Value;
        }

        if (Distance.HasValue)
        {
            profile.Distance = Distance.Value;
        }

        if (AttachmentDistance.HasValue)
        {
            profile.AttachmentDistance = AttachmentDistance.Value;
        }

        if (FovOrConeAngle.HasValue)
        {
            profile.FovOrConeAngle = FovOrConeAngle.Value;
        }

        if (Shadows.HasValue)
        {
            profile.Shadows = Shadows.Value;
        }

        if (Attachment is not null)
        {
            profile.Attachment = Attachment;
        }

        if (ColorR.HasValue)
        {
            profile.ColorR = ColorR.Value;
        }

        if (ColorG.HasValue)
        {
            profile.ColorG = ColorG.Value;
        }

        if (ColorB.HasValue)
        {
            profile.ColorB = ColorB.Value;
        }

        if (ColorA.HasValue)
        {
            profile.ColorA = ColorA.Value;
        }

        if (VisibleToTeammates.HasValue)
        {
            profile.VisibleToTeammates = VisibleToTeammates.Value;
        }
    }
}
