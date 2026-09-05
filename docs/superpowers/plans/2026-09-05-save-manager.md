# Save Manager Implementation Plan

> Execute the authorized work in this session, using real temporary-file regression tests and a separate code review before delivery.

**Goal:** Ship a Chinese Windows executable for safe, repeatable They Are Billions save rollback.

**Architecture:** Immutable paired-file snapshots, a guarded file transaction, a fresh-session game-log state machine, and a WinForms UI. Only the process/clock boundary is substituted in regression tests.

**Tech Stack:** C# 5, .NET Framework, WinForms, built-in csc, PowerShell build script.

**Spec:** ../specs/2026-09-05-save-manager-design.md

## Global Constraints

- Preserve original paired filenames and hashes; never infer game time from a fabricated save point.
- Require a fresh Steam launch and a confirmed main menu before mutations.
- Never test rollback on the user's live saves.
- Keep personal data, binaries and snapshots out of Git.

## Task 1 — Snapshot storage and guarded restore

Files: src/Models.cs, src/SaveStore.cs, tests/CoreTests.cs, build.ps1.

Interfaces: SaveStore.ScanLocalPairs(), Capture(saveName, reason, deduplicate), GetSnapshots(), GetPairs(snapshot), Restore(pair, guard). Restore accepts archived pairs only.

- [x] Write regression cases with real temporary files for mismatched pairs, immutable snapshots, duplicate content, damaged archive files, unexpected filenames and restoring one slot without touching another.
- [x] Compile against placeholder contracts, run the test executable and confirm missing behaviors fail.
- [x] Implement stable paired-file reads with content verification, SHA-256 manifests and guarded file transaction with a durable recovery directory.
- [x] Add partial-failure tests with a guard that changes after the first move; confirm originals survive and no external new file is overwritten.

## Task 2 — Fresh game-session coordination

Files: src/GameLog.cs, src/RollbackCoordinator.cs, src/WindowsGameHost.cs, tests/CoreTests.cs.

Interfaces: IGameHost.Observe(), Launch(); GameObservation(Running, ProcessId, StartedUtc, LogLastWriteUtc, LogText); RollbackCoordinator.Begin(pair), Poll(), State, Message, Target.

- [x] Add literal log fixtures for a previous session, startup main menu, save chooser, wrong load path and completed target load.
- [x] Exercise the real coordinator and store with a test host that replenishes the main pair at launch, reproducing Steam's observed behavior.
- [x] Implement wait-for-menu, early-click refusal, per-mutation reobservation and exact load verification; confirm only the expected completed target sets Loaded.
- [x] Cover process exit/replacement and timeout without accepting stale successful logs.

## Task 3 — Desktop interface and release

Files: src/MainForm.cs, src/Program.cs, tests/UiSmoke.cs, README.md, Start.cmd, build.ps1.

- [x] Connect current pairs, immutable history, manual backup, deduplicated automatic backups, rollback progress and folder selection to the tested core.
- [x] Disable competing operations while recovery is in progress; preserve error messages and report an unfinished recovery on close.
- [x] Compile the executable, run all core tests and render an owned test-window preview using temporary fixture saves.
- [x] Review actual layout, correct clipping, document limitations and verify a read-only scan of local game saves.
- [x] Obtain an independent code review, fix actionable defects, run relevant checks, and initialize the deliverable Git history if covered by repository authorization.

## Delivery validation — 2026-09-05

- 28 core regression tests pass; compilation treats warnings as errors.
- Normal and minimum-size UI smoke checks pass, including initial backup selection and matching detail text.
- Independent core and UI integration reviews have no remaining actionable findings.
- A read-only scan recognizes the installed game's save directory, complete pairs and readable log.
- The portable ZIP contains only the launcher, README and executable; its executable hash matches the build output.
- Automated validation uses temporary fixtures. No live-game rollback was performed by the program during testing.
