# Changelog — ZombieOutstandingCS2 vs h-an/zombieplaguecs2

> Toate modificările, funcționalitățile noi și bug-fix-urile față de versiunea originală a lui [H-AN/HanZombiePlagueS2](https://github.com/H-AN/HanZombiePlagueS2).

---

## ✨ Funcționalități Noi (Features)

### 🗺️ Game Mode-uri noi
| Mod | Descriere |
|-----|-----------|
| 🦸 **Hero** | Ultimii oameni în viață devin Eroi cu statistici extreme (HP, viteză, gravitație configurabile). |
| ⚔️ **Assassin vs Sniper (AVS)** | Zombie Assassin vs un singur Sniper uman — 1 vs 1. |

### 🛒 Extra Items Shop — articole noi
**Articole pentru oameni (Human):**
| Articol | Descriere |
|---------|-----------|
| 🧪 **SCBA Suit** | Blochează o singură infecție de zombie. |
| 🦘 **Multi-Jump** | +1 săritură extra (stivuibilă, max configurabil). |
| 🗡️ **Knife Blink** | 3 încărcături — teleportare înainte la fiecare lovitură de cuțit. |
| 🚀 **Jetpack** | CTRL+SPACE pentru a zbura; combustibil limitat, se resetează la fiecare rundă. |
| 💣 **Laser Mine** | Deschide meniu de plasare mine — Tripwire sau Explosive. |
| ❤️ **Revive Token** | Respawn automat o singură dată după moarte. |
| 🏹 **Become Survivor** | Transformă jucătorul în Survivor în mijlocul rundei. |
| 🎯 **Become Sniper** | Transformă jucătorul în Sniper în mijlocul rundei. |
| ♾️ **Unlimited Clip** | Muniție infinită în încărcătorul activ. |
| 🎯 **No Recoil** | Elimină recoil-ul armei. |
| ⚡ **Tryder** | HP și armură masivă + glow configurat — rol de tank. |

**Articole pentru zombie (Zombie):**
| Articol | Descriere |
|---------|-----------|
| 💊 **Antidote** | Revine la echipa umană (dezinfecție). |
| 🛡️ **Zombie Madness** | Invulnerabilitate temporară (10 s implicit). |
| 🧬 **T-Virus Grenade** | Grenadă care infectează oamenii din raza de explozie. |
| 💀 **Become Nemesis** | Transformă zombie-ul în Nemesis în mijlocul rundei. |
| 🥷 **Become Assassin** | Transformă zombie-ul în Assassin în mijlocul rundei. |

### 💣 Sistem Laser Trip Mines
Integrat din [H-AN/HanLaserTripmineS2](https://github.com/H-AN/HanLaserTripmineS2):
- **Laser Tripwire** — Daune continue (beam), configurabile per tick.
- **Explosive Mine** — Explozie la traversarea fasciculului.
- Limite per jucător, culori glow/laser, sunete, permissions — toate configurabile.
- Meniu `!mine` dedicat pentru plasare.

### 💰 Sistem Economy (Ammo Packs fără bază de date)
- Ammo Packs stocate prin **Economy plugin** — fără MySQL, fără configurare DB.
- Recompense configurabile: supraviețuire rundă, ucidere zombie, prag de damage.
- `EconomyWalletKind` configurabil pentru compatibilitate cu alte plugin-uri Economy.
- Balanțele persistă între restartul hărții și al serverului.

### 📊 Status HUD Permanent
- Un singur rând compact în centrul ecranului: **Mod · Clasă · Ammo Packs**.
- Actualizat live; poate fi dezactivat via `EnableStatusHud`.

### 🌑 Sistem Fog (Ceață)
- Fog aplicat automat la orice hartă la fiecare load și pentru fiecare jucător care apare (spawn).
- Configurabil: culoare (RGB), distanță start/end, densitate maximă, exponent.
- Poate fi dezactivat complet cu `"Enable": false` în `FogConfig`.

### 🔫 Weapons Menu
- Comandă `!buyweapons` / `sw_buyweapons` — jucătorii pot cumpăra arme înainte de infecție.
- Vizibil doar în fereastra de cumpărare (prima parte a rundei).

### 🎯 One-Shot Kill pentru Sniper Mode
- Opțiunea `OneShotKill: true` — orice tir de sniper omoară instant orice zombie, indiferent de HP (comportament CS 1.6).

### 🔌 API complet — `IZombieOutstandingAPI`
- Interfață shared pentru plugin-uri externe.
- Interogare stare: `IsZombie()`, `IsNemesis()`, `IsAssassin()`, `IsSurvivor()`, `IsHero()`, `CurrentMode`.
- Acțiuni: forțare roluri, give/take Ammo Packs, set glow / FOV / god mode.
- Evenimente: `ZO_OnPlayerInfect`, `ZO_OnNemesisSelected`, `ZO_OnGameStart`, `ZO_OnHumanWin`, `ZO_OnZombieWin`.

---

## 🔧 Configurații Noi

| Cheie | Valoare implicită | Descriere |
|-------|------------------|-----------|
| `MinPlayersForInfection` | `2` | Numărul minim de oameni pentru a începe infecția. Protecție la joc solo. |
| `NormalRoundsInterval` | `0` | Numărul minim de runde normale între moduri speciale. `0` = dezactivat (implicit). |
| `KnockZombieForce` | `250` | Forța knockback aplicată zombie-ilor la lovire. |
| `StunZombieTime` | `0.1` | Durata stun-ului zombie după knockback. |
| `HumanKnockBackHeadMultiply` | `2.0` | Multiplicator knockback pentru lovitură în cap. |
| `HumanKnockBackBodyMultiply` | `1.0` | Multiplicator knockback pentru lovitură în corp. |
| `HumanKnockBackGroundMultiply` | `1.0` | Multiplicator knockback când zombie-ul e pe sol. |
| `HumanKnockBackAirMultiply` | `0.5` | Multiplicator knockback când zombie-ul e în aer. |
| `HumanHeroKnockBackMultiply` | `1.0` | Multiplicator knockback separat pentru jucătorii Hero. |
| `ChatPrefix` | `"[ZO]"` | Prefix vizibil înaintea mesajelor de chat ale plugin-ului. |
| `EconomyWalletKind` | `"ammo"` | Tipul de portofel Economy folosit pentru Ammo Packs. |
| `EnableCommandDebugLogs` | `false` | Loghează comenzile în consola serverului. |
| `EnableCommandDebugChatReply` | `false` | Răspunde în chat la invocarea comenzilor (debug). |
| `Fog` (secțiune) | — | Configurare fog server-wide (culoare, distanță, densitate). |
| `Skybox` | `""` | Override skybox per-hartă (lasă gol pentru skybox-ul default). |
| `NemesisHealth` | `75000` | HP static pentru Nemesis (0 = fallback la valoarea din clasă). |
| `AssassinHealth` | `24000` | HP static pentru Assassin (0 = fallback la valoarea din clasă). |
| `SurvivorHealth` | `8000` | HP static pentru Survivor (0 = HP uman de bază). |
| `SniperHealth` | `5000` | HP static pentru Sniper (0 = HP uman de bază). |
| `OneShotKill` *(Sniper)* | `true` | Un singur tir omoară instant orice zombie. |
| `InvisibilityDist` *(Assassin)* | `200.0` | Distanța (unități) de la care Assassin devine vizibil. |
| `TVirusCanInfectHero` | `true` | Dacă T-Virus Grenade poate infecta jucătorii Hero. |
| `AmbSound` | `""` | Sunet ambient în buclă pe tot parcursul hărții. |
| `AmbSoundLoopTime` | `60.0` | Intervalul de repetiție a sunetului ambient (secunde). |
| `AmbSoundVolume` | `0.6` | Volumul sunetului ambient. |

---

## 🆕 Plugin-uri Standalone Noi

### 🏅 zo_rank — Rank & Top
Un plugin de ranking lightweight cu persistență SQLite:
- Comenzi: `!rank` (rank personal), `!top` / `!top10` / `!top15` (top jucători).
- Stats tracked: kills, deaths, ratio KDA.
- Eliminare boți și sinucideri din statistici.
- Configurabil: dimensiune top list, rânduri vizibile în meniu, hot-reload complet.
- Toate mesajele în fișier de traducere separat.

### 👑 zo_vip — VIP Perks
Plugin de VIP integrat cu `IZombieOutstandingAPI` și Economy plugin:
| Perk | Cheie config |
|------|-------------|
| Armură la spawn | `ArmorAmount` |
| Multi-jump | `ExtraJumps` / `JumpVelocity` |
| Fără damage la cădere | `NoFallDamage` |
| Multiplicator damage vs zombie | `DamageMultiplier` |
| Recompensă AP per damage | `DamageRewardThreshold` / `DamageRewardAmount` |
| Recompensă AP per kill zombie | `KillRewardAmount` |
| Happy Hour (AP bonus + frags) | `HappyHour*` |
| Recompensă infecție (VIP zombie) | `InfectRewardsEnabled` |
| Meniu `!vip` cu linii configurabile | `BenefitLines` |
| Meniu `!vips` cu VIP-ii online | `VipsListCommand` |
| Anunț la join VIP | `JoinAnnounceEnabled` |

---

## 🐛 Bugfix-uri

| Fix | Descriere |
|-----|-----------|
| **Null reference la join** | `OnClientConnected` — operator null-forgiving pe `player` previne crash la conectare. |
| **Round-end instant în solo** | Adăugat flag `MotherZombieWasSelected`; `CheckRoundWinConditions()` nu mai încheie runda dacă nu a existat infecție. |
| **Fog pe hărți workshop** | Fog nu se aplica pe hărțile workshop din cauza ordinii evenimentelor; rezolvat cu hook dedicat. |
| **MaxVisibleItems crash** | Meniurile aruncau excepție când numărul de iteme depășea limita vizibilă; fix ScrollableMenu. |
| **ObjectDisposedException async** | Accesarea `SteamID` al jucătorului în callback-uri async după deconectare; fix prin capturarea valorii înainte. |
| **Thread-unsafe entity ops** | Operațiunile pe entități în afara thread-ului principal; mutate în `NextWorldUpdate`. |
| **MinPlayersForInfection default greșit** | Valoarea default era `1` (nefuncțional ca protecție); corectată la `2`. |
| **Cheie API shared interface greșită** | Cheia `"HanZombiePlague"` redenumită în `"ZombieOutstanding"` în toată baza de cod. |
| **Config server CS2 greșit** | Configurații greșite pentru friendly-fire, solid teammates, boți și round restart delay; corectate. |
| **Duplicate closing brace build** | Eliminată acolada duplicată în `ZPOVIPPlugin.cs` care bloca compilarea. |
| **Chat prefix hot-reload** | Prefix-ul de chat nu se actualiza la schimbarea fișierului config fără restart; fix prin `IOptionsMonitor`. |
| **VIP detection greșit** | Permisiunea VIP nu era verificată corect; corectat cu `IsValidRealPlayer`. |
| **Fog lipsă la schimbare hartă** | Fog-ul nu persista la schimbarea hărții; aplicat din nou în evenimentul `OnMapStart`. |
| **Meniu top truncat** | Numele lungi depășeau limita de caractere a meniului; trunchiate la afișare. |

---

## ♻️ Modificări & Refactorizări

- **HP statice pentru clase speciale** — Nemesis, Assassin, Survivor, Sniper au HP fixe echilibrate în loc de calcul dinamic.
- **Jetpack: only upward thrust** — Push-ul orizontal tip rachetă a fost eliminat; jetpack-ul aplică doar forță verticală.
- **Knockback sistem granular** — Multiplicatori separați per locație de lovire și per stare (sol/aer) în loc de valoare globală unică.
- **Meniu scrollable** — Toate meniurile folosesc `LinearScroll`; footer-ul rămâne fix în jos.
- **Marquee text** — Text scrolling animat în meniurile VIP și Top pentru afișaj mai estetic.
- **API key redenumit** — `"HanZombiePlague"` → `"ZombieOutstanding"` pentru consistență cu noul brand.
- **Cheie de navigare meniu standardizată** — Toate plugin-urile folosesc keybind-urile implicite SwiftlyS2 (nu mai sunt hard-codate).
- **CI/CD GitHub Actions** — Job-uri separate de build pentru `zo_vip` (ZPOVIP) și `zo_rank` (ZORank) pe lângă plugin-ul principal.

---

## 📁 Fișiere Configurare Noi

| Fișier | Descriere |
|--------|-----------|
| `configs/plugins/ZombieOutstandingCS2/ZombieOutstandingCFG.jsonc` | Config principal (extins față de original). |
| `configs/plugins/ZombieOutstandingCS2/ZombieClassesCFG.jsonc` | Statistici clase zombie. |
| `configs/plugins/ZombieOutstandingCS2/ExtraItemsCFG.jsonc` | Prețuri shop, recompense AP. |
| `configs/plugins/ZPOVIP/ZPOVIP.jsonc` | Config plugin VIP. |
| `configs/plugins/ZORank/ZORankCFG.jsonc` | Config plugin Rank/Top. |
| `gamemode_casual.cfg` | Config gamemode CS2 (friendly-fire, solid teammates, boți). |
| `gamemode_casual_server.cfg` | Config server specific gamemode. |
| `server.cfg` | Config general server. |

---

<div align="center">

Bazat pe plugin-ul original creat de [H-AN](https://github.com/H-AN/HanZombiePlagueS2) · Extins cu ❤️ de [DeadPoolCS2](https://github.com/DeadPoolCS2)

</div>
