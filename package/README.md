# Player Limit Mod

Raise Stolen Realm's party and lobby cap above 6 — up to 30 heroes — with paged party and portrait UI.

**v2.0 is a rewrite for the current game (Unity 2022.3 build).** Versions 1.x stopped working after the game's update.

## Features

- **Configurable party size** — 1 to 30 heroes (default 16), applied to party selection, Steam lobbies and PlayFab lobbies.
- **Character select pages** — "Current Party" pages 6 heroes at a time with golden arrows, a page label, a `Party: X/16` counter (turns red when full) and **mouse-wheel paging**. Adding a hero jumps to the page it landed on.
- **Battle portrait pages** — one portrait per hero in the left column, a page at a time, with a [▲ 1/3 ▼] bar above the column and **mouse-wheel paging**. Selecting a hero automatically flips to their page.
- **Map events** — "everyone rolls" results list every hero; "Choose Character" and "Choose As Party" show every hero, paged with a [◀ Page 1/3 ▶] bar under the row and mouse-wheel paging. Vote dots show for 7+ players.
- **Optional extra enemies** — off by default (normal enemy counts). Turn it on to add enemies for every hero beyond 6. Enemy health and damage already scale with party size in the base game.
- **Inventory and gold** — right-click "Send to" and the Send Gold window list every hero.
- **Results screens** — every hero gets an XP bar after battles and adventures, not just the first 6.
- **Player colours for 7+ players** — extra distinct colours instead of a blank border.
- **Crash fixes for big parties** — map event dice rolls and battle pickups no longer crash with 7+ heroes (they use the 6-hero values beyond 6, so balance matches a full vanilla party).

## Installation

Install with Thunderstore Mod Manager or r2modman (search **Player Limit Mod** by girthy_mods), or manually:

1. Install [BepInExPack 5.4.2100](https://thunderstore.io/c/stolen-realm/p/BepInEx/BepInExPack/).
2. Put `PlayerLimitMod.dll` in `BepInEx/plugins/`.
3. Launch once to create the config.

> **Coming from the old StolenRealmModding version (1.x)?** Uninstall it first. Both versions share the same plugin ID, so having both installed means only one of them loads.

## Configuration

`BepInEx/config/com.stolenrealm.playerlimit.cfg`

| Setting | Default | |
|---|---|---|
| `General.MaxPlayers` | 16 | Party/lobby size, 1–30. Restart after changing. |
| `UI.PartySlotsPerPage` | 6 | Party slots per page on character select. |
| `UI.PortraitsPerPage` | 6 | Portraits per page in the left column. Above 6 may run off small screens. |
| `UI.EventPortraitsPerPage` | 6 | Hero tiles per page in map events. Above 6 may overflow the row. |
| `UI.PortraitsFollowSelection` | true | Flip portrait pages to follow the selected hero. |
| `UI.ExtraRewardXpBars` | true | XP bars for heroes past 6 on the post-battle and adventure results screens. |
| `Enemies.BonusEnemyPointsPerHeroAbove6` | 0 | Optional extra enemies: points added per hero beyond 6 (game already adds 2). 0 = normal, 2 = about a full 6-party's enemies per hero. Host's value is used. |
| `Debug.VerboseLogging` | false | Extra detail in `BepInEx/LogOutput.log`. |

## Multiplayer

Everyone in the session needs this mod with the **same `MaxPlayers`**. Players without the mod can't join a lobby with more than 6 heroes.

## Troubleshooting

Check `BepInEx/LogOutput.log` for the line `Patches applied: 22/22`. If a future game update breaks something, the log names exactly which patches did not apply; the rest keep working.

## Changes from 1.x

- Rebuilt against the current game. Fixes party pagination being overwritten (the game now refreshes the party in a coroutine), the Accept button greying out at 7+, and player colours never applying.
- Steam lobby patch now changes only the lobby size (1.x also rewrote an unrelated value).
- Removed PlayFab network patches that lowered the game's own 32-player network cap.
- Removed the always-on desktop debug log, F9/F10/F11 debug hotkeys and the per-frame reflection polling.
- Config cap is now really 30 (1.x clamped to 16).

## Credits

Based on the original [Player Limit Mod](https://thunderstore.io/c/stolen-realm/p/StolenRealmModding/Player_Limit_Mod/) by StolenRealmModding. v2 is a full rewrite for the current game, maintained by girthy_mods.

Built with [BepInEx](https://github.com/BepInEx/BepInEx) and [HarmonyX](https://github.com/BepInEx/HarmonyX).
