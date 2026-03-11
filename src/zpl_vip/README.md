# ZPL VIP

Standalone SwiftlyS2 CS2 plugin that brings classic **Zombie Plague VIP perks** to [ZombiePlagueLegacyCS2](https://github.com/DeadPoolCS2/ZombiePlagueLegacyCS2).

Integrates directly with `IZombiePlagueLegacyAPI` for accurate zombie-state detection and infect events, and with `IEconomyAPIv1` for persistent ammo-pack rewards — no reflection hacks.

---

## Features

| Perk | Config key | AMXX equivalent |
|---|---|---|
| **Armor on spawn** | `ArmorAmount` | `zp_vip_armor` |
| **Multi-jump** | `ExtraJumps` / `JumpVelocity` | `zp_vip_extrajumps` |
| **No fall damage** | `NoFallDamage` | `zp_vip_falldamage` |
| **Damage multiplier** vs zombies | `DamageMultiplier` | `zp_vip_damage` |
| **Damage reward** (AP per X damage) | `DamageRewardThreshold` / `DamageRewardAmount` | `zp_vip_dmgreward_*` |
| **Kill reward** (AP per zombie kill) | `KillRewardAmount` | `zp_vip_killammo` |
| **Happy Hour** (bonus AP + frags) | `HappyHour*` | `zp_vip_happyhour_*` |
| **Infect reward** (VIP-as-zombie) | `InfectRewardsEnabled` | `zp_vip_infectammo/health` |
| **`!vip` menu** – configurable benefit lines | `BenefitLines` | — |
| **`!vips` menu** – online VIP list | `VipsListCommand` | — |
| **Join announce** | `JoinAnnounceEnabled` | — |

All user-facing strings are in **`translations/en.jsonc`** — easy to localise.

---

## Requirements

- CS2 dedicated server with [SwiftlyS2](https://github.com/swiftly-solution/swiftly) loaded.
- **[ZombiePlagueLegacyCS2](https://github.com/DeadPoolCS2/ZombiePlagueLegacyCS2)** — required for zombie-state detection and infect events.
- **Economy plugin** — required for persistent AP rewards (gracefully degraded to chat-only if absent).
- .NET 10 SDK (to build from source).

---

## Installation

1. Build the project or grab `ZPLVIP.dll` from a release.
2. Copy `ZPLVIP.dll` to your SwiftlyS2 plugins folder.
3. Copy `configs/plugins/ZPLVIP/ZPLVIP.jsonc` to the same path on your server.
4. Copy `translations/en.jsonc` into the plugin's `resources/translations/` folder  
   (the build/publish step does this automatically).
5. Add the VIP permission flag to your Swiftly permissions config (see below).
6. Restart or hot-load: `sw_plugins load ZPLVIP`.

---

## Configuration — `configs/plugins/ZPLVIP/ZPLVIP.jsonc`

All settings support **hot-reload**: save the file while the server is running.

```jsonc
{
  "ZPLVIP": {
    "VIPPermission":  "@zplvip/vip",   // permission flag(s), comma-separated
    "VipMenuCommand": "vip",
    "VipsListCommand": "vips",
    "ChatPrefix":     "[VIP]",
    "WalletKind":     "ammo",          // must match ZPL's EconomyWalletKind

    "ArmorAmount":    100,
    "ExtraJumps":     1,
    "JumpVelocity":   300.0,
    "NoFallDamage":   true,

    "DamageMultiplier":   1.5,
    "ExcludeHEGrenade":   true,

    "DamageRewardThreshold": 500,
    "DamageRewardAmount":    1,
    "KillRewardAmount":      2,
    "KillRewardHappyHourBonus": true,

    "HappyHourEnabled": true,
    "HappyHourStart":   19,
    "HappyHourEnd":     8,
    "HappyHourBonusAP": 2,
    "HappyHourBonusFrags": 1,

    "InfectRewardsEnabled": false,
    "InfectRewardAP":       1,
    "InfectRewardHealth":   500,

    // Set to [] to auto-generate lines from perk values above.
    "BenefitLines": [
      "★ Armor on spawn (+100)",
      "★ Double Jump",
      "★ No Fall Damage",
      "★ ×1.5 Damage vs Zombies",
      "★ +1 AP per 500 damage",
      "★ +2 AP per Zombie Kill",
      "★ Happy Hour 19:00–08:00"
    ]
  }
}
```

---

## Translations — `translations/en.jsonc`

Copy and rename (e.g. `fr.jsonc`) to add a new language.

| Key | Description |
|-----|-------------|
| `VipJoinAnnounce` | Broadcast when a VIP spawns. `{0}` = name |
| `VipApReward` | AP reward chat msg. `{0}` = amount, `{1}` = new total |
| `VipApBridgeOffline` | Economy offline fallback. `{0}` = amount |
| `VipMenuIsVip` / `VipMenuNotVip` | VIP status footer line in `!vip` |
| `VipMenuHappyHourActive` | Happy Hour active notice |
| `VipAutoLine*` | Auto-generated benefit lines (used when `BenefitLines` is `[]`) |
| `VipsMenuTitle` | `!vips` title. `{0}` = count |
| `VipsMenuNoVips` | Shown when no VIPs online |
| `VipsMenuEntry` | One row per VIP. `{0}` = name |

---

## Permissions

```jsonc
{
  "Permissions": [
    { "SteamID": "STEAM_1:0:12345678", "Flags": ["@zplvip/vip"] }
  ]
}
```

The flag string must match `VIPPermission` in the plugin config.
Multiple flags may be listed comma-separated; a player matching **any** is VIP.

---

## Project structure

```
src/zpl_vip/
├── ZPLVIP.csproj       # Project file
├── ZPLVIPPlugin.cs     # Plugin entry-point & all logic
├── ZPLVIPConfig.cs     # Strongly-typed config class
├── translations/
│   └── en.jsonc        # English strings
└── README.md           # This file

configs/plugins/ZPLVIP/
└── ZPLVIP.jsonc        # Deployable server config
```

---

## License

Licensed under the same terms as [ZombiePlagueLegacyCS2](../../../LICENSE).
