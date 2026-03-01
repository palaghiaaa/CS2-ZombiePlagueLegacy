<div align="center">

<img width="600" height="131" alt="Zombie Outstanding CS2" src="https://github.com/user-attachments/assets/d0316faa-c2d0-478f-a642-1e3c3651f1d4" />

<h2>Zombie Outstanding — Counter-Strike 2</h2>

<p>A full-featured Zombie Plague plugin for CS2, built on the <strong>SwiftlyS2</strong> framework.<br>
Ammo Packs are persisted via the <strong>Economy</strong> plugin — no database setup needed.</p>

**[▶ Video Preview](https://www.youtube.com/watch?v=DVeR5u28M_s)**

[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)
[![Framework](https://img.shields.io/badge/Framework-SwiftlyS2-orange)](https://github.com/swiftly-solution/swiftlys2)
[![Economy](https://img.shields.io/badge/Requires-Economy%20Plugin-green)](https://github.com/SwiftlyS2-Plugins/Economy)

</div>

---

## 📋 Table of Contents

1. [Features](#-features)
2. [Dependencies](#-dependencies)
3. [Workshop Assets](#-workshop-assets)
4. [Installation](#-installation)
5. [Commands](#-commands)
6. [Game Modes](#-game-modes)
7. [Zombie Classes](#-zombie-classes)
8. [Special Classes](#-special-classes)
9. [Extra Items Shop](#-extra-items-shop)
10. [Laser Trip Mines](#-laser-trip-mines)
11. [Grenades](#-grenades)
12. [Ammo Packs & Rewards](#-ammo-packs--rewards)
13. [Dark Atmosphere](#-dark-atmosphere)
14. [Configuration Reference](#-configuration-reference)
15. [Translations](#-translations)
16. [API](#-api)

---

## ✨ Features

| Feature | Details |
|---------|---------|
| 🗺️ **10 Game Modes** | Infection, Multi-Infection, Nemesis, Survivor, Sniper, Swarm, Plague, Assassin, Hero, Assassin vs Sniper |
| 🧟 **6 Zombie Classes** | Classic Zombie, Raptor, Tight Zombie, Mutant, Predator Blue, Regenerator |
| 👑 **3 Special Classes** | Nemesis, Assassin, Mother Zombie — each with own HP / Speed / Gravity / Damage |
| 🛒 **Extra Items Shop** | Ammo-pack currency; Armor, Grenades, Jetpack, Laser Mine, SCBA Suit, Revive Token, and more |
| 💰 **Damage-Based AP Rewards** | Every N damage dealt to zombies → +AP (configurable) |
| 💣 **Laser Trip Mines** | Two types: beam trap (6 AP) and explosive (10 AP); plant with `!mine` |
| 🚀 **Jetpack** | CTRL+SPACE to fly; right-click to fire a rocket |
| 🧪 **SCBA Suit** | Absorbs one zombie infection |
| ❤️ **Revive Token** | Auto-respawn once on death |
| 🏃 **Multi-Jump & Knife Blink** | Stackable extra jumps; teleport blink on knife swing |
| ⚡ **Knockback System** | Per-hit-location and per-hero damage multipliers |
| 🌑 **Dark Atmosphere** | Configurable per-server fog and screen darkness via tonemap; applied on every map load |
| 💾 **AP Persistence via Economy** | Balances survive reconnects, map changes, and server restarts |
| 🔌 **Full Plugin API** | `IZombieOutstandingAPI` — external plugins can hook events, query state, and set roles |
| 🔊 **Vox / Sound System** | Countdown, mode announcements, win sounds, ambient music |

---

## 📦 Dependencies

> **All dependencies are required.** The plugin will not load correctly if any of them are missing.

| Dependency | Version | Link | Notes |
|------------|---------|------|-------|
| **SwiftlyS2** | latest | [swiftly-solution/swiftlys2](https://github.com/swiftly-solution/swiftlys2) | Core plugin framework |
| **Economy plugin** | latest | [SwiftlyS2-Plugins/Economy](https://github.com/SwiftlyS2-Plugins/Economy) | **Required** — stores all Ammo Pack balances |

### Why Economy?

Ammo Packs are stored **exclusively** through the Economy plugin. This means:

- ✅ Balances survive reconnects, map changes and server restarts automatically
- ✅ No MySQL / database setup required for this plugin
- ✅ Balances can be shared with other Economy-compatible plugins (e.g. shop, rewards)
- ✅ Economy handles all persistence, loading and saving

### Economy Setup

1. Install the Economy plugin following its own README.
2. In Economy's config, create a wallet kind named **`ammo`** (the name is set by `EconomyWalletKind` in `ZombieOutstandingCFG.jsonc` — default is `"ammo"`).
3. That's it — the plugin registers the wallet kind automatically on startup if it doesn't already exist.

---

## 🎨 Workshop Assets

| Asset | Workshop ID |
|-------|-------------|
| 🔊 Sound pack | [3644652779](https://steamcommunity.com/sharedfiles/filedetails/?id=3644652779) |
| 🧟 Zombie models | [3170427476](https://steamcommunity.com/sharedfiles/filedetails/?id=3170427476) |
| 💣 Laser mine model | [3618032051](https://steamcommunity.com/workshop/filedetails/?id=3618032051) |

---

## 🚀 Installation

```
1. Install SwiftlyS2 on your CS2 server.
2. Install the Economy plugin and configure it (create wallet kind "ammo").
3. Copy the plugin folder to:
       addons/swiftlys2/plugins/ZombieOutstandingCS2/
4. (Optional) Subscribe to the Workshop assets above.
5. Start / reload the server:  sw_reload
6. Edit configs under:
       configs/plugins/ZombieOutstandingCS2/
7. Check the server console for any load errors.
```

### File Layout

```
addons/swiftlys2/plugins/
└── ZombieOutstandingCS2/
    └── ZombieOutstandingCS2.dll

configs/plugins/
└── ZombieOutstandingCS2/
    ├── ZombieOutstandingCFG.jsonc  ← Core settings, game modes, special classes, weapons, vox, mines
    ├── ZombieClassesCFG.jsonc      ← Zombie class stats & sounds
    └── ExtraItemsCFG.jsonc         ← Extra items shop, AP rewards, item prices

translations/
└── en.jsonc                      ← English strings (copy to translations folder)
```

---

## 💬 Commands

### Player Commands

| Command | Chat Alias | Description |
|---------|-----------|-------------|
| `sw_zmenu` | `!zmenu` | Open the main game menu |
| `sw_zextra` | `!zextra` | Open the Extra Items shop |
| `sw_buyweapons` | `!buyweapons` | Open the weapon buy menu (alive CT only) |
| `sw_zclass` | `!zclass` | Choose your zombie class preference |
| `sw_blink` | `!blink` | Activate Knife Blink (costs 1 charge) |
| `sw_mine` | `!mine` | Open the mine menu to plant / manage mines |

> Command names can be changed freely in `ZombieOutstandingCFG.jsonc` under the command keys (`MainMenuCommand`, `ExtraItemsCommand`, etc.).

### Admin Commands

| Command | Description | Default Permission |
|---------|-------------|-------------------|
| `sw_zadmin` | Open the admin action menu | `hzp.adminmenu` |

> The required permission is set by `AdminMenuPermission` in `ZombieOutstandingCFG.jsonc`. Leave empty (`""`) to allow everyone.

---

## 🗺️ Game Modes

All modes are configured in `ZombieOutstandingCFG.jsonc`. Each supports `Enable`, `Weight`, `ZombieCanReborn`, and `EnableInfiniteClipMode`.

| # | Mode | Description |
|---|------|-------------|
| 1 | 🧟 **Normal Infection** | 1 Mother Zombie infects the rest |
| 2 | 🧟🧟 **Multi Infection** | Multiple Mother Zombies start at once |
| 3 | 💀 **Nemesis** | 1 ultra-powerful Nemesis; no infection |
| 4 | 🏹 **Survivor** | 1 human Survivor (XM1014) vs all zombies |
| 5 | 🎯 **Sniper** | 1 human Sniper (AWP) vs all zombies |
| 6 | 🌊 **Swarm** | Half the players become zombies instantly |
| 7 | ☠️ **Plague** | Half zombies + 1 Nemesis + 1 Survivor |
| 8 | 🥷 **Assassin** | 1 invisible Assassin zombie; no infection |
| 9 | 🦸 **Hero** | Last X humans become Heroes with extreme stats |
| 10 | ⚔️ **Assassin vs Sniper** | Assassin zombie vs Sniper human |

---

## 🧟 Zombie Classes

Configured in `ZombieClassesCFG.jsonc`. Stats match the original **Zombie Outstanding (ZO) v7.1** class sources.

| Class | HP | Speed | Gravity | Special |
|-------|----|-------|---------|---------|
| 🧟 **Classic Zombie** | 6 000 | 1.16× | 0.60 | Balanced — the default class |
| 🦅 **Raptor** | 4 800 | 1.22× | 1.00 | Fastest zombie |
| 🔒 **Tight Zombie** | 7 500 | 0.88× | 0.80 | High HP, double-jump |
| 👾 **Mutant** | 6 250 | 0.98× | 1.00 | Extra health |
| 💙 **Predator Blue** | 5 600 | 1.12× | 0.80 | Powerful attacker |
| 💉 **Regenerator** | 4 750 | 1.00× | 1.00 | Regenerates 350 HP every 5 s |

> **Speed** is a multiplier relative to default human speed (250 u/s).  
> **MotherZombieHealth** = class HP × 2.5 (from `zp_zombie_first_hp`).

---

## 👑 Special Classes

Configured in `ZombieOutstandingCFG.jsonc` (under `ZOSpecialClassCFG`).

| Class | HP | Speed | Gravity | Damage | Used In |
|-------|----|-------|---------|--------|---------|
| 🧟 **Mother Zombie** | 15 000 | 1.16× | 0.60 | 150 | Normal / Multi Infection |
| 💀 **Nemesis** | 120 000 | 1.00× | 0.50 | 250 | Nemesis / Plague |
| 🥷 **Assassin** | 24 000 | 3.50× | 0.50 | 357 | Assassin / AVS |

---

## 🛒 Extra Items Shop

Open with `!zextra` or via the main menu (`!zmenu`). Items are purchased with **Ammo Packs (AP)**.

### Item Catalogue

| Item | Team | Price | Description |
|------|------|-------|-------------|
| 🛡️ **Armor** | Human | 3 AP | Grants 100 armor points |
| 💥 **HE Grenade** | Human | 2 AP | Explosive grenade |
| ⚡ **Flash Grenade** | Human | 2 AP | Flashbang / light grenade |
| 💨 **Smoke Grenade** | Human | 2 AP | Freeze grenade |
| 🔥 **Incendiary Bomb** | Human | 4 AP | Area fire damage |
| 🌀 **Teleport Grenade** | Human | 3 AP | Decoy teleporter |
| 🧪 **SCBA Suit** | Human | 5 AP | Absorbs one zombie infection |
| 🦘 **Multi-Jump (+1 jump)** | Human | 4 AP | Stackable, up to `MultijumpMax` |
| 🗡️ **Knife Blink (3 charges)** | Human | 5 AP | Teleport blink on knife swing (`!blink`) |
| 🚀 **Jetpack** | Human | 10 AP | CTRL+SPACE to fly; right-click to fire a rocket |
| 💣 **Laser Mine** | Human | 6 AP | Opens mine menu — choose Laser Tripwire (6 AP) or Explosive Mine (10 AP) |
| ❤️ **Revive Token** | Human | 8 AP | Auto-respawn once on death |
| 💊 **Antidote** | Zombie | 8 AP | Converts zombie back to human |
| 🛡️ **Zombie Madness** | Zombie | 6 AP | Temporary invulnerability (10 s) |
| 🧬 **T-Virus Grenade** | Zombie | 6 AP | Infects humans in radius |

> Items whose corresponding `ZombieOutstandingCFG` toggle is `false` are automatically hidden.

---

## 💣 Laser Trip Mines

Mines are configured in `ZombieOutstandingCFG.jsonc` (under `ZOMineCFG`). Open the mine menu with `!mine` after purchasing the **Laser Mine** item from the shop.

### Mine Types

| Type | Price | Behavior | Beam Color | Limit |
|------|-------|----------|------------|-------|
| 💚 **Laser Tripwire** | 6 AP | Continuously deals damage (10 dmg/tick, every 0.1 s) to any zombie crossing the beam | Blue | 2 per player |
| 🔴 **Explosive Mine** | 10 AP | Explodes when beam is crossed (radius 360 u, up to 2 600 dmg) | Red | 2 per player |

### Settings

| Setting | Default |
|---------|---------|
| Plant / manage | `!mine` / `sw_mine` |
| Max active per player per type | 2 |
| Beam length | 300 units |
| Explosion radius | 360 units |
| Max explosion damage | 2 600 (linear falloff) |
| Team restriction | CT only |

Mine visuals (color, model, sounds) and all other settings → `ZombieOutstandingCFG.jsonc` (`ZOMineCFG` section).

---

## 🚀 Jetpack Details

- Hold **CTRL + SPACE** to fly (consumes fuel).
- **Right-click** to fire a rocket (cooldown: 2 s).
- Fuel resets every round.
- Configure in `ExtraItemsCFG.jsonc`: `JetpackMaxFuel`, `JetpackThrustForce`, `JetpackFuelConsumeRate`, `JetpackRocketDamage`, `JetpackRocketRadius`.

---

## 💣 Grenades

Configured in `ZombieOutstandingCFG.jsonc`.

| Grenade | Toggle | Auto-Give | Range | Duration | Effect |
|---------|--------|-----------|-------|----------|--------|
| 🔥 Incendiary | `FireGrenade` | `SpawnGiveFireGrenade` | 300 u | 5 s | 500 initial + 10/s burn |
| ⚡ Light / Flash | `LightGrenade` | `SpawnGiveLightGrenade` | 1 000 u | 30 s | Blind / light effect |
| ❄️ Freeze | `FreezeGrenade` | `SpawnGiveFreezeGrenade` | 300 u | 10 s | Freezes target |
| 🌀 Teleport | `TelportGrenade` | `SpawnGiveTelportGrenade` | — | — | Teleports player |
| 💣 Incendiary Bomb | — | `SpawnGiveIncGrenade` | — | — | Fire damage area |
| 🧬 T-Virus (Zombie) | — | — | 300 u | — | Infects humans in radius |

---

## 💰 Ammo Packs & Rewards

Ammo Packs (AP) are the in-game currency used to buy Extra Items. All balances are stored and managed by the **Economy plugin** — no reconnect loss, no manual saves needed.

### Earning AP

| Source | Amount | Config Key |
|--------|--------|-----------|
| Survive a round as human | +3 | `RoundSurviveReward` |
| Zombie kills / infects a human | +2 | `ZombieKillReward` |
| Human deals N damage to zombies | +1 per threshold | `HumanDamageRewardThreshold` / `HumanDamageReward` |
| Admin grant | any | Economy plugin admin commands |

> The damage reward stacks: deal 2× the threshold → earn 2× the reward, etc.

### Economy Wallet Kind

AP balances live in a wallet kind configured by `EconomyWalletKind` in `ZombieOutstandingCFG.jsonc` (default: `"ammo"`). The plugin registers this wallet kind in Economy automatically on startup if it doesn't already exist.

---

## ⚙️ Configuration Reference

### `ZombieOutstandingCFG.jsonc` — Core Settings

```jsonc
{
  "ZOMainCFG": {
    // ── Round timing ────────────────────────────────────────────────────────
    "RoundReadyTime": 22.0,       // Seconds before Mother Zombie appears
    "RoundTime": 4.0,             // Round duration in minutes

    // ── Human base stats ────────────────────────────────────────────────────
    "HumanMaxHealth": 150,
    "HumanInitialSpeed": 1.0,
    "HumanInitialGravity": 1.0,

    // ── Knockback ───────────────────────────────────────────────────────────
    "KnockZombieForce": 250.0,
    "StunZombieTime": 0.1,

    // ── Grenades (each has a toggle + optional auto-give) ───────────────────
    "FireGrenade": true,
    "SpawnGiveFireGrenade": true,
    "LightGrenade": true,
    "SpawnGiveLightGrenade": true,
    "FreezeGrenade": true,
    "SpawnGiveFreezeGrenade": true,
    "TelportGrenade": true,
    "SpawnGiveTelportGrenade": false,

    // ── Special features ────────────────────────────────────────────────────
    "CanUseScbaSuit": true,
    "TVirusCanInfectHero": true,

    // ── Commands (change the trigger word here) ─────────────────────────────
    "MainMenuCommand": "sw_zmenu",
    "ExtraItemsCommand": "sw_zextra",
    "ZombieClassCommand": "sw_zclass",
    "AdminMenuItemCommand": "sw_zadmin",
    "BuyWeaponsCommand": "sw_buyweapons",
    "KnifeBlinkCommand": "sw_blink",
    "MineMenuCommand": "sw_mine",

    // ── Admin ───────────────────────────────────────────────────────────────
    "AdminMenuPermission": "hzp.adminmenu",  // Empty = everyone; or "perm1,perm2"

    // ── Chat ────────────────────────────────────────────────────────────────
    "ChatPrefix": "[red][INFO][default]",

    // ── Ammo Packs (Economy plugin) ─────────────────────────────────────────
    "EconomyWalletKind": "ammo",

    // ── Atmosphere (fog + darkness) — see "Dark Atmosphere" section ──────────
    "Atmosphere": {
      "FogEnable": false,
      "FogColor": "100,120,130",
      "FogStart": 400.0,
      "FogEnd": 2000.0,
      "FogMaxDensity": 0.7,
      "DarknessEnable": false,
      "ExposureMin": 0.1,
      "ExposureMax": 0.3
    }
  }
}
```

---

### `ZombieOutstandingCFG.jsonc` — Laser Mine Types

```jsonc
{
  "ZOMineCFG": {
    "MineList": [
      {
        "Name": "Laser Tripwire",   // Beam trap — continuous damage
        "CanExplorer": false,
        "Price": 6,                 // Cost in ammo packs
        "Limit": 2,                 // Max active per player
        "Team": "ct",
        "LaserRate": 0.1,
        "LaserDamage": 10.0,
        "LaserKnockBack": 100.0
      },
      {
        "Name": "Explosive Mine",   // Explodes on beam cross
        "CanExplorer": true,
        "Price": 10,                // Cost in ammo packs
        "Limit": 2,
        "Team": "ct",
        "ExplorerRadius": 360,
        "ExplorerDamage": 2600
      }
    ]
  }
}
```

---

### `ExtraItemsCFG.jsonc` — Items & AP Rewards

```jsonc
{
  "ZOExtraItemsCFG": {
    // ── AP Rewards ──────────────────────────────────────────────────────────
    "RoundSurviveReward": 3,              // AP for surviving a round as human
    "ZombieKillReward": 2,                // AP for a zombie killing a human
    "HumanDamageRewardThreshold": 600,    // Damage dealt needed to earn +AP
    "HumanDamageReward": 1,               // AP earned per threshold crossed

    // ── Item list ───────────────────────────────────────────────────────────
    "Items": [
      { "Key": "armor",            "Name": "Armor (100 AP)",                    "Price": 3,  "Team": "Human"  },
      { "Key": "he_grenade",       "Name": "HE Grenade",                        "Price": 2,  "Team": "Human"  },
      { "Key": "flash_grenade",    "Name": "Flash Grenade",                     "Price": 2,  "Team": "Human"  },
      { "Key": "smoke_grenade",    "Name": "Smoke Grenade",                     "Price": 2,  "Team": "Human"  },
      { "Key": "inc_grenade",      "Name": "Incendiary Bomb",                   "Price": 4,  "Team": "Human"  },
      { "Key": "teleport_grenade", "Name": "Teleport Grenade",                  "Price": 3,  "Team": "Human"  },
      { "Key": "scba_suit",        "Name": "SCBA Suit (resist one attack)",      "Price": 5,  "Team": "Human"  },
      { "Key": "multijump",        "Name": "Multi-Jump (+1 jump)",              "Price": 4,  "Team": "Human"  },
      { "Key": "knife_blink",      "Name": "Knife Blink (3 charges)",           "Price": 5,  "Team": "Human"  },
      { "Key": "jetpack",          "Name": "Jetpack",                           "Price": 10, "Team": "Human"  },
      { "Key": "laser_mine",       "Name": "Laser Mine (opens mine menu)",      "Price": 6,  "Team": "Human"  },
      { "Key": "revive_token",     "Name": "Revive Token (respawn once)",       "Price": 8,  "Team": "Human"  },
      { "Key": "antidote",         "Name": "Antidote (cure to human)",          "Price": 8,  "Team": "Zombie" },
      { "Key": "zombie_madness",   "Name": "Zombie Madness (10s invulnerable)", "Price": 6,  "Team": "Zombie" },
      { "Key": "t_virus_grenade",  "Name": "T-Virus Grenade",                   "Price": 6,  "Team": "Zombie" }
    ]
  }
}
```

---

### `ZombieClassesCFG.jsonc` — Zombie Class Schema

```jsonc
{
  "ZOZombieClassCFG": {
    "ZombieClassList": [
      {
        "Name": "Classic Zombie",
        "Enable": true,
        "Stats": {
          "Health": 6000,
          "MotherZombieHealth": 15000,
          "Speed": 1.16,
          "Damage": 60.0,
          "Gravity": 0.6,
          "Fov": 110,
          "EnableRegen": true,
          "HpRegenSec": 5.0,
          "HpRegenHp": 100
        },
        "Models": { "ModelPath": "characters/models/..." },
        "Sounds": {
          "SoundInfect": "han.human.mandeath",
          "SoundPain":   "han.hl.zombie.pain"
        }
      }
    ]
  }
}
```

---

## 🌐 Translations

Translation files live in the `translations/` folder:

```
translations/
└── en.jsonc    ← English (bundled)
```

Key strings:

| Key | Default (EN) |
|-----|-------------|
| `RoundStartAnnounce` | `New round begins. \| Your credits: {0} \| Players connected: {1}` |
| `ServerGameHumanWin` | `Humans WIN !!!` |
| `ServerGameZombieWin` | `Zombies WIN !!!` |
| `APHumanDamageReward` | `You earned {0} Ammo Pack(s) for dealing damage to zombies!` |
| `APZombieKillReward` | `You earned {0} Ammo Pack(s) for infecting a human! Total: {1}` |
| `APRoundSurviveReward` | `You earned {0} Ammo Pack(s) for surviving the round! Total: {1}` |
| `ExtraItemsMenuAP` | `Your Ammo Packs: {0}` |
| `ExtraItemsScbaSuitSuccess` | `You put on a Hazmat Suit and can resist one zombie attack!` |
| `TripMinePlanted` | `Mine planted ({0}/{1} active). Zombies crossing the laser beam will trigger the explosion!` |

---

## 🔌 API

`IZombieOutstandingAPI` is exposed as a SwiftlyS2 shared interface for external plugin integration.

### Registering

```csharp
public override void UseSharedInterface(IInterfaceManager interfaceManager)
{
    if (interfaceManager.HasSharedInterface("ZombieOutstanding"))
    {
        var api = interfaceManager.GetSharedInterface<IZombieOutstandingAPI>("ZombieOutstanding");
        // use api...
    }
}
```

### Capabilities

| Category | Methods / Events |
|----------|-----------------|
| **Events** | `ZO_OnPlayerInfect`, `ZO_OnNemesisSelected`, `ZO_OnGameStart`, `ZO_OnHumanWin`, `ZO_OnZombieWin`, … |
| **Player queries** | `IsZombie`, `IsNemesis`, `IsAssassin`, `IsSurvivor`, `CurrentMode`, … |
| **Actions** | Force-set roles and classes, give/take Ammo Packs, set glow / FOV / god mode |

Full docs: [`src/IZombieOutstandingAPI/IZombieOutstandingAPI.cs`](src/IZombieOutstandingAPI/IZombieOutstandingAPI.cs)

---

<div align="center">

Remade with ❤️ — based on the original plugin by <em>[H-AN / Zombie PlagueS2](https://github.com/H-AN/HanZombiePlagueS2)</em>

</div>

