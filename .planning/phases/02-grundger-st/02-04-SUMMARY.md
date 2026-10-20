---
phase: 02-grundger-st
plan: 04
subsystem: hosting
tags: [logging, rolling-log, single-instance, crash-handlers, data-dir]

requires:
  - phase: 02-grundger-st
    provides: "02-02 BarController, Program"
provides:
  - "MehrBar.Core.Logging: IAppLog, NullAppLog, AppLog.Current, RollingFileLog (1 MB x 4, never throws, sanitises lines); 6 tests"
  - "AppPaths (--data-dir, %APPDATA%\\MehrBar, logs, config.json), SingleInstance (Local mutex), CrashHandlers (dispatcher, domain, tasks)"
  - "Log lines startup, startup-elevated, bar-ready, exit"
affects: [02-07, 02-09, 02-10, 02-15]

actuals:
  tasks: 2
  commits: 1

key-files:
  created:
    - src/MehrBar.Core/Logging/IAppLog.cs
    - src/MehrBar.Core/Logging/RollingFileLog.cs
    - src/MehrBar.Tests/Logging/RollingFileLogTests.cs
    - src/MehrBar.App/Hosting/AppPaths.cs
    - src/MehrBar.App/Hosting/CrashHandlers.cs
    - src/MehrBar.App/Hosting/SingleInstance.cs
  modified:
    - src/MehrBar.App/Hosting/Program.cs
    - src/MehrBar.App/Bars/BarWindow.cs

key-decisions:
  - "Mutex name Local\\MehrBar.SingleInstance.B1C7A36B-A2F6-4D2F-AF9E-FA619DA8047B"
  - "Rolling happens at open and before a write once the current file reached maxBytes"

requirements-completed: [APP-05]

coverage:
  - id: D1
    description: "RollingFileLog rolls, keeps four files, sanitises, never throws"
    verification:
      - kind: other
        ref: "RollingFileLogTests 6/6 (logging.trx)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Single instance, log lines, data dir"
    verification:
      - kind: other
        ref: "Smoke run with --data-dir: second start exit 0 after 30 ms; log has startup, startup-elevated, bar-ready elapsed_ms=241, exit; Release build 0 warnings; suite 59/59; grep checks OK"
        status: pass
    human_judgment: false

completed: 2026-10-20
status: complete
---

# Phase 2 Plan 04: Log, single instance, crash handlers Summary

**MehrBar now writes a rolling log under %APPDATA%\MehrBar\logs (or --data-dir), logs every unhandled error without ending, and a second start exits silently in about 30 ms before WPF loads.**

## Task Commits

- this commit: `feat(02-04): rolling log, single instance, crash handlers and data folder`

## Deviations from Plan

- `AppPaths.cs` needs `using System.IO` (WPF projects drop it from the implicit usings, as in phase 1).
- The smoke run happened in the Hivemo session, which runs elevated in session 0; the log correctly reported `elevated=true` and the UIPI warning. Stephan's normal start is not elevated.

## Self-Check: PASSED
