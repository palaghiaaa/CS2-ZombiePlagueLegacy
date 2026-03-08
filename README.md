<div align="center">

<img width="600" height="131" alt="Zombie Outstanding CS2" src="https://github.com/user-attachments/assets/d0316faa-c2d0-478f-a642-1e3c3651f1d4" />

<h3>A full-featured Zombie Plague gamemode plugin for Counter-Strike 2</h3>

<p>Built on <strong>SwiftlyS2</strong> · Ammo Packs stored via the <strong>Economy</strong> plugin — no database needed</p>

<a href="https://www.youtube.com/watch?v=DVeR5u28M_s">▶ Video Preview</a> &nbsp;·&nbsp;
<a href="LICENSE">GPL-3.0 License</a>

<br/><br/>

[![Framework](https://img.shields.io/badge/Framework-SwiftlyS2-orange?style=for-the-badge)](https://github.com/swiftly-solution/swiftlys2)
[![Economy](https://img.shields.io/badge/Requires-Economy_Plugin-brightgreen?style=for-the-badge)](https://github.com/SwiftlyS2-Plugins/Economy)
[![License](https://img.shields.io/badge/License-GPL_v3-blue?style=for-the-badge)](LICENSE)

</div>

---

## 📋 Table of Contents

1. [Features](#-features)
2. [Dependencies & Setup](#-dependencies--setup)
3. [Workshop Assets](#-workshop-assets)
4. [Installation](#-installation)
5. [Commands](#-commands)
6. [Game Modes](#-game-modes)
7. [Zombie Classes](#-zombie-classes)
8. [Special Classes](#-special-classes)
9. [Extra Items Shop](#-extra-items-shop)
10. [Grenades & Weapons](#-grenades--weapons)
11. [Ammo Packs & Rewards](#-ammo-packs--rewards)
12. [Configuration Reference](#️-configuration-reference)
13. [Translations](#-translations)
14. [API](#-api)

---

## ✨ Features

<table>
<tr>
  <td>🗺️ <strong>10 Game Modes</strong></td>
  <td>Infection, Multi-Infection, Nemesis, Survivor, Sniper, Swarm, Plague, Assassin, Hero, Assassin vs Sniper</td>
</tr>
<tr>
  <td>🧟 <strong>Zombie Classes</strong></td>
  <td>Classic Zombie, Raptor, Tight Zombie, Mutant, Predator Blue, Regenerator — each with unique HP / Speed / Gravity</td>
</tr>
<tr>
  <td>👑 <strong>Special Roles</strong></td>
  <td>Nemesis, Assassin, Mother Zombie, Survivor, Sniper, Hero — buy-able mid-round via the shop</td>
</tr>
<tr>
  <td>📊 <strong>Permanent Status HUD</strong></td>
  <td>Live center display showing round type, player class, and Ammo Packs balance — always visible</td>
</tr>
<tr>
  <td>🛒 <strong>Extra Items Shop</strong></td>
  <td>Armor, Grenades, Jetpack, Laser Mines, SCBA Suit, Revive Token, Multi-Jump, Knife Blink, and more</td>
</tr>
<tr>
  <td>💰 <strong>Ammo Pack Economy</strong></td>
  <td>Earn AP from damage, kills, and survival — balances persist across maps and restarts</td>
</tr>
<tr>
  <td>💣 <strong>Laser Trip Mines</strong></td>
  <td>Plant beam traps or explosive mines using the <code>!mine</code> menu</td>
</tr>
<tr>
  <td>🚀 <strong>Jetpack</strong></td>
  <td>Hold CTRL + SPACE to fly; fuel depletes over time and resets each round</td>
</tr>
<tr>
  <td>⚡ <strong>Knockback System</strong></td>
  <td>Configurable knockback per hit location, with separate multipliers for hero players</td>
</tr>
<tr>
  <td>🌑 <strong>Dark Atmosphere</strong></td>
  <td>Per-server fog and tonemap darkness applied automatically on every map load</td>
</tr>
<tr>
  <td>🔊 <strong>Vox / Sound System</strong></td>
  <td>Countdown voices, mode announcements, win sounds, and looping ambient music</td>
</tr>
<tr>
  <td>🔌 <strong>Full Plugin API</strong></td>
  <td><code>IZombieOutstandingAPI</code> — hook events, query player state, and set roles from external plugins</td>
</tr>
</table>

---

## 📦 Dependencies & Setup

> **Both dependencies are required.** The plugin will not work without them.

| Dependency | Link |
|------------|------|
| **SwiftlyS2** (framework) | [swiftly-solution/swiftlys2](https://github.com/swiftly-solution/swiftlys2) |
| **Economy plugin** (AP storage) | [SwiftlyS2-Plugins/Economy](https://github.com/SwiftlyS2-Plugins/Economy) |

**Why Economy?**  
Ammo Packs are stored exclusively through the Economy plugin — no MySQL or database setup required. Balances survive reconnects, map changes, and server restarts, and can be shared with other Economy-compatible plugins.

**Economy quick-start:**
1. Install the Economy plugin and follow its own README.
2. The wallet kind `"ammo"` is registered automatically on first startup (name is set by `EconomyWalletKind` in `ZombieOutstandingCFG.jsonc`).

---

## 🎨 Workshop Assets

| Asset | Workshop ID |
|-------|-------------|
| 🔊 Sounds + Models + Lasemine | [3678630823](https://steamcommunity.com/sharedfiles/filedetails/?id=3678630823) |

---

## 🚀 Installation

**Step-by-step:**

```
1. Install SwiftlyS2 on your CS2 server.
2. Install the Economy plugin.
3. Copy the plugin folder:
       addons/swiftlys2/plugins/ZombieOutstandingCS2/
4. Subscribe to the Workshop assets above (optional but recommended).
5. Start or reload the server:  sw_reload
6. Edit configs in:
       configs/plugins/ZombieOutstandingCS2/
7. Check the server console for load errors.
```

**File layout:**

```
addons/swiftlys2/plugins/
└── ZombieOutstandingCS2/
    └── ZombieOutstandingCS2.dll

configs/plugins/ZombieOutstandingCS2/
├── ZombieOutstandingCFG.jsonc   ← Core settings, game modes, special classes, weapons, vox, mines
├── ZombieClassesCFG.jsonc       ← Zombie class stats & sounds
└── ExtraItemsCFG.jsonc          ← Shop items, prices, and AP reward rates

translations/
└── en.jsonc                     ← English strings
```

---

## 💬 Commands

### Player Commands

| Command | Chat Shortcut | Description |
|---------|--------------|-------------|
| `sw_zmenu` | `!zmenu` | Open the main game menu |
| `sw_zextra` | `!zextra` | Open the Extra Items shop |
| `sw_buyweapons` | `!buyweapons` | Buy weapons before infection starts |
| `sw_zclass` | `!zclass` | Choose your zombie class preference |
| `sw_mine` | `!mine` | Open the laser mine placement menu |

> All command names are configurable in `ZombieOutstandingCFG.jsonc`.

### Admin Commands

| Command | Permission | Description |
|---------|-----------|-------------|
| `sw_zadmin` | `hzp.adminmenu` | Admin action menu (infect, respawn, set roles, etc.) |

> Set `AdminMenuPermission` to `""` in `ZombieOutstandingCFG.jsonc` to allow all players.

---

## 🗺️ Game Modes

All modes are configured in `ZombieOutstandingCFG.jsonc`. Every mode supports `Enable`, `Weight` (random chance), `ZombieCanReborn`, and `EnableInfiniteClipMode`.

| # | Mode | Description |
|---|------|-------------|
| 1 | 🧟 **Normal Infection** | 1 Mother Zombie chosen — infects the rest |
| 2 | 🧟🧟 **Multi Infection** | Multiple Mother Zombies at once |
| 3 | 💀 **Nemesis** | 1 ultra-powerful Nemesis — no regular infection |
| 4 | 🏹 **Survivor** | 1 human Survivor (XM1014) vs all zombies |
| 5 | 🎯 **Sniper** | 1 human Sniper (AWP, one-shot) vs all zombies |
| 6 | 🌊 **Swarm** | Half the server becomes zombies instantly |
| 7 | ☠️ **Plague** | Half zombies + 1 Nemesis + 1 Survivor simultaneously |
| 8 | 🥷 **Assassin** | 1 near-invisible Assassin zombie — no regular infection |
| 9 | 🦸 **Hero** | Last humans alive become Heroes with extreme stats |
| 10 | ⚔️ **Assassin vs Sniper** | Assassin zombie faces a Sniper human 1-on-1 |

> Use `NormalRoundsInterval` in `ZombieOutstandingCFG.jsonc` to enforce a minimum number of normal rounds between special modes.

---

## 🧟 Zombie Classes

Defined in `ZombieClassesCFG.jsonc`. Stats are based on the original **Zombie Outstanding v7.1** class balance.

| Class | HP | Speed | Gravity | Notes |
|-------|----|-------|---------|-------|
| 🧟 **Classic Zombie** | 6 000 | 1.16× | 0.60 | Balanced all-rounder |
| 🦅 **Raptor** | 4 800 | 1.22× | 1.00 | Fastest zombie |
| 🔒 **Tight Zombie** | 7 500 | 0.88× | 0.80 | Tanky, double-jump ability |
| 👾 **Mutant** | 6 250 | 0.98× | 1.00 | Slightly above average HP |
| 💙 **Predator Blue** | 5 600 | 1.12× | 0.80 | Powerful attacker |
| 💉 **Regenerator** | 4 750 | 1.00× | 1.00 | Regenerates 350 HP every 5 s |

> Speed is a multiplier relative to the default CS2 walk speed (250 u/s).

---

## 👑 Special Classes

Configured under `ZOSpecialClassCFG` (models/sounds/regen) and each mode section (HP override).

| Class | HP | Speed | Gravity | Damage | Used In |
|-------|----|-------|---------|--------|---------|
| 🧟 **Mother Zombie** | 15 000 | 1.16× | 0.60 | 150 | Normal / Multi Infection |
| 💀 **Nemesis** | 75 000 | 1.00× | 0.50 | 250 | Nemesis / Plague |
| 🥷 **Assassin** | 24 000 | 3.50× | 0.50 | 357 | Assassin / AVS |

> Set `NemesisHealth` / `AssassinHealth` to `0` in `ZombieOutstandingCFG.jsonc` to fall back to the raw class HP from `ZOSpecialClassCFG`.

---

## 🛒 Extra Items Shop

Open with `!zextra` or via the main menu (`!zmenu`). Everything is purchased with **Ammo Packs (AP)**.

### Human Items

| Item | Price | Description |
|------|-------|-------------|
| 🛡️ **Armor** | 3 AP | 100 armor points |
| 💥 **HE Grenade** | 2 AP | Standard explosive |
| ⚡ **Flash Grenade** | 2 AP | Flashbang / light effect |
| ❄️ **Freeze Grenade** | 2 AP | Freezes zombies in blast radius |
| 🔥 **Incendiary Bomb** | 4 AP | Area fire damage |
| 🌀 **Teleport Grenade** | 3 AP | Teleports the thrower on detonation |
| 🧪 **SCBA Suit** | 5 AP | Blocks one zombie infection |
| 🦘 **Multi-Jump** | 4 AP | +1 extra jump (stackable) |
| 🗡️ **Knife Blink** | 5 AP | 3 charges — blink forward on each knife swing |
| 🚀 **Jetpack** | 10 AP | CTRL+SPACE to fly; fuel resets each round |
| 💣 **Laser Mine** | 6 AP | Opens mine menu — Tripwire or Explosive |
| ❤️ **Revive Token** | 8 AP | Auto-respawn once if you die |
| 🏹 **Become Survivor** | 20 AP | Transform into Survivor mid-round |
| 🎯 **Become Sniper** | 15 AP | Transform into Sniper mid-round |

### Zombie Items

| Item | Price | Description |
|------|-------|-------------|
| 💊 **Antidote** | 8 AP | Revert back to human |
| 🛡️ **Zombie Madness** | 6 AP | Temporary invulnerability (10 s) |
| 🧬 **T-Virus Grenade** | 6 AP | Infect nearby humans on detonation |
| 💀 **Become Nemesis** | 20 AP | Transform into Nemesis mid-round |
| 🥷 **Become Assassin** | 15 AP | Transform into Assassin mid-round |

> Role-buy items are disabled by default. Enable them with `"Enable": true` in `ExtraItemsCFG.jsonc`.  
> Players who already hold a special role cannot purchase another.

### Jetpack Details

- Hold **CTRL + SPACE** to activate thrust (consumes fuel).
- Horizontal velocity is preserved — only vertical force is applied.
- Configure in `ExtraItemsCFG.jsonc`: `JetpackMaxFuel`, `JetpackThrustForce`, `JetpackFuelConsumeRate`.

### Laser Trip Mines

| Type | Price | Behavior | Limit |
|------|-------|----------|-------|
| 💚 **Laser Tripwire** | 6 AP | Continuous beam damage (10 dmg per 0.1 s tick) | 2 per player |
| 🔴 **Explosive Mine** | 10 AP | Explodes on beam cross (radius 360 u, up to 2 600 dmg) | 2 per player |

> Plant with `!mine` after purchasing. Visuals, colors, sounds, and limits are fully configurable in `ZombieOutstandingCFG.jsonc` (`ZOMineCFG` section).

---

## 💣 Grenades & Weapons

All grenades are configured in `ZombieOutstandingCFG.jsonc`.

| Grenade | Enable Key | Auto-Give Key | Range | Duration | Effect |
|---------|-----------|--------------|-------|----------|--------|
| 🔥 Incendiary | `FireGrenade` | `SpawnGiveFireGrenade` | 300 u | 5 s | 500 burst + 10/s burn damage |
| ⚡ Light / Flash | `LightGrenade` | `SpawnGiveLightGrenade` | 1 000 u | 30 s | Blind / illumination |
| ❄️ Freeze | `FreezeGrenade` | `SpawnGiveFreezeGrenade` | 300 u | 10 s | Immobilizes target |
| 🌀 Teleport | `TelportGrenade` | `SpawnGiveTelportGrenade` | — | — | Teleports thrower |
| 💣 Incendiary Bomb | — | `SpawnGiveIncGrenade` | — | — | Fire area denial |
| 🧬 T-Virus (Zombie) | — | — | 300 u | — | Infects humans in radius |

---

## 💰 Ammo Packs & Rewards

Ammo Packs (AP) are the in-game currency for the Extra Items shop. All balances are managed by the **Economy plugin** — no reconnect loss, no manual saves.

### Earning AP

| Source | Default Amount | Config Key |
|--------|---------------|-----------|
| Survive the round as human | +3 | `RoundSurviveReward` |
| Infect / kill a human (as zombie) | +2 | `ZombieKillReward` |
| Deal N cumulative damage to zombies | +1 per threshold | `HumanDamageRewardThreshold` / `HumanDamageReward` |

> Damage rewards stack: deal 2× the threshold in one round → earn 2× the reward.

---

## ⚙️ Configuration Reference

<details>
<summary><strong>ZombieOutstandingCFG.jsonc — Core settings</strong></summary>

```jsonc
{
  "ZOMainCFG": {
    "RoundReadyTime": 22.0,        // Seconds before Mother Zombie appears
    "RoundTime": 4.0,              // Round duration in minutes

    "HumanMaxHealth": 150,
    "HumanInitialSpeed": 1.0,
    "HumanInitialGravity": 1.0,
    "KnockZombieForce": 250.0,
    "StunZombieTime": 0.1,

    "EnableDamageHud": true,       // Show damage dealt in center screen
    "EnableStatusHud": true,       // Permanent HUD: round type / class / AP

    "FireGrenade": true,           "SpawnGiveFireGrenade": true,
    "LightGrenade": true,          "SpawnGiveLightGrenade": true,
    "FreezeGrenade": true,         "SpawnGiveFreezeGrenade": true,
    "TelportGrenade": true,        "SpawnGiveTelportGrenade": false,

    "CanUseScbaSuit": true,
    "TVirusCanInfectHero": true,

    "MainMenuCommand":     "sw_zmenu",
    "ExtraItemsCommand":   "sw_zextra",
    "ZombieClassCommand":  "sw_zclass",
    "AdminMenuItemCommand":"sw_zadmin",
    "BuyWeaponsCommand":   "sw_buyweapons",
    "MineMenuCommand":     "sw_mine",

    "AdminMenuPermission": "hzp.adminmenu",  // Empty = allow everyone
    "ChatPrefix":          "[red][INFO][default]",
    "EconomyWalletKind":   "ammo",

    "NormalRoundsInterval": 0      // Min normal rounds between special modes (0 = disabled)
  }
}
```

</details>

<details>
<summary><strong>ZombieOutstandingCFG.jsonc — Special class HP overrides</strong></summary>

```jsonc
"Nemesis":  { "NemesisHealth":  75000 },  // ~60 s TTK for 5 players, ~29 s for 10
"Survivor": { "SurvivorHealth":  8000 },  // Durable but killable by coordinated zombies
"Sniper":   { "SniperHealth":    5000 },  // Glass-cannon; dies fast if zombies close in
"Assassin": { "AssassinHealth": 24000 }   // ~45 s TTK for 2 focused players
```

> Set any value to `0` to use the raw HP from `ZOSpecialClassCFG` instead.

</details>

<details>
<summary><strong>ZombieOutstandingCFG.jsonc — Laser mines (ZOMineCFG)</strong></summary>

```jsonc
"ZOMineCFG": {
  "MineList": [
    {
      "Name": "Laser Tripwire",
      "CanExplorer": false,
      "Price": 6,  "Limit": 2,  "Team": "ct",
      "LaserRate": 0.1,  "LaserDamage": 10.0,  "LaserKnockBack": 100.0
    },
    {
      "Name": "Explosive Mine",
      "CanExplorer": true,
      "Price": 10,  "Limit": 2,  "Team": "ct",
      "ExplorerRadius": 360,  "ExplorerDamage": 2600
    }
  ]
}
```

</details>

<details>
<summary><strong>ExtraItemsCFG.jsonc — Shop items & AP rewards</strong></summary>

```jsonc
"ZOExtraItemsCFG": {
  "RoundSurviveReward": 3,
  "ZombieKillReward": 2,
  "HumanDamageRewardThreshold": 600,
  "HumanDamageReward": 1,

  "Items": [
    { "Key": "armor",            "Price": 3,  "Team": "Human"  },
    { "Key": "he_grenade",       "Price": 2,  "Team": "Human"  },
    { "Key": "flash_grenade",    "Price": 2,  "Team": "Human"  },
    { "Key": "smoke_grenade",    "Price": 2,  "Team": "Human"  },
    { "Key": "inc_grenade",      "Price": 4,  "Team": "Human"  },
    { "Key": "teleport_grenade", "Price": 3,  "Team": "Human"  },
    { "Key": "scba_suit",        "Price": 5,  "Team": "Human"  },
    { "Key": "multijump",        "Price": 4,  "Team": "Human"  },
    { "Key": "knife_blink",      "Price": 5,  "Team": "Human"  },
    { "Key": "jetpack",          "Price": 10, "Team": "Human"  },
    { "Key": "laser_mine",       "Price": 6,  "Team": "Human"  },
    { "Key": "revive_token",     "Price": 8,  "Team": "Human"  },
    { "Key": "buy_survivor",     "Price": 20, "Team": "Human",  "Enable": true },
    { "Key": "buy_sniper",       "Price": 15, "Team": "Human",  "Enable": true },
    { "Key": "antidote",         "Price": 8,  "Team": "Zombie" },
    { "Key": "zombie_madness",   "Price": 6,  "Team": "Zombie" },
    { "Key": "t_virus_grenade",  "Price": 6,  "Team": "Zombie" },
    { "Key": "buy_nemesis",      "Price": 20, "Team": "Zombie", "Enable": true },
    { "Key": "buy_assassin",     "Price": 15, "Team": "Zombie", "Enable": true }
  ]
}
```

</details>

<details>
<summary><strong>ZombieClassesCFG.jsonc — Class schema example</strong></summary>

```jsonc
"ZOZombieClassCFG": {
  "ZombieClassList": [
    {
      "Name": "Classic Zombie",
      "Enable": true,
      "Stats": {
        "Health": 6000,  "MotherZombieHealth": 15000,
        "Speed": 1.16,   "Damage": 60.0,  "Gravity": 0.6,
        "Fov": 110,  "EnableRegen": true,  "HpRegenSec": 5.0,  "HpRegenHp": 100
      },
      "Models": { "ModelPath": "characters/models/..." },
      "Sounds": { "SoundInfect": "han.human.mandeath", "SoundPain": "han.hl.zombie.pain" }
    }
    // ... more classes
  ]
}
```

</details>

---

## 🌐 Translations

Translation files live in the `translations/` folder (`en.jsonc` is bundled). Copy additional language files to the same folder.

| Key | Default (EN) |
|-----|-------------|
| `ServerGameHumanWin` | `Humans WIN !!!` |
| `ServerGameZombieWin` | `Zombies WIN !!!` |
| `RoundStartAnnounce` | `New round begins! \| Your credits: {0} \| Players: {1}` |
| `APRoundSurviveReward` | `You survived the round! +{0} Ammo Packs (total: {1}).` |
| `APZombieKillReward` | `You infected a human! +{0} Ammo Packs (total: {1}).` |
| `APHumanDamageReward` | `Damage bonus! +{0} Ammo Packs (total: {1}).` |
| `ExtraItemsMenuAP` | `Your Ammo Packs: {0}` |

---

## 🔌 API

`IZombieOutstandingAPI` is exposed as a SwiftlyS2 shared interface so external plugins can integrate with the gamemode.

**Registering the API:**

```csharp
public override void UseSharedInterface(IInterfaceManager interfaceManager)
{
    if (interfaceManager.HasSharedInterface("ZombieOutstanding"))
    {
        var api = interfaceManager.GetSharedInterface<IZombieOutstandingAPI>("ZombieOutstanding");
        // hook events, query state, perform actions...
    }
}
```

**Capabilities:**

| Category | Examples |
|----------|---------|
| **Events** | `ZO_OnPlayerInfect`, `ZO_OnNemesisSelected`, `ZO_OnGameStart`, `ZO_OnHumanWin`, `ZO_OnZombieWin` |
| **Queries** | `IsZombie()`, `IsNemesis()`, `IsAssassin()`, `IsSurvivor()`, `CurrentMode` |
| **Actions** | Force roles, give/take Ammo Packs, set glow / FOV / god mode |

Full interface: [`src/IZombieOutstandingAPI/IZombieOutstandingAPI.cs`](src/IZombieOutstandingAPI/IZombieOutstandingAPI.cs)

---

<div align="center">

Remade with ❤️ — based on the original plugin by [H-AN / Zombie PlagueS2](https://github.com/H-AN/HanZombiePlagueS2)

</div>

