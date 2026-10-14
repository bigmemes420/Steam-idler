# Steam library

A Windows app that reads the Steam account already logged in on this PC, lists the games it owns, and can idle them through the Steam client that is already open. There is no sign-in: the app never asks for a username, password or Steam Guard code.

## Scanning

The app scans once automatically when its window opens. Press **Scan** to scan again. A scan reads only local Steam files:

- the most recent account in `config\loginusers.vdf`
- the packages that account owns, from its encrypted license cache (`userdata\<id>\config\licensecache`)
- app names and types from `appcache\appinfo.vdf` (only apps of type "game" are listed)
- library folders from `config\libraryfolders.vdf`, plus each game's `appmanifest_<appid>.acf`

A game shows as **Installed** only when its app manifest says it is fully installed and its folder under `steamapps\common` exists. Being listed in a library folder is not enough.

## The list

Each row shows the game's cached library icon, AppID, name, whether it is installed, and an **Idle?** checkbox. The search box filters by name or AppID. Checked boxes stay checked while the list is filtered.

**Save JSON** saves the scanned list. The suggested file is `steam-games-<account>.json` in Documents. Each game has `appid`, `name`, `type` and `installed`.

## Idling

Steam must be running and logged in. Checking a game tells the open Steam client that the game is running, so it shows as playing. You can idle up to 34 games at once; a 35th is refused.

Each idled game runs as a small hidden copy of the app. Unchecking a game stops it, **Stop all** stops every game and clears every checkbox (including rows hidden by the search), and closing the window stops them all. Stopping is clean: the idle process releases its Steam connection and exits on its own, and it is only force-closed if it has not exited after 3 seconds.

## "Installed locally" and Restart Steam

While you idle a game, Steam may list it under **Installed locally**, even if it is not installed. Steam does this for any tool that idles through the open client (Steam Achievement Manager does it too). It is kept only in Steam's memory, not in files or the registry, and it goes away when Steam restarts. The app shows a note about this under the list.

**Restart Steam** clears it. After you confirm, it:

1. stops all idling and waits for the idle processes to exit
2. asks Steam to exit with `steam.exe -shutdown` and waits up to 30 seconds (if Steam is still running, it tells you and stops; it never force-closes Steam)
3. starts Steam again and waits up to 60 seconds for it to log in
4. scans again

## Requirements

- Windows x64
- Steam installed and logged in

## Build

The project targets .NET 9 for Windows x64.

```
dotnet publish WinFormsApp1.csproj -c Release -o publish
```

This creates a single self-contained file, `publish\WinFormsApp1.exe`, that includes the .NET runtime, so the PC it runs on does not need .NET installed.

## Credits

The code that connects idled games to Steam (`SteamIdle.cs`) is adapted from [Steam Achievement Manager](https://github.com/gibbed/SteamAchievementManager), Copyright (c) 2024 Rick (gibbed), under the zlib License.
