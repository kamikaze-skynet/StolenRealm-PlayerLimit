# Player Limit Mod for Stolen Realm

A BepInEx 5 mod that raises Stolen Realm's party and lobby cap above 6 (up to 30 heroes) and fixes every screen that assumed 6.

**Install it from Thunderstore:** [girthy_mods / Player Limit Mod](https://thunderstore.io/c/stolen-realm/p/girthy_mods/Player_Limit_Mod/)

This repo is the source. The player-facing description, settings and changelog are in [`package/README.md`](package/README.md) and [`package/CHANGELOG.md`](package/CHANGELOG.md).

## What it does

- Party size and Steam/PlayFab lobby size up to 30 (config, default 16)
- Paged UI wherever the game had exactly 6 slots: character select, battle portraits, map event hero rows, Send Gold
- Lists that ran out of rows with 7+ heroes: item right-click "Send to", "everyone rolls" event results, XP bars, vote dots
- Crash fixes for tables indexed by party size (event dice modifiers, battle pickups)
- Optional extra enemies for big parties (off by default)

## Repository layout

| Path | |
|---|---|
| `PlayerLimitMod/` | The mod (C#, .NET Framework 4.7.2, HarmonyX patches) |
| `PlayerLimitMod/Patches/` | One file per game area. Each `[HarmonyPatch]` class is applied on its own, so a game update that breaks one patch doesn't take down the rest |
| `PlayerLimitMod/UI/` | Page bars and pagers built from the game's own fonts |
| `package/` | Thunderstore package files (manifest, README, changelog, icon) |
| `tests/PatchHarness/` | Loads the real game assemblies and applies every patch under Mono, then checks the transpiled IL |
| `art/make_art.py` | Generates the icon and page-arrow art |
| `scripts/` | Build-and-package scripts |

No game files are included. You need your own copy of Stolen Realm to build.

## Building

Requirements: the [.NET SDK](https://dotnet.microsoft.com/download) (8 or newer), Stolen Realm, and a BepInEx 5 `core` folder (from your mod manager profile, or [BepInExPack](https://thunderstore.io/c/stolen-realm/p/BepInEx/BepInExPack/)).

Tell the build where those are, either as properties:

```
dotnet build PlayerLimitMod -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Stolen Realm" -p:BepInExCore="C:\Users\you\AppData\Roaming\Thunderstore Mod Manager\DataFolder\StolenRealm\profiles\Default\BepInEx\core"
```

or as environment variables `STOLEN_REALM_DIR` and `BEPINEX_CORE`. If neither is set, `GameDir` defaults to the standard Steam folder and `BepInExCore` to `<GameDir>\BepInEx\core`.

To build and produce the Thunderstore zip in `dist/`:

```powershell
# Windows
$env:STOLEN_REALM_DIR = "D:\SteamLibrary\steamapps\common\Stolen Realm"
$env:BEPINEX_CORE     = "C:\path\to\BepInEx\core"
.\scripts\package.ps1
```

```bash
# Linux / macOS
STOLEN_REALM_DIR=/path/to/Stolen\ Realm BEPINEX_CORE=/path/to/BepInEx/core ./scripts/package.sh
```

The version comes from `package/manifest.json`. Keep `Version` in `PlayerLimitMod/Plugin.cs` and `PlayerLimitMod.csproj` in step with it.

## Testing

`tests/PatchHarness` applies every patch to the real game code without starting the game, checks that each transpiler replaced exactly the intended constants, and runs the patches that don't need the Unity engine. It needs the full `Stolen Realm_Data/Managed` folder and must run under **Mono** (Unity's engine calls can't load under .NET Core):

```
dotnet build tests/PatchHarness -c Release -o tests/PatchHarness/out -p:BepInExCore="C:\path\to\BepInEx\core"
mono tests/PatchHarness/out/PatchHarness.exe "<GameDir>/Stolen Realm_Data/Managed" "C:\path\to\BepInEx\core" PlayerLimitMod/bin/Release/PlayerLimitMod.dll 16
```

Expect `ALL CHECKS PASSED`. The last argument is the party size to test with.

## How it finds things in the game

Patches target the game's own types directly, so the compiler catches renamed members. Hard-coded `6`s are replaced with `PlayerLimitPlugin.MaxPlayers` by transpilers that only match a `6` feeding a comparison or a specific call. Each transpiler states how many it expects to replace and logs a warning if the game no longer matches. On startup the log reports `Patches applied: N/N` and names any patch that failed.

## License

[MIT](LICENSE). Stolen Realm itself and its assets are not covered and are not included in this repo.

## Credits

Based on the original [Player Limit Mod](https://thunderstore.io/c/stolen-realm/p/StolenRealmModding/Player_Limit_Mod/) by StolenRealmModding. v2 is a full rewrite for the current game by girthy_mods.

Built with [BepInEx](https://github.com/BepInEx/BepInEx) and [HarmonyX](https://github.com/BepInEx/HarmonyX). Stolen Realm is by Burst2Flame Entertainment; this project is not affiliated with them.
