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

When a game stops idling, the app closes it cleanly so Steam sees a normal exit. If the game was not installed before idling, the app then removes anything Steam recorded that makes it look installed: an `appmanifest_<appid>.acf` that Steam created during idling with no installed flag, no game folder and no size on disk, and the `Installed`/`Running` flags under `HKCU\Software\Valve\Steam\Apps\<appid>`. Games that were installed, or that start downloading while idled, are never touched. Steam may need a restart before the game leaves **Installed locally**.

## Build

The project targets .NET 9 for Windows x64.

```
dotnet publish WinFormsApp1.csproj -c Release -o publish
```

That writes one self-contained file, `publish\WinFormsApp1.exe`. It includes the app and the .NET runtime, so the machine it runs on does not need .NET installed.
