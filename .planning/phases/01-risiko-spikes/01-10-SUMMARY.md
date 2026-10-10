---
phase: 01-risiko-spikes
plan: 10
subsystem: testing
tags: [z-order, win-d, winevent-hook, desktop-host, driver-case, spike-harness, cswin32]

requires:
  - phase: 01-risiko-spikes
    provides: "01-07 HostProcess/ZOrder/PixelProbe/DesktopPrep, 01-04 SafetySession/Input, 01-03 WindowHost variant registry, 01-09 HitCase.BarGeometry"
provides:
  - "DesktopHost.Find(): fresh lookup of the owner of SHELLDLL_DefView (Progman on this build), plus IsAbove and HasOrdinaryWindowAbove"
  - "Host variants S0, S1, S2 (ZOrderGuard) with transition logging and an optional raw WinEvent log"
  - "Driver case `zorder`: guard x trigger x repetition loop, gap sampler, cover test, z-order snapshots, per-run CSV, German summary table"
  - "All eleven D-10 triggers with registered restores; explorer, display and lock refuse to run without their --allow flag"
affects: [01-14, 01-17]

actuals:
  tokens: 23500
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Guards are host variants combined with H1 (`--variant H1,S1`); guard log rows use event + detail (`desktop-host`, `class=Progman hwnd=0x..`)"
    - "Every trigger registers a state-flagged undo on the SafetySession before its first input/registry/process/display call, so an undo never toggles twice"
    - "Screen readback for time-critical sampling is one BitBlt of the probe row (ProbeStrip), not GetPixel per probe"

key-files:
  created:
    - spikes/Common/DesktopHost.cs
    - spikes/Common/native/q2/NativeMethods.txt
    - spikes/WindowHost/Variants/ZOrderGuard.cs
    - spikes/Driver/Cases/ZOrderCase.cs
    - spikes/Driver/Cases/ZOrderTriggers.cs
  modified: []

key-decisions:
  - "Sampling cadence is one display refresh (16.7 ms at 60 Hz), not 10 ms: GetPixel and BitBlt both wait for the next frame, so ProbeStrip reads all five probes with one BitBlt (60 samples/s instead of 12)"
  - "While the watcher holds the bar lifted, 'desktop shown' is judged by 'no ordinary window above the desktop host', because 'host above bar' is false by construction once the bar is topmost"
  - "Alt+Tab ends with Escape before Alt up, so no window of the developer is switched to or restored"
  - "Real runs keep a list of the developer's visible windows and restore those still minimized, because the shell has a single Win+M undo list"

patterns-established:
  - "Driver zorder CLI: `zorder --guard S0|S1|S2[,..] --trigger <names>|all|none|manual --repeat N [--allow-display] [--allow-explorer] [--allow-lock] [--seconds N] [--dry-run] [--out csv] [--summary-md md]`"
  - "`--out` is the per-run result CSV; the event log goes next to it as `<out>-events.csv`"

requirements-completed: []

coverage:
  - id: D1
    description: "Layer strategies S0/S1/S2 exist as host variants; the desktop host is looked up fresh on every check; the guard starts and logs guard-start and desktop-host rows (class Progman on this build)"
    verification:
      - kind: other
        ref: "dotnet build spikes/WindowHost -c Release -warnaserror && dotnet build spikes/Driver -c Release -warnaserror && Driver.exe zorder --guard S0,S1,S2 --trigger none --repeat 1 --dry-run -> ZORDER DRY OK S0/S1/S2, exit 0"
        status: pass
    human_judgment: false
  - id: D2
    description: "Trigger matrix complete: --list-triggers prints exactly the eleven names; explorer, display and lock exit 2 without their --allow flag; nothing was fired in this plan"
    verification:
      - kind: other
        ref: "Driver.exe zorder --list-triggers | grep -c -x -E '<11 names>' -> 11; Driver.exe zorder --trigger explorer|display|lock --dry-run -> exit 2 with message"
        status: pass
    human_judgment: false
  - id: D3
    description: "The gap and in-front numbers of a real run are trustworthy (assumption A1: BitBlt on the screen DC returns the DWM-composed picture; the cadence of one refresh resolves 1-2 sample borderline cases)"
    verification: []
    human_judgment: true
    rationale: "Needs a real run (sentinel visible, witness on top), reserved for 01-13/01-14 after the hands-off checkpoint. The dry run only proves that BitBlt and GetPixel return the same five colours at rest."

