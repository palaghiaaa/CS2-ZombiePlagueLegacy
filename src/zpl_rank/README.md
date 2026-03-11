# ZPL Rank & Top

A lightweight **rank and top-players** plugin for [ZombiePlagueLegacy CS2](https://github.com/DeadPoolCS2/ZombiePlagueLegacyCS2) built on the [SwiftlyS2](https://github.com/swiftly-solution/swiftly) framework.

---

## Features

| Command | Description |
|---------|-------------|
| `!rank` | Shows your current rank, kills and deaths in chat |
| `!top` | Opens a scrollable menu of the top N players (configurable) |
| `!top15` | Always opens a top-15 menu |
| `!top10` | Always opens a top-10 menu |

- Stats are tracked **per session** (in-memory) — bots and suicides are excluded  
- All command names, the chat tag, and list sizes are **hot-reloadable** via the config file  
- All user-facing strings are in a **separate translation file** — easy to localise

---

## Installation

1. Build the project or grab the compiled `ZPLRank.dll` from a release.  
2. Copy `ZPLRank.dll` to your SwiftlyS2 plugins folder.  
3. Copy `configs/plugins/ZPLRank/ZPLRankCFG.jsonc` to the same path on your server.  
4. Copy `translations/en.jsonc` into the plugin's `resources/translations/` folder  
   (the build system does this automatically when you publish).  
5. Restart the server or load the plugin with `sw_plugins load ZPLRank`.

---

## Configuration — `configs/plugins/ZPLRank/ZPLRankCFG.jsonc`

```jsonc
{
  "ZPLRankCFG": {
    // Tag shown at the start of every chat message.
    "ChatTag": "[ZPL Rank]",

    // Command names (can be changed without recompiling).
    "RankCommand":  "rank",
    "TopCommand":   "top",
    "Top15Command": "top15",
    "Top10Command": "top10",

    // How many players !top shows (top10/top15 always use 10/15).
    "TopListSize": 15,

    // Rows visible at once in the scrollable top menu.
    "TopMenuVisibleRows": 5,

    // Set to false to disable individual features.
    "EnableRankCommand": true,
    "EnableTopCommands": true
  }
}
```

---

## Translations — `translations/en.jsonc`

Copy and rename this file (e.g. `fr.jsonc`) to add a new language.  
All keys support positional placeholders `{0}`, `{1}` …

| Key | Placeholders | Default |
|-----|-------------|---------|
| `RankMessage` | `{0}` name · `{1}` rank · `{2}` total · `{3}` kills · `{4}` deaths | `Player {0}'s rank is {1}/{2} with {3} Kills and {4} Deaths` |
| `TopMenuTitle` | `{0}` limit | `TOP {0}` |
| `TopMenuNoStats` | — | `No stats available yet.` |
| `TopMenuEntry` | `{0}` position · `{1}` name · `{2}` kills · `{3}` deaths | `#{0}  {1}  [{2} Kills / {3} Deaths]` |

---

## Project structure

```
src/zpl_rank/
├── ZPLRank.csproj          # Project file
├── ZPLRank.cs              # Plugin entry-point & logic
├── ZPLRankCFG.cs           # Strongly-typed config class
├── translations/
│   └── en.jsonc           # English strings (add other locales here)
└── README.md              # This file

configs/plugins/ZPLRank/
└── ZPLRankCFG.jsonc        # Server-side config (deploy alongside the DLL)
```

---

## License

Licensed under the same terms as [ZombiePlagueLegacyCS2](../../../LICENSE).
