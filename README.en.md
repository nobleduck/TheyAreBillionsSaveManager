# They Are Billions Save Manager

English | [简体中文](README.md)

A Windows desktop utility that keeps paired save snapshots and restores survival progress from a game backup or an archived snapshot. Includes English and Simplified Chinese interfaces.

**[Download the latest release](https://github.com/nobleduck/TheyAreBillionsSaveManager/releases/latest)** · [Download the EXE directly](https://github.com/nobleduck/TheyAreBillionsSaveManager/releases/latest/download/TheyAreBillionsSaveManager.exe)

## Run without building

Download `TheyAreBillionsSaveManager-v0.2.1-windows.zip` from the release's **Assets**, extract it, and double-click **Start.cmd**. You can also run **dist/TheyAreBillionsSaveManager.exe**, or download the standalone EXE. The **Source code** downloads require building first.

Uses the .NET Framework included with Windows 10 / 11. No Python, Node.js, NuGet downloads or installer required. Steam and the game must already be installed. `SHA256SUMS.txt` contains SHA-256 hashes for the EXE and ZIP.

## Restore a save

1. Save and exit the game normally.
2. Open the tool, select a **Game backup** or a row in **Snapshot history**, and check its save name and time.
3. Click **Launch & restore**. Stay at the game's main menu while the tool prepares the files.
4. Wait for **Ready! You can click Continue now**, then load that save in the current game session.
5. The tool reports **Restore point loaded and confirmed** only after the game log confirms the expected file finished loading.

Do not click Continue early or restart the game after the files are ready. If you click too early, the tool stops; exit the game and retry. When finished playing, save and exit normally, then let Steam finish cloud synchronization.

Steam may download a newer main save during startup. This tool preserves the target first, launches through Steam, waits for a fresh main-menu session, backs up the current disk state, and then installs the chosen pair. It checks the loaded path and file integrity afterward. Steam cloud settings are not changed; the tool does not upload files itself.

## Language

On first launch, the tool uses Simplified Chinese for a Chinese Windows display language and English otherwise. Use **语言 / Language** in the top-right corner to switch immediately. Your choice is saved for the next launch. Switching is disabled while a backup or restore operation is active.

Controls, restore status, application errors and snapshot reasons are translated. Save names, filenames and file contents remain unchanged. Previous activity messages keep the language in which they were recorded. Windows errors and native system dialogs use the system language. Settings and Chinese snapshot reasons from v0.1.0 remain compatible.

## Snapshots and rollback times

- **Main save:** the current progress written to disk.
- **Game backup:** the game's `_Backup.zxsav` / `_Backup.zxcheck` pair.
- **Back up selected:** preserves the selected slot's complete pairs currently on disk.
- **Keep new saves:** checks every 15 seconds while the tool is open and idle. Unchanged content is deduplicated.
- **Snapshot history:** immutable copies preserved by the tool, with the original paired filenames.

The tool can only preserve saves the game has already written. It cannot force a save every 15 seconds or create an arbitrary point five minutes in the past. Listed time differences are wall-clock time, not in-game days.

The first automatic pass captures existing complete slots. Snapshots are retained until you remove them; use **Open snapshots** to inspect disk usage.

## Storage and recovery

| Data | Default location |
| --- | --- |
| Game saves | Windows Documents folder → `My Games\They Are Billions\Saves` |
| Snapshots | `%LOCALAPPDATA%\TheyAreBillionsSaveManager\Snapshots` |
| Preferences | `%LOCALAPPDATA%\TheyAreBillionsSaveManager\settings.json` |
| Restore journal and preserved originals | `TABSaveManager-Recovery`, beside the game's `Saves` folder |

Each snapshot records filenames, modification times, sizes and SHA-256 hashes in `snapshot.json`. The `.zxsav` / `.zxcheck` pair and `_Backup` names are preserved. Do not rename or mix files from different pairs.

To return to the state before a restore, select its **Safety backup before restore** entry and use the same restore workflow. Incomplete `.pending` folders are not offered as restore points. If the tool stops during a file transaction, preserved originals and `recovery.json` remain in the recovery directory.

The tool refuses incomplete or changing pairs, damaged snapshots, early menu interaction and unexpected process/log changes. Startup verification times out after 3 minutes; load verification after 10 minutes. A timeout does not force the game to close. This release supports the observed Steam save/log format; linked files, symlinks and junctions are not supported.

## Build and test

In a source checkout, run on Windows:

```powershell
.\build.ps1 -Test -UiSmoke -Package
```

The script uses Windows' built-in .NET Framework C# compiler. `VERSION` controls the executable metadata and ZIP name. Output goes to `dist/`, including the standalone EXE, portable ZIP and `SHA256SUMS.txt`.

Core tests use isolated temporary files and never restore real game saves. UI tests check both languages, normal/minimum window sizes, selection preservation and saved language preferences. Preview images are written to `build/ui-preview-*.png`.

The build script runs checks and produces the download package. See [the release guide](docs/RELEASING.md) for publishing a version.

## Development

`SaveStore.cs` handles immutable snapshots and guarded restores; `RollbackCoordinator.cs` checks fresh game sessions and successful loads; `MainForm.cs` provides the desktop interface. `Localization.cs` uses source-language message keys with English translations. Preserve format placeholders when editing translations. Snapshot reason IDs, filenames, log markers and journal states are language-independent.

Real saves, settings, snapshots and generated files are excluded from Git. Binaries are distributed through GitHub Releases. An open-source license has not yet been selected.
