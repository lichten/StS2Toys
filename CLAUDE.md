# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```powershell
dotnet build
dotnet run --project StS2Toys
```

No test or lint commands are configured.

## Tools Setup (one-time, git-ignored)

The `tools/` directory is not tracked. Localization data for cards/relics is extracted from the game's `.pck` file:

1. Place `GodotPCKExplorer.Console.exe` at `tools/GodotPCKExplorer/GodotPCKExplorer.Console.exe`
2. Extract game assets:
   ```powershell
   $pck = "C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\SlayTheSpire2.pck"
   .\tools\GodotPCKExplorer\GodotPCKExplorer.Console.exe -e $pck .\tools\extracted
   ```
3. The extracted `tools/extracted/` directory (~1.8 GB) provides localization JSON embedded at build time.

## Architecture

**StS2Toys** is a Windows Forms desktop app (.NET 10) that reads Slay the Spire 2 save files and displays the player's current deck and relics with EN/JP localized names.

### Data flow

1. `SaveDataService` loads a `.save` file (JSON) from the auto-detected Steam path (`%APPDATA%\SlayTheSpire2\steam\{steamId}\profile1\saves\current_run.save`) and deserializes it into `Models/SaveData.cs` records.
2. `CardDatabaseService` provides name/description lookups. It merges two sources:
   - `Resources/card_database.json` — hand-curated EN/JP name overrides (embedded resource)
   - Localization JSON extracted from the game (`localization/{eng,jpn}/{cards,relics}.json`) — also embedded at build time via `.csproj`
3. `DescriptionFormatter` strips HTML-like tags and template variables from raw description strings before display.
4. `Form1` is the main window with two `ListView` panels (deck + relics). Double-clicking opens `CardDetailForm`, a resizable modal showing the full card/relic details in both languages.

### Key files

| File | Role |
|---|---|
| `Models/SaveData.cs` | `[JsonPropertyName]`-annotated records: `RunSaveData`, `PlayerData`, `CardData`, `RelicData` |
| `Services/SaveDataService.cs` | `Load(path)` and `GetDefaultSavePath()` |
| `Services/CardDatabaseService.cs` | `GetName`, `GetDescription`, `GetFlavor` with EN/JP toggle |
| `Services/DescriptionFormatter.cs` | Tag-stripping utility |
| `Form1.cs` | Main UI; auto-loads on startup |
| `CardDetailForm.cs` | Detail modal |

### Known limitations / pending work

- Card type (Attack/Skill/Power) is not in save data — sorting/coloring by type requires a separate master data source.
- Upgraded card display (e.g. `"Strike+"`) depends on `upgrade_count` in save data; not yet confirmed present in early-game saves.
