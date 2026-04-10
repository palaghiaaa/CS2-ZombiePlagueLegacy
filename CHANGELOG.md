# ZombiePlagueLegacyCS2 — Changelog

> Toate modificările, funcționalitățile noi și bug-fix-urile față de versiunea originală a lui [H-AN/HanZombiePlagueS2](https://github.com/H-AN/HanZombiePlagueS2).

**Plugin:** ZombiePlagueLegacyCS2  
**Bazat pe:** [HanZombiePlagueS2](https://github.com/H-AN/HanZombiePlagueS2) de H-AN  
**Extins de:** [DeadPoolCS2](https://github.com/DeadPoolCS2)

---

Salut tuturor! Mai jos găsiți o listă completă cu tot ce s-a adăugat, modificat și rezolvat față de plugin-ul original. Conținut nou, ajustări de echilibru și optimizări de experiență pe toată linia.

---

## ✨ Ce este nou

### Game Mode-uri noi

- 🦸 **Hero** — Ultimii oameni în viață devin Eroi cu statistici extreme. HP, viteză și gravitație sunt complet configurabile.
- ⚔️ **Assassin vs Sniper (AVS)** — Un Zombie Assassin se confruntă față în față cu un singur Sniper uman. Pur 1v1.

### Articole noi în Shop

**Pentru oameni:**

- 🧪 **SCBA Suit** — Blochează automat o singură infecție de zombie.
- 🦘 **Multi-Jump** — Acordă +1 săritură extra. Stivuibil, cu un maxim configurabil.
- 🗡️ **Knife Blink** — 3 încărcături. Fiecare lovitură de cuțit te teleportează înainte.
- 🚀 **Jetpack** — Ține CTRL+SPACE pentru a zbura. Combustibilul este limitat și se resetează la fiecare rundă.
- 💣 **Laser Mine** — Deschide meniul de plasare mine. Alege Tripwire sau Explosive.
- ❤️ **Revive Token** — Te respawnează automat o singură dată după moarte.
- 🏹 **Become Survivor** — Transformă-te în Survivor în mijlocul rundei.
- 🎯 **Become Sniper** — Transformă-te în Sniper în mijlocul rundei.
- ♾️ **Unlimited Clip** — Muniție infinită în încărcătorul activ.
- 🎯 **No Recoil** — Elimină complet recoil-ul armei.
- ⚡ **Tryder** — HP și armură masivă cu un glow configurat. Rol complet de tank.

**Pentru zombie:**

- 💊 **Antidote** — Te întoarce în echipa umană.
- 🛡️ **Zombie Madness** — Invulnerabilitate temporară (10 secunde implicit).
- 🧬 **T-Virus Grenade** — Infectează oamenii din apropiere la detonare.
- 💀 **Become Nemesis** — Transformă-te în Nemesis în mijlocul rundei.
- 🥷 **Become Assassin** — Transformă-te în Assassin în mijlocul rundei.

### Sistem Laser Trip Mines

Integrat din [H-AN/HanLaserTripmineS2](https://github.com/H-AN/HanLaserTripmineS2):

- **Laser Tripwire** — Daune continue prin beam, configurabile per tick.
- **Explosive Mine** — Explodează când cineva traversează fasciculul.
- Limite per jucător, culori glow/laser, sunete și permisiuni sunt complet configurabile.
- Meniu `!mine` dedicat pentru plasare.

### Sistem Economy (Ammo Packs — fără bază de date)

- Ammo Packs stocate prin **Economy plugin** — fără MySQL, fără configurare DB.
- Recompense configurabile pentru supraviețuire rundă, ucideri zombie și praguri de damage.
- `EconomyWalletKind` configurabil pentru compatibilitate cu alte plugin-uri Economy.
- Balanțele persistă între schimbările de hartă și restarturile serverului.

### Status HUD Permanent

- Un rând compact în centrul ecranului: **Mod · Clasă · Ammo Packs**.
- Actualizat live. Poate fi dezactivat prin `EnableStatusHud`.

### Sistem Fog (Ceață)

- Fog aplicat automat la fiecare încărcare de hartă și pentru fiecare jucător la spawn.
- Complet configurabil: culoare (RGB), distanță start/end, densitate maximă și exponent.
- Poate fi dezactivat complet cu `"Enable": false` în `FogConfig`.

### Weapons Menu

- `!buyweapons` / `sw_buyweapons` — jucătorii pot cumpăra arme înainte de începerea infecției.
- Vizibil doar în fereastra de cumpărare (prima parte a rundei).

### One-Shot Kill pentru Sniper Mode

- `OneShotKill: true` — orice tir de sniper omoară instant orice zombie, indiferent de HP. Comportamentul clasic din CS 1.6.

### API complet — `IZombiePlagueLegacyAPI`

- Interfață shared pentru plugin-uri externe care se integrează cu gamemode-ul.
- Interogare stare: `IsZombie()`, `IsNemesis()`, `IsAssassin()`, `IsSurvivor()`, `IsHero()`, `CurrentMode`.
- Acțiuni: forțare roluri, give/take Ammo Packs, set glow / FOV / god mode.
- Evenimente: `ZPL_OnPlayerInfect`, `ZPL_OnNemesisSelected`, `ZPL_OnGameStart`, `ZPL_OnHumanWin`, `ZPL_OnZombieWin`.

---

## ⚙️ Configurații noi

- `MinPlayersForInfection` *(implicit: 2)* — Numărul minim de jucători necesar pentru a declanșa infecția. Previne problemele la joc solo.
- `NormalRoundsInterval` *(implicit: 0)* — Numărul minim de runde normale între moduri speciale. `0` = dezactivat.
- `KnockZombieForce` *(implicit: 250)* — Forța knockback aplicată zombie-ilor la lovire.
- `StunZombieTime` *(implicit: 0.1)* — Durata stun-ului după knockback.
- `HumanKnockBackHeadMultiply` *(implicit: 2.0)* — Multiplicator knockback pentru lovitură în cap.
- `HumanKnockBackBodyMultiply` *(implicit: 1.0)* — Multiplicator knockback pentru lovitură în corp.
- `HumanKnockBackGroundMultiply` *(implicit: 1.0)* — Multiplicator knockback când zombie-ul e pe sol.
- `HumanKnockBackAirMultiply` *(implicit: 0.5)* — Multiplicator knockback când zombie-ul e în aer.
- `HumanHeroKnockBackMultiply` *(implicit: 1.0)* — Multiplicator knockback separat pentru jucătorii Hero.
- `ChatPrefix` *(implicit: "[ZM]")* — Prefix afișat înaintea mesajelor de chat ale plugin-ului.
- `EconomyWalletKind` *(implicit: "ammo")* — Tipul de portofel Economy folosit pentru Ammo Packs.
- `EnableCommandDebugLogs` *(implicit: false)* — Loghează comenzile în consola serverului.
- `EnableCommandDebugChatReply` *(implicit: false)* — Răspunde în chat la invocarea comenzilor (debug).
- Secțiunea `Fog` — Configurare fog la nivel de server (culoare, distanță, densitate).
- `Skybox` *(implicit: "")* — Override skybox per-hartă. Lasă gol pentru skybox-ul implicit al hărții.
- `NemesisHealth` *(implicit: 75000)* — HP static pentru Nemesis. Setează `0` pentru fallback la valoarea din clasă.
- `AssassinHealth` *(implicit: 24000)* — HP static pentru Assassin. Setează `0` pentru fallback la valoarea din clasă.
- `SurvivorHealth` *(implicit: 8000)* — HP static pentru Survivor. Setează `0` pentru HP-ul uman de bază.
- `SniperHealth` *(implicit: 5000)* — HP static pentru Sniper. Setează `0` pentru HP-ul uman de bază.
- `OneShotKill` *(Sniper, implicit: true)* — Un singur tir omoară instant orice zombie.
- `InvisibilityDist` *(Assassin, implicit: 200.0)* — Distanța (unități) de la care Assassin devine vizibil.
- `TVirusCanInfectHero` *(implicit: true)* — Dacă T-Virus Grenade poate infecta jucătorii Hero.
- `AmbSound` *(implicit: "")* — Sunet ambient în buclă pe tot parcursul hărții.
- `AmbSoundLoopTime` *(implicit: 60.0)* — Intervalul de repetiție al sunetului ambient în secunde.
- `AmbSoundVolume` *(implicit: 0.6)* — Volumul sunetului ambient.

---

## 🔌 Plugin-uri Standalone Noi

### 🏅 zpl_rank — Rank & Top

Un plugin de ranking lightweight cu persistență SQLite:

- Comenzi: `!rank` (rank personal), `!top` / `!top10` / `!top15` (clasament).
- Urmărește eliminări, decese și ratio K/D.
- Boții și sinuciderile sunt excluse din statistici.
- Dimensiunea clasamentului, rândurile vizibile în meniu și suport complet pentru hot-reload sunt configurabile.
- Toate mesajele se află într-un fișier de traducere separat.

### 👑 zpl_vip — VIP Perks

Plugin VIP complet integrat cu `IZombiePlagueLegacyAPI` și Economy plugin:

- Armură la spawn (`ArmorAmount`)
- Multi-jump (`ExtraJumps` / `JumpVelocity`)
- Fără damage la cădere (`NoFallDamage`)
- Multiplicator damage vs zombie (`DamageMultiplier`)
- Recompensă AP per damage (`DamageRewardThreshold` / `DamageRewardAmount`)
- Recompensă AP per kill zombie (`KillRewardAmount`)
- Happy Hour — bonus AP + frags (`HappyHour*`)
- Recompensă infecție pentru zombie VIP (`InfectRewardsEnabled`)
- Meniu `!vip` cu linii de beneficii configurabile (`BenefitLines`)
- Meniu `!vips` cu VIP-ii online (`VipsListCommand`)
- Anunț la conectare VIP (`JoinAnnounceEnabled`)

### 🎲 zpl_teambets — Team Bets cu Ammo Packs

Plugin de pariuri pe echipe integrat cu Economy (Ammo Packs):

- Pariurile se deschid automat la `EventRoundStart` și se blochează la `EventRoundFreezeEnd`
- Jucătorii pariază AP pe **Humans** sau **Zombies** câștigând runda
- Comanda principală: `!bet` (deschide meniu) sau `!bet <sumă> <humans|zombies>` (direct)
- Preset-uri rapide configurabile (`QuickBetAmounts`): 10, 25, 50, 100, 250 AP
- Multiplicator de câștig configurabil (`WinMultiplier`, default 2.0×)
- Minim și maxim de pariu configurabile (`MinBet` / `MaxBet`)
- Plata automată la round-end; anunț broadcast cu câștigătorii și totalul AP distribuit
- Lifecycle complet: unregister comenzi, unhook events, close menus la `Unload()`

---

## 🐛 Bug Fix-uri

- **Null reference la join** — `OnClientConnected`: operatorul null-forgiving pe `player` previne un crash la conectare.
- **Round-end instant în solo** — Adăugat flag `MotherZombieWasSelected`. `CheckRoundWinConditions()` nu mai încheie runda dacă nu a avut loc nicio infecție.
- **Fog lipsă pe hărți workshop** — Fog-ul nu se aplica pe hărțile workshop din cauza ordinii evenimentelor. Rezolvat cu un hook dedicat.
- **MaxVisibleItems crash** — Meniurile aruncau excepție când numărul de iteme depășea limita vizibilă. Rezolvat gestionarea ScrollableMenu.
- **ObjectDisposedException în async** — `SteamID` era accesat în callback-uri async după deconectare. Rezolvat prin capturarea valorii înainte.
- **Operațiuni de entități thread-unsafe** — Operațiunile pe entități se întâmplau în afara thread-ului principal. Mutate în `NextWorldUpdate`.
- **Default greșit pentru MinPlayersForInfection** — Default-ul era `1` (fără protecție reală). Corectat la `2`.
- **Cheie API shared greșită** — Cheia `"HanZombiePlague"` redenumită în `"ZombiePlagueLegacy"` în întreaga bază de cod.
- **Config server CS2 greșit** — Setări incorecte pentru friendly-fire, solid teammates, boți și round restart delay. Toate corectate.
- **Eroare de build acoladă duplicată** — Eliminată o acoladă duplicată în `ZPLVIPPlugin.cs` care bloca compilarea.
- **Chat prefix fără hot-reload** — Prefix-ul de chat nu se actualiza la schimbarea fișierului config fără restart complet. Rezolvat cu `IOptionsMonitor`.
- **Detectare VIP greșită** — Permisiunea VIP nu era verificată corect. Rezolvat cu `IsValidRealPlayer`.
- **Fog lipsă după schimbare hartă** — Fog-ul nu persista la schimbarea hărții. Aplicat din nou în evenimentul `OnMapStart`.
- **Nume tăiate în meniul top** — Numele lungi depășeau limita de caractere a meniului. Acum trunchiate corect la afișare.
- **Glow Tryder oprit la deconectare** — `SetGlow` în `NextWorldUpdate` nu verifica validitatea pawn-ului. Adăugat try/catch și guard de validitate.
- **Knife Blink prin pereți** — Teleportarea folosea calcul naiv (origine + direcție × distanță) fără trace de geometrie. Înlocuit cu trace `MaskTrace.Solid` care oprește la primul perete.
- **Mine damage de la gloanțe** — Minele puteau fi distruse de orice glonț. Înlocuit cu detecție de atac corp la corp (knife proximity) pe swingul zombiei, care reflectă mai bine tema gameplay-ului.

---

## ♻️ Modificări & Îmbunătățiri

- **HP statice pentru clasele speciale** — Nemesis, Assassin, Survivor și Sniper folosesc acum valori HP fixe și echilibrate în loc de calcule dinamice.
- **Jetpack: forță doar în sus** — Push-ul orizontal de tip rachetă a fost eliminat. Jetpack-ul aplică acum doar forță verticală.
- **Sistem knockback granular** — Multiplicatori separați per locație de lovire (cap/corp) și starea zombie (sol/aer) în loc de o singură valoare globală.
- **Meniuri scrollable** — Toate meniurile folosesc `LinearScroll`. Footer-ul rămâne fixat în jos.
- **Text marquee** — Text animat cu scroll în meniurile VIP și Top pentru un aspect mai îngrijit.
- **Cheie API redenumită** — `"HanZombiePlague"` → `"ZombiePlagueLegacy"` pentru consistență cu noul brand.
- **Navigare meniu standardizată** — Toate plugin-urile folosesc acum keybind-urile implicite SwiftlyS2. Fără mai multe chei hard-codate.
- **CI/CD cu GitHub Actions** — Job-uri de build separate pentru `zpl_vip` (ZPLVIP) și `zpl_rank` (ZPLRank) alături de plugin-ul principal.
- **Glow Zombie Madness (roșu)** — Zombie Madness aplică acum un glow roșu pe durata activării. Culoarea este configurabilă via `MadnessGlowR/G/B` în `ExtraItemsCFG.jsonc`.
- **Mine HP + HUD** — Fiecare tip de mină acceptă acum un câmp `MineHealth`. Când > 0, zombii pot distruge mina prin atac corp la corp (knife swing în raza `ZombieAttackRange`). Proprietarul minei vede live HUD-ul: `Mine HP: <current> / <max>`. Raza și daunele per atac sunt configurabile (`ZombieAttackRange`, `ZombieAttackDamage`).
- **HP Mother Zombie scalat cu numărul de jucători** — HP-ul Mother Zombie se interpolează liniar între `MotherZombieHPMinMultiplier` (1 jucător) și `MotherZombieHPMultiplier` (≥ `MotherZombieHPMaxPlayers` jucători). Oprește HP-ul abuziv de mare pe servere cu 2–4 jucători. Poate fi dezactivat cu `MotherZombieHPPlayerScaleEnabled: false`.

---

## 📁 Fișiere de Configurare Noi

- `configs/plugins/ZombiePlagueLegacyCS2/ZombiePlagueLegacyCFG.jsonc` — Config principal, extins față de original.
- `configs/plugins/ZombiePlagueLegacyCS2/ZombieClassesCFG.jsonc` — Statistici clase zombie.
- `configs/plugins/ZombiePlagueLegacyCS2/ExtraItemsCFG.jsonc` — Prețuri shop și recompense AP.
- `configs/plugins/ZPLVIP/ZPLVIP.jsonc` — Config plugin VIP.
- `configs/plugins/ZPLRank/ZPLRankCFG.jsonc` — Config plugin Rank/Top.
- `configs/plugins/ZPLTeamBets/ZPLTeamBets.jsonc` — Config plugin Team Bets.
- `gamemode_casual.cfg` — Config gamemode CS2 (friendly-fire, solid teammates, boți).
- `gamemode_casual_server.cfg` — Config server specific gamemode.
- `server.cfg` — Config general server.

---

<div align="center">

Bazat pe plugin-ul original creat de [H-AN](https://github.com/H-AN/HanZombiePlagueS2) · Extins cu ❤️ de [DeadPoolCS2](https://github.com/DeadPoolCS2)

</div>
