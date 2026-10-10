---
phase: 01-risiko-spikes
plan: 07
subsystem: testing
tags: [driver, oracles, pixel-probe, z-order, host-process, cswin32, spike-harness]

requires:
  - phase: 01-risiko-spikes
    provides: "01-03 WindowHost (READY line, host CSV, witness role), 01-04 Safety/Input (SafetySession.OnRestore, Input.Chord), 01-02 Driver skeleton"
provides:
  - "HostProcess: launches WindowHost, waits (10 s) for `READY hwnd=0x.. pid=.. role=..`, tails the host CSV (ReadNewRows, ReadAllRows, WaitForRow), Kill/Dispose"
  - "CaseContext.Host: Log (RunLog), StartHost(args), StartWitness(cover, title, activate); every started host is killed at process exit and registered as a SafetySession restore step"
  - "Oracles: PixelProbe (GetDC/GetPixel, Matches), ZOrder (Snapshot, Below, ToRedactedCsv, ToPrivateCsv, WindowAt), Foreground.Take (GetGUIThreadInfo)"
  - "DesktopPrep.ClearTopArea (restore registered before Win+M) and BringToForeground (Alt-tap fallback)"
  - "Driver cases `smoke` (no input, passes on this machine) and `selftest` (dry-run proven; real run reserved for 01-13)"
affects: [01-08, 01-09, 01-10, 01-11, 01-12, 01-13, 01-17]

actuals:
  tokens: 12800
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Types that expose CsWin32 handles (HWND) are internal: HostProcess, ZEntry, ForegroundInfo, DesktopPrep, and the CaseContext members StartHost/StartWitness"
    - "Committed output of window inventories goes through ZOrder.ToRedactedCsv (foreign windows as class `other`); ToPrivateCsv writes the file itself and only into RepoPaths.PrivateResults"
    - "Desktop host = top-level window owning SHELLDLL_DefView, looked up on every snapshot (Progman on this machine, build 26200)"

key-files:
  created:
    - spikes/Common/Oracles.cs
    - spikes/Common/native/oracles/NativeMethods.txt
    - spikes/Driver/HostProcess.cs
    - spikes/Driver/CaseContext.Host.cs
    - spikes/Driver/DesktopPrep.cs
    - spikes/Driver/Cases/SmokeCase.cs
    - spikes/Driver/Cases/OracleCases.cs
  modified:
    - spikes/WindowHost/WitnessWindow.cs

key-decisions:
  - "Witness gets `--activate false` (ShowActivated off) so dry runs and smoke never take the foreground from the developer"
  - "Started hosts are cleaned up through AppDomain.ProcessExit plus a SafetySession restore step; Program.cs (owned by 01-02) stays untouched"
  - "ToPrivateCsv takes a file name and writes inside RepoPaths.PrivateResults itself, so a caller cannot put a full window inventory anywhere committable"

patterns-established:
  - "Driver verify command: dotnet build spikes/WindowHost -c Release -warnaserror && dotnet build spikes/Driver -c Release -warnaserror && Driver.exe smoke | grep 'SMOKE OK'"
  - "Z-order criteria exclude own windows: WPF puts a hidden `HwndWrapper[WindowHost;;guid]` window directly below the bar (first-frame row `below=HwndWrapper[..]`)"

requirements-completed: []

coverage:
  - id: D1
    description: "Driver launches the bar without input and confirms from outside: READY, ex-style TOOLWINDOW|NOACTIVATE (0x08080080), rectangle 10,0 1900x250 equal to SpikeGeometry, nothing visible and foreign below the bar, foreground unchanged, host rows self-check bottom=true and first-frame"
    verification:
      - kind: other
        ref: "dotnet build spikes/WindowHost -c Release -warnaserror && dotnet build spikes/Driver -c Release -warnaserror && Driver.exe smoke -> SMOKE OK, exit 0 (run three times)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Oracle plumbing: five probes sampled, z-order snapshot contains the bar and the desktop host (Progman), redacted snapshot carries no foreign class, process or title, no new tracked file under spikes/results"
    verification:
      - kind: other
        ref: "Driver.exe selftest --dry-run -> SELFTEST DRY OK, exit 0; Driver.exe --list | grep -c -x -E 'smoke|selftest|safety-selftest|stats-selftest' -> 4; git status --porcelain spikes/results -> empty"
        status: pass
    human_judgment: false
  - id: D3
    description: "Pixel oracle really separates visible from covered (assumption A1: GetPixel on the screen DC returns the DWM-composed image)"
    verification: []
    human_judgment: true
    rationale: "Needs the real selftest run (Win+M, witness on top) which is reserved for plan 01-13 after the hands-off checkpoint; this plan only proves the plumbing in dry-run"

