# Steam library

A Windows app that reads the Steam account already logged in on this PC and lists the games it owns. Checking a game tells the running Steam client that the game is being played. Nothing is downloaded from Steam and no second login is required.

## What it does

The app scans once automatically when its window opens. **Scan** runs it again. It reads the local Steam installation:

- the account marked most recent in `config\loginusers.vdf`
- owned packages from that account's encrypted license cache
- app names and types from Steam's local `appinfo.vdf`
- whether a game is present in a Steam library folder

The grid shows a cached library icon, the AppID, the name, whether it is installed, and an **Idle?** checkbox. The search box filters by name or AppID. Icons and checked boxes stay attached to their rows while the list is filtered.

**Save JSON** writes the scanned list. The suggested file is `steam-games-<account>.json` in Documents. Each game has `appid`, `name`, `type`, and `installed`.

## Idling

Each checked game is reported to the already-open Steam client. Steam allows 34 games at once; checking a 35th is refused. Unchecking a game stops it, and closing the window stops all of them. **Stop all** stops every game and clears every checkbox, including rows hidden by the search.

Steam must be running and logged in before a game is checked.

When a game stops idling, the app closes it cleanly (it releases its Steam user and pipe) instead of killing it.

Each idled game connects to Steam the same way [Steam Achievement Manager](https://github.com/gibbed/SteamAchievementManager) does: `SteamAppId` is set, `steamclient64.dll` is loaded from the Steam install folder, and the `SteamClient018` interface is used to create a pipe, connect to the global user, and open the utils, user and user stats interfaces, with callbacks pumped every 100 ms. That code is adapted from SAM, Copyright (c) 2024 Rick (gibbed), under the zlib License.

## Build

The project targets .NET 9 for Windows x64.

```
dotnet publish WinFormsApp1.csproj -c Release -o publish
```

That writes one self-contained file, `publish\WinFormsApp1.exe`. It includes the app and the .NET runtime, so the machine it runs on does not need .NET installed.