duration: 11min
completed: 2026-10-10
plan_head_before: ded3b112c6708495d8c38f99c49d0d82fc1c4778
plan_head_after: ddd8fbdfdaef62647d9ba00519cf134c35d39c8a
commits: 2
status: complete
---

# Phase 1 Plan 10: Q2 Layer Strategies and the zorder Case Summary

**Three layer guards (S0 enforce-bottom, S1 WinEvent+timer watcher with temporary topmost, S2 re-insert above the desktop host) and a Driver `zorder` case that fires the full D-10 trigger matrix and measures gap, self-return and time in front of a witness; proven input-free with dry runs only.**

## Performance

- **Duration:** ~11 min (start recorded 2026-10-10T11:09:31Z)
- **Tasks:** 2 (1 tracer, 1 auto)
- **Files:** 5 created, 0 modified
- **Estimate vs actual:** the plan estimated 95000 tokens; the realized diff is about 23500 (chars/4).

## Accomplishments

- `DesktopHost.Find()` enumerates top-level windows and returns the one owning SHELLDLL_DefView (`Progman`, hwnd differs per boot), every call, no cached handle. `IsAbove(bar)` walks GW_HWNDPREV; `HasOrdinaryWindowAbove` answers "do the windows have come back" independent of the bar's own position.
- `ZOrderGuard.cs` (variants S0, S1, S2): S0 rewrites every z-order-changing WM_WINDOWPOSCHANGING to HWND_BOTTOM; S1 adds a `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS)` on the UI thread plus a 250 ms / 100 ms timer and lifts the bar to topmost only while the desktop is shown (placement flags `SWP_NOMOVE|NOSIZE|NOOWNERZORDER|NOACTIVATE|NOSENDCHANGING`); S2 re-inserts directly above the desktop host without topmost. All strategies register TaskbarCreated, WM_DISPLAYCHANGE and WTS session notifications. `--log-events true` logs every EVENT_SYSTEM_* and EVENT_OBJECT_REORDER event by name, hwnd and (redacted) class. The procedure was written from RESEARCH Pattern 2; no third-party source was opened.
- `ZOrderCase`: per guard x trigger x repetition it starts the bar (`--variant H1,<guard> --log-events true`) and a partial witness, runs the trigger, samples probes 1, 2, 5 for the gap, takes z-order snapshots at T+0/0.5/1/2 s (redacted CSV committed-safe, full CSV only under `spikes/results/private`), calls the trigger's undo, then runs the cover test on probe 3 for 3 s. Output: per-run CSV with the specified columns, an events CSV, and the German summary table with a D-11 classification (erfüllt / Grenzfall / verfehlt). Guard transitions and WinEvent counts are taken from the host CSV and aligned to the trigger moment through the host process start time.
- `ZOrderTriggers.cs`: win-d, corner (TaskbarSd read/set/restore, WM_SETTINGCHANGE "TraySettings", corner found from Shell_TrayWnd), win-m, peek (hover), peek-key (Win+comma), alt-tab, explorer, display (reg export backup, dynamic 1600x900 or 1280x720 at the same refresh rate, restore to the registry mode), lock (waits for the developer's unlock via OpenInputDesktop, sampling starts then), manual (nothing injected) and none. Nothing was fired in this plan.
- Dry run (`--dry-run`, only trigger `none`): starts bar and non-activating witness, asserts the `guard-start strategy=<guard>` and `desktop-host class=Progman|WorkerW` rows, exercises sampler (18 samples in 300 ms), cover test, analysis self-test, a redacted snapshot and the foreground-unchanged check, then prints `ZORDER DRY OK <guard>`. On this machine all three guards pass.

## Task Commits

1. **Task 1: End-to-end zorder with watcher S1 and no-op trigger (tracer)** - `9619a2b` (feat)
2. **Task 2: All D-10 triggers with registered restores** - `ddd8fbd` (feat)

Tracer gate: `<verify>` has only an automated part; it was re-run end-to-end after the commit (builds with `-warnaserror`, `ZORDER DRY OK S1`, header cell present) before Task 2 started.

## Files Created/Modified

- `spikes/Common/DesktopHost.cs` - `DesktopHost.Find/IsAbove/HasOrdinaryWindowAbove`, `DesktopHostInfo`
- `spikes/WindowHost/Variants/ZOrderGuard.cs` - variants S0/S1/S2
- `spikes/Driver/Cases/ZOrderCase.cs` - case, `ProbeStrip`, `GapSampler`, `CoverTest`, `ZContext`, `IZTrigger`, `WindowRestore`, `Q2Summary`
- `spikes/Driver/Cases/ZOrderTriggers.cs` - `ZTriggers` registry and the eleven triggers
- `spikes/Common/native/q2/NativeMethods.txt` - 24 names missing elsewhere (WinEvent constants, WM_DISPLAYCHANGE, WTS, BitBlt family, SendMessageTimeout, OpenInputDesktop, DEVMODEW, ShowWindow)

## Decisions Made

- Sampling cadence and the readback method (deviation 1).
- Lifted-state detection (deviation 2).
- `--guard-drop direct` host option: the default drop is the specified two-step (HWND_NOTOPMOST, then HWND_BOTTOM); `direct` uses one HWND_BOTTOM call. HWND_NOTOPMOST puts the bar on top of all normal windows for a moment, so 01-14 can compare both if the cover test reports in-front samples after the restore.
- Host rows use the existing `event,detail` columns, so `desktop-host class=Progman hwnd=0x..` is event `desktop-host` with detail `class=Progman hwnd=0x..`; the Driver reads them by event name.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The 10 ms sampling cadence cannot be reached with GetPixel**
- **Found during:** Task 1 (first dry run showed 4 samples in 150 ms)
- **Issue:** GetPixel on the screen DC costs one display refresh per call (measured: 5 probes = 12 samples/s, 1 probe = 60/s); a single BitBlt of a small strip also waits one refresh (60/s). 10 ms is below one frame of the 60 Hz display, so the picture cannot change between two faster samples anyway.
- **Fix:** `ProbeStrip` reads the whole probe row with one BitBlt + GetDIBits (all five probes of the same instant), giving 60 samples/s. The result reports the largest sample interval; "1 to 2 samples" in D-11 therefore means 1 to 2 display frames (16.7 to 33 ms). The dry run checks that the strip returns the same five colours as GetPixel.
- **Files modified:** spikes/Driver/Cases/ZOrderCase.cs, spikes/Common/native/q2/NativeMethods.txt
- **Commit:** 9619a2b

**2. [Rule 1 - Bug] "host above bar" is false once the watcher has lifted the bar**
- **Found during:** Task 1 (design of the S1/S2 state machine)
- **Issue:** The plan's detection (`IsAbove(bar)`) would flip to "hidden" right after the bar becomes topmost or is inserted above the host, so S1 and S2 would oscillate.
- **Fix:** Not lifted: `IsAbove(bar, host)`. Lifted: the desktop is still shown while no ordinary window (visible, not minimized, not cloaked, not topmost, not shell, not of the host process) lies above the desktop host (`DesktopHost.HasOrdinaryWindowAbove`). This is a heuristic to validate in 01-14 with the WinEvent log.
- **Files modified:** spikes/Common/DesktopHost.cs, spikes/WindowHost/Variants/ZOrderGuard.cs
- **Commit:** 9619a2b

**3. [Rule 2 - Missing critical functionality] Alt+Tab would switch to or restore a window of the developer**
- **Found during:** Task 2
- **Issue:** Alt down, Tab, 500 ms, Tab, Alt up selects the third entry of the switcher, which with everything minimized restores a minimized window of the developer.
- **Fix:** The sequence is Alt down, Tab, 500 ms, Tab, Escape (cancels the switcher), Alt up. The checks (bar still TOOLWINDOW, not foreground) are unchanged.
- **Files modified:** spikes/Driver/Cases/ZOrderTriggers.cs
- **Commit:** ddd8fbd

**4. [Rule 2 - Missing critical functionality] Win+M undo list is overwritten by the trigger**
- **Found during:** Task 2
- **Issue:** The shell keeps one undo list for Win+M. ClearTopArea's Win+M, then the trigger `win-m`, then the final Win+Shift+M would restore only the witness; the developer's windows would stay minimized (T-01-11).
- **Fix:** Before ClearTopArea a real run captures the developer's visible windows and registers a restore that runs after ClearTopArea's own and un-minimizes any of them still minimized (`WindowRestore`, SW_RESTORE, bottom first).
- **Files modified:** spikes/Driver/Cases/ZOrderCase.cs
- **Commit:** ddd8fbd

**5. [Rule 2 - Missing critical functionality] Corner retry must not restart Explorer without consent**
- **Found during:** Task 2
- **Issue:** The plan has the corner trigger restart Explorer when the corner click does not show the desktop; that is an `explorer` action.
- **Fix:** The restart happens only with `--allow-explorer`; otherwise `corner-inactive` and `corner-retry skipped` are logged and the run continues.
- **Files modified:** spikes/Driver/Cases/ZOrderTriggers.cs
- **Commit:** ddd8fbd

**6. [Rule 3 - Blocking] Result CSV and event log are separate files; Safety watch flag per trigger**
- **Issue:** The interface makes `--out` the per-run result CSV, but `CaseContext.Log` also writes to `--out`. Unlocking (lock) and manual keys are real input, which would abort a session that watches for it.
- **Fix:** The case writes its own result CSV and an events CSV next to it (`<out>-events.csv`); triggers expose `HumanParticipates` (manual, lock) and such runs open the SafetySession with `watchRealInput: false`. `IZTrigger` also has `RequiredFlag`, `SamplerStartsAfterFire` and `SampleMs`. Extra types (`ProbeStrip`, `WindowRestore`, `ZContext`) live in ZOrderCase.cs, no file outside the plan's list was added.
- **Files modified:** spikes/Driver/Cases/ZOrderCase.cs, spikes/Driver/Cases/ZOrderTriggers.cs
- **Commit:** 9619a2b, ddd8fbd

**Total deviations:** 6 (2 bugs, 3 missing-critical, 1 blocking). **Impact:** No change of scope. The measurement resolution is one display frame, which 01-14 has to quote when it classifies borderline cases; the lifted-state heuristic of S1/S2 is the one piece that real data must confirm.

## Issues Encountered

- The first dry run of the tracer failed its own sampler check (4 samples in 150 ms); that led to deviation 1. Afterwards all dry runs pass.
- On this machine the top area is covered by the developer's windows during dry runs (probes read #1E1E1E), as in 01-07; dry runs do not judge pixels.

## Known Stubs

None.

## Threat Flags

None beyond the plan. The new process creations (taskkill, explorer.exe, reg export) and the HKCU `TaskbarSd` write are the T-01-11 surface; each is gated by an `--allow-*` flag or by the explicit trigger choice and has a restore registered first. No packages were added (T-01-SC). The guard is written from the described procedure (T-01-12).

## Next Phase Readiness

Ready for 01-14 (real Q2 run after the hands-off checkpoint): `Driver zorder --guard S0,S1 --trigger all --repeat 5 --out spikes/results/q2-zorder.csv --summary-md spikes/results/q2-zorder.md`, plus explorer/display/lock with their flags and consent, and `--trigger manual --seconds 60` for the hand parts. Things 01-14 should check first: the baseline note in the result rows (sentinel visible, witness colour at probe 3), whether Win+D really raises the desktop host above the bar on build 26200 (`winevent` rows and `desktop-shown` rows), and S2 only if S1 fails D-11.

## Self-Check: PASSED

- Files exist: spikes/Common/DesktopHost.cs, spikes/Common/native/q2/NativeMethods.txt, spikes/WindowHost/Variants/ZOrderGuard.cs, spikes/Driver/Cases/ZOrderCase.cs, spikes/Driver/Cases/ZOrderTriggers.cs.
- Commits `9619a2b` and `ddd8fbd` are ancestors of HEAD.
- Acceptance criteria re-run: ZOrderGuard.cs contains SetWinEventHook, EVENT_SYSTEM_FOREGROUND, WINEVENT_SKIPOWNPROCESS, HWND_TOPMOST, HWND_NOTOPMOST, TaskbarCreated; the dry-run host CSV has a `desktop-host` row with `class=Progman`; DesktopHost.cs has no static field (the window list travels through a GCHandle); ZOrderTriggers.cs contains TaskbarSd, TraySettings, Shell_TrayWnd, ChangeDisplaySettingsEx, EnumDisplaySettings, LockWorkStation, OpenInputDesktop, `reg export`; in each trigger the OnRestore call precedes the first Input/registry/process/display call; explorer, display, lock exit 2 with a message without their flag.
- Plan verification: `--list-triggers` prints 11 names; dry runs S0, S1, S2 exit 0 with three `ZORDER DRY OK` lines; the summary has the `| Wächter |` header cell.