duration: 25min
completed: 2026-10-10
plan_head_before: 8f09b1975f8b4129b9228ec09d60c500a1229089
plan_head_after: 1fc061c83bbeb87404ecd3c77cf6797ca7471966
commits: 2
status: complete
---

# Phase 1 Plan 07: Driver Oracles and Smoke Summary

**Two-process harness completed on the Driver side: HostProcess/READY handshake, pixel + z-order + foreground oracles with redacted/private output split, DesktopPrep with restore-before-minimize, and an input-free `smoke` case that passes against the real bar on build 26200.**

## Performance

- **Duration:** ~25 min (start time was not recorded at the beginning; estimated)
- **Completed:** 2026-10-10
- **Tasks:** 2 (1 tracer, 1 auto)
- **Files:** 7 created, 1 modified

## Accomplishments

- `Driver smoke` launches the bar (`--exit-after-ms 4000`, killed after the checks) and verifies from outside, with no input: READY handshake, `WS_EX_TOOLWINDOW|WS_EX_NOACTIVATE` (exstyle 0x08080080), window rectangle equals SpikeGeometry (10,0 1900x250), nothing visible, non-cloaked and foreign below the bar, foreground unchanged after show and after the checks, host rows `self-check ... bottom=true` and `first-frame`. Output ends with `SMOKE OK`.
- On this machine the bar sits at z-index 270 of 390 top-level windows; the desktop host is `Progman` at 272 (it owns SHELLDLL_DefView); 18 further `WorkerW` windows lie above the bar and are irrelevant. Directly below the bar is the host's own hidden WPF helper window, which is why own windows are excluded from the blocker test.
- `Oracles.cs`: `PixelProbe` (Sample via GetDC(NULL)/GetPixel, COLORREF to 0xRRGGBB, Matches with +-8 tolerance), `ZOrder` (Snapshot via EnumWindows, Below via GW_HWNDNEXT, DWMWA_CLOAKED, desktop host looked up fresh each time), `Foreground.Take` (GetForegroundWindow + GetGUIThreadInfo focus window).
- `selftest --dry-run`: starts bar and a non-activating witness, samples five probes (read #1E1E1E: the developer's own window covers the top area, as expected), takes one snapshot, asserts 5 samples + bar + desktop host, prints `SELFTEST DRY OK`. The real run (bar uncovered => visible >= 4/5, witness on top => cover colour at 5/5, WindowFromPoint as extra data) is implemented but deliberately not executed; it belongs to 01-13.
- `DesktopPrep.ClearTopArea` registers the Win+Shift+M restore on the session before it sends Win+M; `BringToForeground` uses AllowSetForegroundWindow and, if the foreground lock refuses and a session exists, one synthetic Alt tap. Neither was executed in this plan.

## Task Commits

1. **Task 1: End-to-end Driver -> Host (tracer)** - `fe3a2a6` (feat)
2. **Task 2: Oracles, DesktopPrep, selftest** - `1fc061c` (feat)

Tracer gate: `<verify>` has only an automated part, so it was re-run end-to-end after the commit (builds with `-warnaserror`, `smoke` -> `SMOKE OK`) before Task 2 started.

## Files Created/Modified

- `spikes/Common/Oracles.cs` - PixelProbe, ZOrder, ZEntry, Foreground, ForegroundInfo
- `spikes/Common/native/oracles/NativeMethods.txt` - `IsWindow`, `GUITHREADINFO`, `GetAncestor`, `GET_ANCESTOR_FLAGS`, `ASFW_ANY` (everything else was already in base/host/input)
- `spikes/Driver/HostProcess.cs` - launch, READY parse, CSV tailing, kill
- `spikes/Driver/CaseContext.Host.cs` - Log, StartHost, StartWitness, Dispose
- `spikes/Driver/DesktopPrep.cs` - ClearTopArea, BringToForeground
- `spikes/Driver/Cases/SmokeCase.cs`, `spikes/Driver/Cases/OracleCases.cs` - cases `smoke`, `selftest`
- `spikes/WindowHost/WitnessWindow.cs` - `--activate false` option

## Decisions Made

- Witness `--activate false` (see deviation 2); smoke needs no witness, dry-run selftest uses it.
- Cleanup of started hosts via `AppDomain.ProcessExit` + `SafetySession.OnRestore` instead of editing `Program.cs` (belongs to 01-02; every Driver run is exactly one case).
- Redaction rule: a window is kept verbatim in committed CSVs only if it is the bar, one of our own processes (WindowHost, Driver) or class Progman/WorkerW/Shell_TrayWnd; everything else is class `other` with no process and no title.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Oracles.cs and its NativeMethods list land in the Task 1 commit**
- **Found during:** Task 1
- **Issue:** `smoke` (Task 1) needs the z-order snapshot and the foreground oracle that the plan assigns to Task 2, so the tracer could not compile or run without them.
- **Fix:** Created `Oracles.cs` and `native/oracles/NativeMethods.txt` in Task 1; Task 2 extended `Oracles.cs` (`ZOrder.WindowAt`) and added DesktopPrep and `selftest`.
- **Files modified:** spikes/Common/Oracles.cs, spikes/Common/native/oracles/NativeMethods.txt
- **Commit:** fe3a2a6

**2. [Rule 1 - Bug] Dry runs would have taken the foreground (witness ShowActivated = true)**
- **Found during:** Task 2 (dry-run requirement "no foreground change")
- **Issue:** `WitnessWindow` always showed itself activated, so a dry-run witness would have stolen the focus from the developer's active window, contradicting the plan and the "do not disturb" constraint of this run.
- **Fix:** New host option `--activate false` (default unchanged), used by `StartWitness(..., activate: false)`. Verified: `foreground-unchanged,true` row in the dry-run CSV.
- **Files modified:** spikes/WindowHost/WitnessWindow.cs (not in the plan's file list)
- **Commit:** fe3a2a6

**3. [Rule 3 - Blocking] Contract visibility and additive members**
- CsWin32 handle types are internal, so `HostProcess`, `StartHost`, `StartWitness`, `ZEntry`, `ForegroundInfo`, `DesktopPrep` are `internal` (consumers are in the same assembly). `ZEntry` has two trailing extra members (`Title`, `Rect`); `ToPrivateCsv(entries, fileName)` writes the file itself; extras: `ZOrder.Below`, `ZOrder.WindowAt`, `ZOrder.IsBlocker`, `HostProcess.ReadNewRows/ReadAllRows/WaitForRow`, `StartWitness(..., activate)`. All names in the plan's interface block exist; nothing was removed.

**Total deviations:** 3 (1 bug, 2 blocking). **Impact:** none on scope; downstream plans 01-09..01-12 use the contracts as named.

## Issues Encountered

- `PInvoke.WindowFromPoint` takes `System.Drawing.Point` (CsWin32 maps POINT to it), not a Foundation `POINT`; fixed at build time.
- Hints for 01-10/01-13: the machine has ~18 `WorkerW` windows, so "the" desktop host must be found through SHELLDLL_DefView (done in `ZOrder`), never by class name alone. The first-frame `below=` value names the host's own helper window, not a foreign one.

## Known Stubs

None.

## Threat Flags

None. The only new process creation is `HostProcess` starting the locally built `WindowHost.exe` from `RepoPaths.HostExe`; no new network, auth or trust-boundary surface. T-01-04 (redaction) and T-01-29 (restore before Win+M) are implemented as planned; T-01-SC: no packages added.

## Next Phase Readiness

Ready for 01-08 (no shared files) and the Q1/Q2/Q3/Q4 case plans 01-09..01-12, which add only their own files on top of these contracts. The real oracle self-test (assumption A1) is the first thing 01-13 has to run after the hands-off checkpoint: `Driver.exe selftest` (expects `SELFTEST visible>=4/5 covered=5/5`).

## Self-Check: PASSED

- Files exist: Oracles.cs, native/oracles/NativeMethods.txt, HostProcess.cs, CaseContext.Host.cs, DesktopPrep.cs, SmokeCase.cs, OracleCases.cs, WitnessWindow.cs (all present on disk).
- Commits `fe3a2a6` and `1fc061c` are ancestors of HEAD.
- Acceptance criteria re-run: HostProcess parses `READY hwnd=` and kills on Dispose; CaseContext.Host contains `partial class CaseContext`, `StartHost`, `StartWitness`; smoke host CSV has a `first-frame` row; Oracles.cs contains `GetPixel`, `EnumWindows`, `DWMWA_CLOAKED`, `SHELLDLL_DefView`, `ToRedactedCsv`, `ToPrivateCsv`; in DesktopPrep.cs `OnRestore` (line 31) precedes the first `Chord` (line 33); `git status --porcelain spikes/results` is empty after the dry run.
- Plan verification: `Driver.exe smoke` -> SMOKE OK; `Driver.exe selftest --dry-run` -> SELFTEST DRY OK; four harness cases registered.
