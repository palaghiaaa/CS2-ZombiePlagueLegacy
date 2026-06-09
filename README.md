<div align="center">

<img width="600" height="131" alt="Zombie Plague: Legacy CS2" src="https://github.com/user-attachments/assets/d0316faa-c2d0-478f-a642-1e3c3651f1d4" />

<h3>A full-featured Zombie Plague gamemode for Counter-Strike 2</h3>

<p>
  Built on <strong>SwiftlyS2</strong> &nbsp;·&nbsp;
  Faithful port of the classic <strong>TheZombieApocalypse CS 1.6</strong> addon &nbsp;·&nbsp;
  Ammo Packs via the <strong>Economy</strong> plugin
</p>

<a href="https://www.youtube.com/watch?v=DVeR5u28M_s">▶ Video Preview</a>
&nbsp;·&nbsp;
<a href="CHANGELOG.md">📋 Changelog</a>
&nbsp;·&nbsp;
<a href="LICENSE">GPL-3.0 License</a>

<br/><br/>

[![Framework](https://img.shields.io/badge/Framework-SwiftlyS2%201.4-orange?style=for-the-badge)](https://github.com/swiftly-solution/swiftlys2)
[![Economy](https://img.shields.io/badge/Requires-Economy_Plugin-brightgreen?style=for-the-badge)](https://github.com/SwiftlyS2-Plugins/Economy)
[![Cookies](https://img.shields.io/badge/Requires-Cookies_Plugin-blue?style=for-the-badge)](https://github.com/SwiftlyS2-Plugins/Cookies)
[![License](https://img.shields.io/badge/License-GPL_v3-lightgrey?style=for-the-badge)](LICENSE)

</div>

---

## 📋 Table of Contents

1. [Overview](#-overview)
2. [Plugin Suite](#-plugin-suite)
3. [Requirements](#-requirements)
4. [Installation](#-installation)
5. [Commands](#-commands)
6. [Game Modes](#-game-modes)
7. [Zombie Classes](#-zombie-classes)
8. [Special Classes](#-special-classes)
9. [Extra Items Shop](#-extra-items-shop)
10. [Grenades](#-grenades)
11. [Ammo Packs & Rewards](#-ammo-packs--rewards)
12. [Configuration Reference](#️-configuration-reference)
13. [API](#-api)

---

## 🎮 Overview

**ZombiePlagueLegacyCS2** is a complete Zombie Plague gamemode for CS2, ported from the classic **TheZombieApocalypse CS 1.6** addon. It preserves the original gameplay feel — including class abilities, balance, and mechanics — while being fully rebuilt for the CS2 / SwiftlyS2 architecture.

The project ships as a **suite of 9 plugins** that work together out of the box, each independently loadable.

---

## 📦 Plugin Suite

| Plugin | Description |
|--------|-------------|
| **ZombiePlagueLegacyCS2** | Core gamemode — infection, rounds, classes, shop, grenades, mines |
| **ZPLRank** | In-session kill/death ranking with `!rank`, `!top10`, `!top15` menus |
| **ZPLVIP** | VIP perks — armor, multi-jump, damage boost, AP rewards, Happy Hour |
| **ZPLTags** | Score and chat tags per zombie class, with Cookies persistence |
| **ZPLTeamBets** | Bet Ammo Packs on Humans or Zombies winning the round |
| **ZPLMegaEvents** | Special-class selection hooks and mega-event triggers |
| **ZPLFlashlight** | Flashlight toggle for humans |
| **ZPLDarkFog** | Per-server atmospheric fog and tonemap on every map load |
| **IZombiePlagueLegacyAPI** | Shared contracts DLL for external plugin integration |

---

## 📋 Requirements

> All three dependencies are **required**. The plugin will not start without them.

| Dependency | Purpose | Link |
|------------|---------|------|
| **SwiftlyS2** ≥ 1.4.0-beta.38 | CS2 plugin framework | [swiftly-solution/swiftlys2](https://github.com/swiftly-solution/swiftlys2) |
| **Economy plugin** | Persistent Ammo Pack storage (no DB config needed) | [SwiftlyS2-Plugins/Economy](https://github.com/SwiftlyS2-Plugins/Economy) |
| **Cookies plugin** | Cross-session preference persistence (zombie class, tags) | [SwiftlyS2-Plugins/Cookies](https://github.com/SwiftlyS2-Plugins/Cookies) |

**Cookies setup** — add a `"cookies"` entry to SwiftlyS2's `configs/database.jsonc`:

```jsonc
{
  "mysql": { "host": "127.0.0.1", "port": 3306, "user": "...", "password": "...", "database": "swiftly" },
  "cookies": { "host": "127.0.0.1", "port": 3306, "user": "...", "password": "...", "database": "swiftly" }
}
```

---

## 🔧 Installation

### Workshop Assets

Download and mount the following Workshop items before starting the server:

| Asset | Workshop ID |
|-------|-------------|
| Sounds + Models | `3644652779` |
| Sounds + Models (extra) | `3718944950` |
| Sounds + Models (extra) | `3618032051` |
| Laser Mine models | `3626771819` |

### Steps

1. Install **SwiftlyS2**, **Economy**, and **Cookies** plugins.
2. Copy all plugin `.dll` files to `addons/swiftlys2/plugins/`.
3. Copy the `configs/` folder to `addons/swiftlys2/configs/`.
4. Copy the `translations/` folder to each plugin's `resources/translations/`.
5. Subscribe to all four Workshop items above and add them to your `gameinfo.gi`.
6. Add the `"cookies"` database entry (see Requirements above).
7. Start the server — the `"ammo"` Economy wallet is created automatically on first load.

### Build from Source

```bash
dotnet publish -c Release -r linux-x64
```

Requires **.NET 10 SDK** and internet access (to restore SwiftlyS2 NuGet packages).

---

## 💬 Commands

### Player Commands

| Command | Chat Alias | Description |
|---------|-----------|-------------|
| `sw_zmenu` | `!zmenu` | Open the main game menu |
| `sw_zextra` | `!zextra` | Open the Extra Items shop |
| `sw_zclass` | `!zclass` | Choose your zombie class preference |
| `sw_buyweapons` | `!buyweapons` | Buy weapons (available before infection starts) |
| `sw_mine` | `!mine` | Open the laser mine placement menu |
| `sw_rank` | `!rank` | Show your current rank and K/D |
| `sw_top` | `!top` / `!top10` / `!top15` | View the top players leaderboard |
| `sw_vip` | `!vip` | View your VIP perks |
| `sw_vips` | `!vips` | List online VIP players |
| `sw_bet` | `!bet` | Place a bet on the round outcome |

### Admin Commands

| Command | Permission | Description |
|---------|-----------|-------------|
| `sw_zadmin` | `hzp.adminmenu` | Admin menu — infect, respawn, set roles, force modes |

> Set `AdminMenuPermission` to `""` in `ZombiePlagueLegacyCFG.jsonc` to open the admin menu to all players.

---

## 🗺️ Game Modes

All modes are configured in `ZombiePlagueLegacyCFG.jsonc` with `Enable`, `Weight` (random selection weight), and `MinPlayers`.

| # | Mode | Min Players | Description |
|---|------|------------|-------------|
| 1 | 🧟 **Normal Infection** | 2 | 1 Mother Zombie is selected — spreads infection to all remaining humans |
| 2 | 🧟🧟 **Multi Infection** | 10 | Multiple Mother Zombies are chosen simultaneously |
| 3 | 💀 **Nemesis** | 10 | 1 ultra-powerful Nemesis (75 000 HP) — no standard infection |
| 4 | 🛡️ **Survivor** | 10 | 1 human Survivor (XM1014, 8 000 HP) faces an entire zombie team |
| 5 | 🎯 **Sniper** | 10 | 1 human Sniper (AWP, 5 000 HP, one-shot kills) vs all zombies |
| 6 | 🌊 **Swarm** | 10 | Half the server becomes zombies instantly at round start |
| 7 | ☠️ **Plague** | 15 | Half zombies + 1 Nemesis + 1 Survivor at the same time |
| 8 | 🥷 **Assassin** | 10 | 1 near-invisible Assassin zombie (24 000 HP) — no standard infection |
| 9 | 🦸 **Hero** | 10 | The last humans alive ascend to Hero status with extreme stats |
| 10 | ⚔️ **Assassin vs Sniper** | 10 | Pure 1v1: Assassin zombie vs a single Sniper human |

> `NormalRoundsInterval: 5` — at least 5 normal rounds must pass between special modes.

---

## 🧟 Zombie Classes

Stats and abilities are ported directly from **TheZombieApocalypse CS 1.6** (speed converted: CS16 units ÷ 250).

| Class | HP | Speed | Gravity | Damage | Regen | Special Ability |
|-------|----|-------|---------|--------|-------|----------------|
| 🧟 **Classic Zombie** | 6 000 | 1.16× | 0.60 | 60 | — | Balanced all-rounder |
| 🦅 **Raptor** | 4 800 | 1.22× | 1.00 | 55 | — | **+200 HP** on every infect |
| 🔒 **Tight Zombie** | 7 500 | 0.88× | 0.80 | 70 | — | **Double Jump** (1 extra mid-air jump) |
| 👾 **Mutant** | 6 250 | 0.98× | 1.00 | 80 | — | **Yellow glow** for 3 s on infect |
| 💙 **Predator Blue** | 5 600 | 1.12× | 0.80 | 70 | — | **Blue glow** for 3 s on infect |
| 💉 **Regenerator** | 4 750 | 1.00× | 1.00 | 50 | **350 HP / 5 s** | — |

> Only **Regenerator** has HP regeneration — exactly as in the original CS 1.6 addon.  
> Speed is a multiplier relative to the default CS2 walk speed (250 u/s).

### Ability Details

- **Raptor** — Each successful infection grants the infector +200 HP (capped at max HP).
- **Tight Zombie** — Can perform one extra jump while airborne. Jumps reset automatically on landing.
- **Mutant** — On each infection, the infector glows yellow (`255,215,0`) for 3 seconds.
- **Predator Blue** — On each infection, the infector glows blue (`10,10,255`) for 3 seconds.
- **Regenerator** — Passively regenerates 350 HP every 5 seconds.

> All values are configurable per class via the `Abilities` block in `ZombieClassesCFG.jsonc`.

---

## 👑 Special Classes

| Class | HP | Speed | Gravity | Damage | Appears In |
|-------|----|-------|---------|--------|-----------|
| 🧟 **Mother Zombie** | Scales with player count | 1.16× | 0.60 | 150 | Normal / Multi Infection |
| 💀 **Nemesis** | 75 000 | 1.00× | 0.50 | 250 | Nemesis / Plague |
| 🥷 **Assassin** | 24 000 | 3.50× | 0.50 | 357 | Assassin / AVS |
| 🛡️ **Survivor** | 8 000 | 1.00× | 1.00 | — | Survivor / Plague |
| 🎯 **Sniper** | 5 000 | 1.00× | 1.00 | One-shot | Sniper / AVS |
| 🦸 **Hero** | Configurable | 1.20× | 0.80 | — | Hero |

**Mother Zombie HP scaling** — HP interpolates between `MotherZombieHPMinMultiplier` (low player count) and `MotherZombieHPMultiplier` (≥ `MotherZombieHPMaxPlayers`). Prevents unfair HP values on low-population servers. Disable with `MotherZombieHPPlayerScaleEnabled: false`.

> Set any HP value to `0` in `ZombiePlagueLegacyCFG.jsonc` to fall back to the raw value from `ZPLSpecialClassCFG`.

---

## 🛒 Extra Items Shop

Open with `!zextra` or through the main menu (`!zmenu`). All items are purchased with **Ammo Packs (AP)**.

### 🔵 Human Items

| Item | Price | Description |
|------|------:|-------------|
| 🛡️ Armor | 3 AP | +100 armor |
| 💥 HE Grenade | 2 AP | Standard explosive |
| ⚡ Flash Grenade | 2 AP | Flashbang / light effect |
| 💨 Smoke Grenade | 2 AP | Smoke screen |
| 🔥 Incendiary Bomb | 4 AP | Fire area-denial |
| 🌀 Teleport Grenade | 3 AP | Teleports the thrower on detonation |
| 🧪 SCBA Suit | 5 AP | Absorbs one zombie infection |
| 🦘 Multi-Jump | 4 AP | +1 extra mid-air jump (stackable) |
| 🗡️ Knife Blink | 5 AP | 3 charges — knife swing teleports you forward; stops at walls |
| 🚀 Jetpack | 10 AP | Hold **CTRL + SPACE** to fly; WASD + eye direction for thrust |
| 💣 Laser Mine | 6 AP | Opens mine menu (Tripwire or Explosive) |
| ❤️ Revive Token | 8 AP | Auto-respawns you once if you die |
| ⚡ Tryder | 15 AP | +1 000 HP, +500 armor, infinite clip, blue glow |
| ♾️ Unlimited Clip | 8 AP | Infinite magazine for your current weapon |
| 🎯 No Recoil | 6 AP | Zero weapon spread |
| 🏹 Become Survivor | 20 AP | Transform into Survivor mid-round *(disabled by default)* |
| 🎯 Become Sniper | 15 AP | Transform into Sniper mid-round *(disabled by default)* |

### 🔴 Zombie Items

| Item | Price | Limit | Description |
|------|------:|------:|-------------|
| 💊 Antidote | 8 AP | 3/round | Revert back to human |
| 🛡️ Zombie Madness | 6 AP | 5/round | 10 s invulnerability + red glow |
| 🧬 T-Virus Grenade | 6 AP | 3/round | Infects nearby humans on detonation |
| 💀 Become Nemesis | 20 AP | — | Transform into Nemesis mid-round *(disabled by default)* |
| 🥷 Become Assassin | 15 AP | — | Transform into Assassin mid-round *(disabled by default)* |

### 💣 Laser Mines

| Type | Price | Limit | Behavior |
|------|------:|------:|----------|
| 💚 Laser Tripwire | 6 AP | 2/player | Continuous beam damage (10 dmg / 0.1 s tick) |
| 🔴 Explosive Mine | 10 AP | 2/player | Explodes when beam is crossed (360 u radius, up to 2 600 dmg) |

**Mine HP system** — set `MineHealth > 0` to make mines destroyable by zombie knife melee. The mine owner sees a live center-screen HP readout (`Mine HP: X / max`). At 0 HP the mine detonates.

---

## 💣 Grenades

All grenades are configured in `ZombiePlagueLegacyCFG.jsonc` and can be toggled or auto-given on spawn.

| Grenade | Range | Duration | Effect |
|---------|------:|--------:|--------|
| 🔥 Incendiary | 300 u | 5 s | 500 burst + 10/s burn damage |
| ⚡ Light / Flash | 1 000 u | 30 s | Blind or illuminate area |
| ❄️ Freeze | 300 u | 10 s | Immobilizes zombies in radius |
| 🌀 Teleport | — | — | Teleports the thrower on detonation |
| 🧬 T-Virus (zombie) | 300 u | — | Infects all humans in radius |

---

## 💰 Ammo Packs & Rewards

Ammo Packs are the in-game currency for the Extra Items shop. Balances are managed by the **Economy plugin** and persist across map changes and server restarts.

| Source | Amount | Config Key |
|--------|-------:|-----------|
| Survive the round as human | +3 AP | `RoundSurviveReward` |
| Infect / kill a human (as zombie) | +2 AP | `ZombieKillReward` |
| Deal 600 cumulative damage to zombies | +1 AP | `HumanDamageRewardThreshold` / `HumanDamageReward` |

> Damage rewards stack — deal 1 200 damage in one round to earn +2 AP.

---

## ⚙️ Configuration Reference

<details>
<summary><strong>ZombiePlagueLegacyCFG.jsonc — Core settings</strong></summary>

```jsonc
{
  "ZPLMainCFG": {
    // ── Round timing ────────────────────────────────────────────────────────
    "RoundReadyTime": 22.0,         // Seconds before Mother Zombie is selected
    "RoundTime": 4.0,               // Round duration in minutes
    "MinPlayersForInfection": 2,    // Minimum players required to trigger infection

    // ── Human base stats ────────────────────────────────────────────────────
    "HumanMaxHealth": 150,
    "HumanInitialSpeed": 1.0,
    "HumanInitialGravity": 1.0,

    // ── Knockback ───────────────────────────────────────────────────────────
    "KnockZombieForce": 200.0,
    "StunZombieTime": 0.1,
    "HumanKnockBackHeadMultiply": 2.0,
    "HumanKnockBackBodyMultiply": 1.0,
    "HumanKnockBackGroundMultiply": 1.0,
    "HumanKnockBackAirMultiply": 0.5,
    "HumanHeroKnockBackMultiply": 1.0,

    // ── HUD ─────────────────────────────────────────────────────────────────
    "EnableDamageHud": true,        // Center-screen damage info on hit
    "EnableStatusHud": true,        // Permanent HUD: mode / class / AP

    // ── Grenades (enable + auto-give on spawn) ───────────────────────────────
    "FireGrenade": true,            "SpawnGiveFireGrenade": true,
    "LightGrenade": true,           "SpawnGiveLightGrenade": true,
    "FreezeGrenade": true,          "SpawnGiveFreezeGrenade": true,
    "TelportGrenade": true,         "SpawnGiveTelportGrenade": false,

    // ── Misc ────────────────────────────────────────────────────────────────
    "CanUseScbaSuit": true,
    "TVirusCanInfectHero": true,
    "NormalRoundsInterval": 5,      // Min normal rounds between special modes (0 = off)

    // ── Commands ────────────────────────────────────────────────────────────
    "MainMenuCommand":      "sw_zmenu",
    "ExtraItemsCommand":    "sw_zextra",
    "ZombieClassCommand":   "sw_zclass",
    "AdminMenuCommand":     "sw_zadmin",
    "BuyWeaponsCommand":    "sw_buyweapons",
    "MineMenuCommand":      "sw_mine",
    "AdminMenuPermission":  "hzp.adminmenu",  // "" = allow everyone

    // ── Economy ─────────────────────────────────────────────────────────────
    "ChatPrefix":         "[red][ZM][default]",
    "EconomyWalletKind":  "ammo"
  }
}
```

</details>

<details>
<summary><strong>ZombiePlagueLegacyCFG.jsonc — Special class HP</strong></summary>

```jsonc
"Nemesis":  { "NemesisHealth":  75000 },
"Survivor": { "SurvivorHealth":  8000 },
"Sniper":   { "SniperHealth":    5000 },
"Assassin": { "AssassinHealth": 24000 }
// Set any value to 0 to use the raw HP from ZPLSpecialClassCFG instead
```

</details>

<details>
<summary><strong>ZombieClassesCFG.jsonc — Class schema</strong></summary>

```jsonc
{
  "ZPLZombieClassCFG": {
    "ZombieClassList": [
      {
        "Name": "Classic Zombie",
        "Enable": true,
        "Stats": {
          "Health": 6000,  "MotherZombieHealth": 15000,
          "Speed": 1.16,   "Damage": 60.0,  "Gravity": 0.6,
          "Fov": 110,
          "EnableRegen": false,  "HpRegenSec": 5.0,  "HpRegenHp": 0
        },
        "Models": { "ModelPath": "characters/models/..." },
        "Sounds": { "SoundInfect": "han.human.mandeath", ... },
        "Abilities": {
          "InfectHealAmount": 0,        // Raptor: 200
          "ExtraJumps": 0,              // Tight Zombie: 1
          "InfectGlowColor": "",        // Mutant: "255,215,0,180" | Predator Blue: "10,10,255,180"
          "GlowDurationSeconds": 3.0,
          "SilentSteps": false
        }
      }
      // ...
    ]
  }
}
```

</details>

<details>
<summary><strong>ExtraItemsCFG.jsonc — AP rewards, shop prices & item settings</strong></summary>

```jsonc
{
  "ZPLExtraItemsCFG": {
    "RoundSurviveReward": 3,
    "ZombieKillReward": 2,
    "HumanDamageRewardThreshold": 600,
    "HumanDamageReward": 1,

    // Tryder
    "TryderHealth": 1000,  "TryderArmor": 500,
    "TryderGlowR": 0,  "TryderGlowG": 127,  "TryderGlowB": 255,

    // Zombie Madness
    "MadnessDuration": 10.0,
    "MadnessGlowR": 255,  "MadnessGlowG": 0,  "MadnessGlowB": 0,

    // Knife Blink
    "KnifeBlinkCharges": 3,
    "KnifeBlinkDistance": 300.0,
    "KnifeBlinkCooldown": 2.0,

    // Jetpack
    "JetpackMaxFuel": 250.0,
    "JetpackThrustForce": 350.0,
    "JetpackHorizontalForce": 300.0,
    "JetpackFuelConsumeRate": 30.0,

    "Items": [ ... ]
  }
}
```

</details>

<details>
<summary><strong>ZPLMineCFG — Laser mine configuration</strong></summary>

```jsonc
"ZPLMineCFG": {
  "MineList": [
    {
      "Name": "Laser Tripwire",
      "CanExplorer": false,
      "Price": 6,  "Limit": 2,  "Team": "ct",
      "LaserRate": 0.1,  "LaserDamage": 10.0,  "LaserKnockBack": 100.0,
      "GlowColor": "0,255,0,255",  "LaserColor": "0,0,255,255",
      "MineHealth": 500,           // HP for zombie melee; 0 = invincible
      "ZombieAttackDamage": 150
    },
    {
      "Name": "Explosive Mine",
      "CanExplorer": true,
      "Price": 10,  "Limit": 2,  "Team": "ct",
      "ExplorerRadius": 360,  "ExplorerDamage": 2600,
      "GlowColor": "255,0,0,255",  "LaserColor": "255,0,0,255",
      "MineHealth": 750,
      "ZombieAttackDamage": 150
    }
  ]
}
```

</details>

---

## 🔌 API

`IZombiePlagueLegacyAPI` is exposed as a SwiftlyS2 shared interface for external plugin integration.

```csharp
public override void UseSharedInterface(IInterfaceManager interfaceManager)
{
    if (interfaceManager.HasSharedInterface("ZombiePlagueLegacy"))
    {
        var api = interfaceManager.GetSharedInterface<IZombiePlagueLegacyAPI>("ZombiePlagueLegacy");
    }
}
```

| Category | Members |
|----------|---------|
| **State queries** | `IsZombie()`, `IsNemesis()`, `IsAssassin()`, `IsSurvivor()`, `IsSniper()`, `IsHero()`, `CurrentMode` |
| **Events** | `ZPL_OnPlayerInfect`, `ZPL_OnNemesisSelected`, `ZPL_OnGameStart`, `ZPL_OnHumanWin`, `ZPL_OnZombieWin` |
| **Actions** | Force roles, give/take AP, set glow / FOV / god mode / speed |

Full interface definition: [`src/IZombiePlagueLegacyAPI/IZombiePlagueLegacyAPI.cs`](src/IZombiePlagueLegacyAPI/IZombiePlagueLegacyAPI.cs)

---

<div align="center">

Remade with ❤️ based on the original work by [H-AN / HanZombiePlagueS2](https://github.com/H-AN/HanZombiePlagueS2) · Extended by [DeadPoolCS2](https://github.com/DeadPoolCS2)

</div>  <td>Hold CTRL + SPACE to fly with full omnidirectional thrust (WASD + eye yaw horizontal); fuel resets each round</td>
</tr>
<tr>
  <td>🗡️ <strong>Knife Blink</strong></td>
  <td>Swing knife to teleport forward; geometry trace stops the blink at walls — no more clipping out of the map</td>
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
  <td><code>IZombiePlagueLegacyAPI</code> — hook events, query player state, and set roles from external plugins</td>
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
2. The wallet kind `"ammo"` is registered automatically on first startup (name is set by `EconomyWalletKind` in `ZombiePlagueLegacyCFG.jsonc`).

---

## 🎨 Workshop Assets

| Asset | Workshop ID |
|-------|-------------|
| 🔊 Sounds + Models + Lasermine | [ 3644652779 + 3718944950 + 3618032051 + 3626771819 ] |

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

> All command names are configurable in `ZombiePlagueLegacyCFG.jsonc`.

### Admin Commands

| Command | Permission | Description |
|---------|-----------|-------------|
| `sw_zadmin` | `hzp.adminmenu` | Admin action menu (infect, respawn, set roles, etc.) |

> Set `AdminMenuPermission` to `""` in `ZombiePlagueLegacyCFG.jsonc` to allow all players.

---

## 🗺️ Game Modes

All modes are configured in `ZombiePlagueLegacyCFG.jsonc`. Every mode supports `Enable`, `Weight` (random chance), `ZombieCanReborn`, and `EnableInfiniteClipMode`.

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

> Use `NormalRoundsInterval` in `ZombiePlagueLegacyCFG.jsonc` to enforce a minimum number of normal rounds between special modes.

---

## 🧟 Zombie Classes

Defined in `ZombieClassesCFG.jsonc`. Stats are based on the original **Zombie Plague: Legacy v7.1** class balance.

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

Configured under `ZPLSpecialClassCFG` (models/sounds/regen) and each mode section (HP override).

| Class | HP | Speed | Gravity | Damage | Used In |
|-------|----|-------|---------|--------|---------|
| 🧟 **Mother Zombie** | 15 000 | 1.16× | 0.60 | 150 | Normal / Multi Infection |
| 💀 **Nemesis** | 75 000 | 1.00× | 0.50 | 250 | Nemesis / Plague |
| 🥷 **Assassin** | 24 000 | 3.50× | 0.50 | 357 | Assassin / AVS |

> Set `NemesisHealth` / `AssassinHealth` to `0` in `ZombiePlagueLegacyCFG.jsonc` to fall back to the raw class HP from `ZPLSpecialClassCFG`.

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
| 🗡️ **Knife Blink** | 5 AP | 3 charges — blink forward on knife swing; stops at walls |
| 🚀 **Jetpack** | 10 AP | CTRL+SPACE to fly with WASD directional thrust; fuel resets each round |
| 💣 **Laser Mine** | 6 AP | Opens mine menu — Tripwire or Explosive; optional HP lets zombies melee-attack mines |
| ❤️ **Revive Token** | 8 AP | Auto-respawn once if you die |
| 🔵 **Tryder** | 15 AP | Bonus HP + armor + infinite clip + blue glow |
| ♾️ **Unlimited Clip** | 8 AP | Infinite magazine |
| 🎯 **No Recoil** | 6 AP | Zero weapon spread |
| 🏹 **Become Survivor** | 20 AP | Transform into Survivor mid-round |
| 🎯 **Become Sniper** | 15 AP | Transform into Sniper mid-round |

### Zombie Items

| Item | Price | Description |
|------|-------|-------------|
| 💊 **Antidote** | 8 AP | Revert back to human (max 3 per round) |
| 🔴 **Zombie Madness** | 6 AP | 10 s invulnerability + red glow (max 5 per round) |
| 🧬 **T-Virus Grenade** | 6 AP | Infect nearby humans on detonation |
| 💀 **Become Nemesis** | 20 AP | Transform into Nemesis mid-round |
| 🥷 **Become Assassin** | 15 AP | Transform into Assassin mid-round |

> Role-buy items are disabled by default. Enable them with `"Enable": true` in `ExtraItemsCFG.jsonc`.  
> Players who already hold a special role cannot purchase another.

### Jetpack Details

- Hold **CTRL + SPACE** to activate thrust (consumes fuel).
- Thrust direction follows your **eye yaw + WASD keys** — full omnidirectional flight.
- Configure in `ExtraItemsCFG.jsonc`: `JetpackMaxFuel`, `JetpackThrustForce`, `JetpackHorizontalForce`, `JetpackFuelConsumeRate`.

### Knife Blink Details

- Swing the knife to use one charge.
- A geometry trace (`MaskTrace.Solid`) fires from the eye position in the look direction.
- If a wall is within `KnifeBlinkDistance` units, the blink stops **20 units before the surface** — no clipping through walls or falling out of the map.
- Configure in `ExtraItemsCFG.jsonc`: `KnifeBlinkCharges`, `KnifeBlinkDistance`, `KnifeBlinkCooldown`.

### Laser Trip Mines

| Type | Price | Behavior | HP | Limit |
|------|-------|----------|----|-------|
| 💚 **Laser Tripwire** | 6 AP | Continuous beam damage (10 dmg per 0.1 s tick) | 0 (invincible by default) | 2 per player |
| 🔴 **Explosive Mine** | 10 AP | Explodes on beam cross (radius 360 u, up to 2 600 dmg) | 0 (invincible by default) | 2 per player |

**Mine HP System:**
- Set `MineHealth` to any positive value in `ZombiePlagueLegacyCFG.jsonc` to make a mine destroyable by zombie **knife melee** attacks.
- Each knife swing within `ZombieAttackRange` (default 80 units) reduces HP by `ZombieAttackDamage`; the mine owner sees a live centre-screen HUD: **`Mine HP: <current> / <max>`**.
- At 0 HP the mine explodes using the standard explosion path (same effect as a beam trigger on an Explosive Mine).
- Default `0` keeps the original invincible behaviour.

### Tryder Details

- Grants bonus HP (`TryderHealth`), full armor (`TryderArmor`), and infinite clip.
- While active, the player has a **blue glow** (configurable `TryderGlowR/G/B`).
- Glow is removed on death or infection.

### Zombie Madness Details

- 10-second invulnerability window (duration configurable via `MadnessDuration`).
- While active, the player has a **red glow** (configurable `MadnessGlowR/G/B`, default `255 0 0`).
- Glow is removed automatically when the timer expires.

---

## 💣 Grenades & Weapons

All grenades are configured in `ZombiePlagueLegacyCFG.jsonc`.

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
<summary><strong>ZombiePlagueLegacyCFG.jsonc — Core settings</strong></summary>

```jsonc
{
  "ZPLMainCFG": {
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
    "ChatPrefix":          "[red][ZM][default]",
    "EconomyWalletKind":   "ammo",

    "NormalRoundsInterval": 0      // Min normal rounds between special modes (0 = disabled)
  }
}
```

</details>

<details>
<summary><strong>ZombiePlagueLegacyCFG.jsonc — Special class HP overrides</strong></summary>

```jsonc
"Nemesis":  { "NemesisHealth":  75000 },  // ~60 s TTK for 5 players, ~29 s for 10
"Survivor": { "SurvivorHealth":  8000 },  // Durable but killable by coordinated zombies
"Sniper":   { "SniperHealth":    5000 },  // Glass-cannon; dies fast if zombies close in
"Assassin": { "AssassinHealth": 24000 }   // ~45 s TTK for 2 focused players
```

> Set any value to `0` to use the raw HP from `ZPLSpecialClassCFG` instead.

</details>

<details>
<summary><strong>ZombiePlagueLegacyCFG.jsonc — Laser mines (ZPLMineCFG)</strong></summary>

```jsonc
"ZPLMineCFG": {
  "MineList": [
    {
      "Name": "Laser Tripwire",
      "CanExplorer": false,
      "Price": 6,  "Limit": 2,  "Team": "ct",
      "LaserRate": 0.1,  "LaserDamage": 10.0,  "LaserKnockBack": 100.0,
      "GlowColor": "0,255,0,255",  "LaserColor": "0,0,255,255",
      "ModelAngleFix": 90.0,
      "MineHealth": 500,        // HP for zombie melee attacks; 0 = invincible
      "ZombieAttackDamage": 150 // damage per zombie knife swing within ZombieAttackRange
    },
    {
      "Name": "Explosive Mine",
      "CanExplorer": true,
      "Price": 10,  "Limit": 2,  "Team": "ct",
      "ExplorerRadius": 360,  "ExplorerDamage": 2600,
      "GlowColor": "255,0,0,255",  "LaserColor": "255,0,0,255",
      "ModelAngleFix": 0.0,
      "MineHealth": 750,        // HP for zombie melee attacks; 0 = invincible
      "ZombieAttackDamage": 150 // damage per zombie knife swing within ZombieAttackRange
    }
  ]
}
```

> **`MineHealth`** — when > 0, zombies can **melee attack** (knife swing) the mine within `ZombieAttackRange` units. Each hit deals `ZombieAttackDamage` and shows the owner a live centre-screen HP readout. At 0 HP the mine explodes.

</details>

<details>
<summary><strong>ExtraItemsCFG.jsonc — Shop items, AP rewards & item settings</strong></summary>

```jsonc
"ZPLExtraItemsCFG": {
  "RoundSurviveReward": 3,
  "ZombieKillReward": 2,
  "HumanDamageRewardThreshold": 600,
  "HumanDamageReward": 1,

  // ── Tryder ─────────────────────────────────────────────────────────────────
  "TryderHealth": 1000,
  "TryderArmor":  500,
  "TryderGlowR":  0,     // Blue glow by default (R=0 G=127 B=255)
  "TryderGlowG":  127,
  "TryderGlowB":  255,

  // ── Zombie Madness ──────────────────────────────────────────────────────────
  "MadnessDuration": 10.0,
  "MadnessGlowR": 255,   // Red glow by default
  "MadnessGlowG": 0,
  "MadnessGlowB": 0,

  // ── Knife Blink ─────────────────────────────────────────────────────────────
  "KnifeBlinkCharges":  3,
  "KnifeBlinkDistance": 300.0,  // Max blink distance in units (trace stops at walls)
  "KnifeBlinkCooldown": 2.0,

  // ── Jetpack ──────────────────────────────────────────────────────────────────
  "JetpackMaxFuel":         250.0,
  "JetpackThrustForce":     350.0,
  "JetpackHorizontalForce": 300.0,
  "JetpackFuelConsumeRate":  30.0,

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
    { "Key": "tryder",           "Price": 15, "Team": "Human"  },
    { "Key": "unlimited_clip",   "Price": 8,  "Team": "Human"  },
    { "Key": "no_recoil",        "Price": 6,  "Team": "Human"  },
    { "Key": "buy_survivor",     "Price": 20, "Team": "Human",  "Enable": true },
    { "Key": "buy_sniper",       "Price": 15, "Team": "Human",  "Enable": true },
    { "Key": "antidote",         "Price": 8,  "Team": "Zombie", "PurchaseLimit": 3 },
    { "Key": "zombie_madness",   "Price": 6,  "Team": "Zombie", "PurchaseLimit": 5 },
    { "Key": "t_virus_grenade",  "Price": 6,  "Team": "Zombie", "PurchaseLimit": 3 },
    { "Key": "buy_nemesis",      "Price": 20, "Team": "Zombie", "Enable": true },
    { "Key": "buy_assassin",     "Price": 15, "Team": "Zombie", "Enable": true }
  ]
}
```

</details>

<details>
<summary><strong>ZombieClassesCFG.jsonc — Class schema example</strong></summary>

```jsonc
"ZPLZombieClassCFG": {
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

`IZombiePlagueLegacyAPI` is exposed as a SwiftlyS2 shared interface so external plugins can integrate with the gamemode.

**Registering the API:**

```csharp
public override void UseSharedInterface(IInterfaceManager interfaceManager)
{
    if (interfaceManager.HasSharedInterface("ZombiePlagueLegacy"))
    {
        var api = interfaceManager.GetSharedInterface<IZombiePlagueLegacyAPI>("ZombiePlagueLegacy");
        // hook events, query state, perform actions...
    }
}
```

**Capabilities:**

| Category | Examples |
|----------|---------|
| **Events** | `ZPL_OnPlayerInfect`, `ZPL_OnNemesisSelected`, `ZPL_OnGameStart`, `ZPL_OnHumanWin`, `ZPL_OnZombieWin` |
| **Queries** | `IsZombie()`, `IsNemesis()`, `IsAssassin()`, `IsSurvivor()`, `CurrentMode` |
| **Actions** | Force roles, give/take Ammo Packs, set glow / FOV / god mode |

Full interface: [`src/IZombiePlagueLegacyAPI/IZombiePlagueLegacyAPI.cs`](src/IZombiePlagueLegacyAPI/IZombiePlagueLegacyAPI.cs)

---

<div align="center">

Remade with ❤️ — based on the original plugin by [H-AN / Zombie PlagueS2](https://github.com/H-AN/HanZombiePlagueS2)

</div>

