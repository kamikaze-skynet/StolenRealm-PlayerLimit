## 2.1.3

- Enemy counts are back to the game's normal scaling by default. The extra-enemies option is now `Enemies.BonusEnemyPointsPerHeroAbove6` and defaults to 0 (the old setting from 2.1.0 is ignored, so existing installs reset automatically). Set it to 2 or more if you want bigger fights.

## 2.1.2

- Fixed: the Send Gold button (⇄ next to your gold) did nothing with 7+ heroes. The window has a spare row with no name label that the game never fills in a 6-party; filling it crashed the window. That row is now skipped and extra rows are built from a working one.
- Extra rows for the item menu, event results and XP bars are now copied from a row the game is known to use.

## 2.1.1

- Fixed: "everyone rolls" events froze on "Pending..." with 7+ heroes and only listed 6 of them. The result list now has a row for every hero (also fixed for other players' screens in multiplayer).

## 2.1.0

- **More enemies for big parties.** Each hero beyond 6 adds extra enemy points when a battle is rolled (default +2 per hero, on top of the game's own +2), so a 16-hero party faces about the same number of enemies per hero as a full 6-party. Set with `Enemies.ExtraPointsPerHeroAbove6` (0 = game's scaling only). The host's setting decides. Boss and scripted battles keep their designed lineups.
- Enemy spawn safety: if the normal enemy spawn area fills up, enemies spawn on the nearest free hex instead of breaking the battle.
- Send Gold: every hero is now listed as a recipient (the list stopped at 5). Paged with a [◀ Page 1/3 ▶] bar under the list and mouse-wheel paging.

## 2.0.3

- Portrait page bar now fits the space above the portraits: it shrinks on high-resolution or scaled UIs and never gets cut off by the screen edge. Same on-screen safety for the event page bar.

## 2.0.2

- Inventory: right-click "Send to [hero]" now lists every hero (the menu ran out of slots and stopped partway). The menu also stays on screen when opened near the bottom.
- Battle portrait column: page controls are now one [▲ 1/3 ▼] bar above the first portrait. The down arrow could previously be pushed off the bottom of the screen.
- Map events: page controls are now a centred [◀ Page 1/3 ▶] bar under the hero row instead of arrows off the row's ends.
- Page bars sit on a dark gold-edged plate with larger arrows; the greyed-out arrow is easier to see. Mouse wheel over a bar also pages.

## 2.0.1

- Map events: "Choose Character", "Choose As Party" and "Choose For Your Characters" now show every hero instead of only the first 6, paged with arrows, a page label and mouse-wheel paging.
- Event vote dots now show for players 7+.
- Post-battle screen shows an XP bar for every hero (previously only the adventure results screen did).
- New config option `UI.EventPortraitsPerPage`.

## 2.0.0

Rewrite for the current Stolen Realm build (Unity 2022.3). 1.x no longer worked after the game update.

**Fixed**
- Party pagination on character select was overwritten by the game (party refresh now runs as a coroutine).
- Accept button greyed out with 7+ heroes.
- Battle portraits for heroes 7+ never appeared (1.x relied on an update loop that the game shuts down at startup).
- Player colours for 7+ players never applied (game changed its colour list type).
- Map event dice rolls crashed with 7+ heroes (party-size modifier table had 6 entries).
- Only one of the two PlayFab lobby methods was patched.
- Steam lobby patch also rewrote an unrelated value; it now changes only the lobby size.
- "Add To Party" mouse hint disappeared at 6 heroes.

**New**
- Mouse-wheel paging on the party panel and the portrait column.
- Portrait column follows the selected hero to its page.
- Character select jumps to the page a newly added hero lands on; party counter turns red when full.
- XP bars on the results screen for every hero.
- Config options for slots/portraits per page, selection follow, and reward bars.
- Startup log reports how many patches applied and names any that didn't.
- New icon and page-arrow art.

**Removed**
- PlayFab network patches that lowered the game's own 32-player network cap to 16.
- Desktop debug log file, F9/F10/F11 debug hotkeys, per-frame reflection polling.
- No-op battle placement radius patch.

**Changed**
- MaxPlayers range is now really 1–30 (1.x clamped to 16).
- Roster limit is raised only as far as needed to fill the party (1.x removed the character creation limit entirely).

## 1.1.0

- Fixed mod initialization crash caused by PlayFab ambiguous method match
- Added battle portrait pagination with up/down arrow navigation
- Fixed UpdatePortraits hardcoded 6-player limit
- Added BattleManager.CheckBattleDrops array bounds guard for 7+ players

## 1.0.0

- Initial release
