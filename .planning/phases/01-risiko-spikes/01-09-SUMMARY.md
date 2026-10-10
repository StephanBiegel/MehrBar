---
phase: 01-risiko-spikes
plan: 09
subsystem: testing
tags: [hit-area, setwindowrgn, ws-ex-transparent, driver-cases, cswin32, spike-harness]

requires:
  - phase: 01-risiko-spikes
    provides: "01-03 HostWindow/H1 plate and host CSV rows, 01-04 Safety/Input, 01-07 HostProcess/Oracles/DesktopPrep"
provides:
  - "Host variants H2 (SetWindowRgn, optional region churn with region-cost rows) and H3 (WS_EX_TRANSPARENT toggled by a 16 ms cursor poll, h3-transparent rows)"
  - "Driver case `hit` (H1-H3): seeded click plan, host-log attribution, foreground evidence, mouse sweep, H2 region check + churn stats, H3 entry latency, German summary table"
  - "Driver case `focus` (witness|notepad) and `firstframe`; every case input-free in dry-run"
affects: [01-13, 01-17]

actuals:
  tokens: 15700
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Variant extras live in VariantProbes (Q1Cases.cs); the hit pipeline stays variant-agnostic"
    - "Right clicks are only sent when WindowFromPoint reports the bar under the cursor (T-01-10)"
    - "H3 entry latency = host process start time + ts_ms against the Driver wall clock"

key-files:
  created:
    - spikes/WindowHost/Variants/HitRegion.cs
    - spikes/Driver/Cases/Q1Cases.cs
    - spikes/Common/native/q1/NativeMethods.txt
  modified: []

key-decisions:
  - "Hit/focus/firstframe hosts run with --sentinel off, so dry runs and real runs show no magenta squares on the developer's screen (firstframe --manual keeps them on to make a flash visible)"
  - "Right clicks (focus case) are skipped unless WindowFromPoint reports the bar at the cursor; skipped count is a note in the result"
  - "Notepad (focus --target notepad) is moved below the bar (y=320) if it covers it, never closed, never given keystrokes"
  - "Safety timeout of the hit/focus real runs is computed from click count and double-click time (minimum 10 / 5 min) instead of a fixed 10 min"

patterns-established:
  - "Verify chain: build WindowHost + Driver with -warnaserror, then Driver hit/focus/firstframe dry runs"

requirements-completed: []

coverage:
  - id: D1
    description: "Variants H2 and H3 exist next to H1 (--list-variants prints H1, H2, H3); H2 window region equals the plate (GetWindowRgn from the Driver, SIMPLEREGION 650,8-1250,64) and the region churn produces region-cost rows; H3 poll logs h3-transparent"
    verification:
      - kind: other
        ref: "Driver.exe hit --variants H1,H2,H3 --clicks 10 --seed 2 --dry-run -> three 'HIT DRY OK' lines, exit 0"
        status: pass
    human_judgment: false
  - id: D2
    description: "Cases hit, focus, firstframe implemented with dry-run and manual modes; dry runs send no input"
    verification:
      - kind: other
        ref: "Driver.exe focus --target witness --cycles 3 --dry-run -> FOCUS DRY OK; Driver.exe firstframe --launches 2 -> FIRSTFRAME bottom=2/2; hit --manual --seconds 2 -> HIT MANUAL DONE"
        status: pass
    human_judgment: false
  - id: D3
    description: "Real-run paths (clicks, right clicks, sweep, entry latency, Notepad launch) behave as intended on the desktop"
    verification: []
    human_judgment: true
    rationale: "These paths send real input and move foreground windows; they are reserved for 01-13 after the hands-off checkpoint and were only reviewed, not executed"

duration: 12min
completed: 2026-10-10
plan_head_before: 075f1f307faecad5c9423419d2bd8619eef093ac
plan_head_after: acc2af0e90887593b01fda03f7f7942866ce024b
commits: 2
status: complete
---

# Phase 1 Plan 09: Q1 hit-area variants and cases Summary

**Hit-area instrument for Q1: variants H2 (SetWindowRgn) and H3 (WS_EX_TRANSPARENT cursor-poll toggle) plus the Driver cases `hit`, `focus` and `firstframe`, proven end-to-end in dry-run without touching the desktop.**

## Performance

- **Duration:** ~12 min (including an interruption between task commits and close-out)
- **Started:** 2026-10-10T10:30:06Z
- **Completed:** 2026-10-10
- **Tasks:** 2 (1 tracer, 1 auto)
- **Files:** 3 created

## Accomplishments

- `hit`: seeded click plan (N inside points 2 px from the border, N outside split over left/right/above/below 1 px off the plate and the alpha-0 overhang gap), alternating, consecutive outside clicks at least SM_CXDOUBLECLK/SM_CYDOUBLECLK apart, any two clicks at least GetDoubleClickTime()+100 ms apart (here 580 ms). Real run: BeginSafety, ClearTopArea, witness in the foreground, per click re-assert foreground, click, 250 ms, attribute by `lbuttondown` host rows (position within 2 px); outside passes carry foreground-left-witness as positive evidence and WindowFromPoint as extra data; a mouse sweep inside and along the gap compares host WM_MOUSEMOVE deltas. Writes per-click CSV rows (redacted classes) and the German summary table plus a per-category table.
- Dry-run results on this machine: `HIT DRY OK` for H1, H2, H3; the 20+20 plan covers all five outside categories (4 each); the foreground stayed unchanged. H2: `GetWindowRgn` from the Driver process works across processes (SIMPLEREGION, box 650:8:1250:64 equals the plate); the 5 s region churn delivered 156 `region-cost` rows (median 467 us, p95 3176 us in this unmeasured dry sample, not a result). H3: the poll runs and logs `h3-transparent on cursor=...`.
- `focus`: left click, right click, mouse move per cycle, foreground and focus window compared with the baseline after every action; right clicks only when the bar is under the cursor; Notepad target uses an empty `focus-test.txt` under `RepoPaths.PrivateResults`, is moved below the bar when needed, never closed and never fed keystrokes. Dry run prints `FOCUS DRY OK <variant>` (the notepad target only prepares the file).
- `firstframe`: N launches with `--exit-after-ms 1500`, counts host `first-frame ... bottom=true`; dry sample `FIRSTFRAME bottom=2/2` with the Driver-side check at READY also 2/2.

## Task Commits

1. **Task 1: hit case for H1 (tracer)** - `99196b4` (feat)
2. **Task 2: H2/H3 variants, focus and firstframe cases** - `acc2af0` (feat)

Tracer gate: the `<verify>` has only an automated part; it was re-run after the commit (builds with `-warnaserror`, `hit --variants H1 --clicks 20 --seed 1 --dry-run` -> `HIT DRY OK H1`, summary has `| Variante |`) before Task 2 started.

## Files Created/Modified

- `spikes/Driver/Cases/Q1Cases.cs` - ClickPlanner, VariantResult, Q1Summary, HitCase, VariantProbes, FocusCase, FirstFrameCase
- `spikes/WindowHost/Variants/HitRegion.cs` - `HitRegionVariant` ([HostVariant("H2","H3")]); options `region-churn`, `poll-ms`
- `spikes/Common/native/q1/NativeMethods.txt` - `GetDoubleClickTime`, `GetWindowRgn`, `GetRgnBox` (SetWindowRgn, CreateRectRgn, GetCursorPos, GetGUIThreadInfo were already in earlier lists)

## Decisions Made

See frontmatter `key-decisions`. For 01-13: run `hit` once per set of variants (`--variants H1,H2,H3 --clicks 100`), `focus` per target, `firstframe --launches 20`; H3 clicks follow `MoveAbs` + 20 ms, so a poll interval of 16 ms can legitimately miss the first click after entry, which is part of what Q1 measures.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] q1 CsWin32 list created in Task 1**
- **Found during:** Task 1
- **Issue:** `GetDoubleClickTime` (needed by the tracer) is listed in no existing topic list; the plan assigned the q1 list file to Task 2.
- **Fix:** Created `spikes/Common/native/q1/NativeMethods.txt` with that name in Task 1; Task 2 added `GetWindowRgn` and `GetRgnBox`.
- **Commit:** 99196b4, acc2af0

**2. [Rule 2 - Missing critical] Right-click guard and firstframe/focus `--manual`**
- **Issue:** The plan forbids right clicks outside the plate (T-01-10) but gives no mechanism for a plate that is not hit-testable; and "every case supports --manual" was only spelled out for `hit`.
- **Fix:** The focus case right-clicks only when WindowFromPoint reports the bar; `focus` and `firstframe` got `--manual`; `firstframe --dry-run` is accepted and equals a normal run (no input part exists).
- **Commit:** acc2af0

**Total deviations:** 2 auto-fixed (1 blocking, 1 missing critical). **Impact:** none on scope.

## Issues Encountered

None. Real-run paths were not executed (hands-off rule); they were reviewed and are covered by the 01-13 runs.

## Known Stubs

None.

## Threat Flags

None. T-01-10 is implemented as planned (single left clicks outside, spacing rules, no outside right click); T-01-SC: no packages added.

## Next Phase Readiness

01-13 can run `hit`, `focus` and `firstframe` for real after the hands-off checkpoint. Open point for 01-13: assumption A2 (what WindowFromPoint reports over alpha-0 pixels) decides whether the focus case skips right clicks.

## Self-Check: PASSED

- Files exist: Q1Cases.cs, HitRegion.cs, q1/NativeMethods.txt.
- Commits `99196b4` and `acc2af0` are ancestors of HEAD.
- Acceptance criteria re-run: Q1Cases.cs contains `GetDoubleClickTime`, `SM_CXDOUBLECLK`, `DriverCase("focus")`, `DriverCase("firstframe")`, `focus-test.txt` under `RepoPaths.PrivateResults`; `BeginSafety` precedes the first `Input.` call; no `CloseMainWindow`/`Kill`/`Input.Key` in Q1Cases.cs; HitRegion.cs contains `SetWindowRgn`, `CreateRectRgn`, `WS_EX_TRANSPARENT`, `GetCursorPos`, `region-cost`; `WindowHost.exe --list-variants` prints H1 H2 H3; dry CSV lists 20 inside and 20 outside points (4 per outside category).
- Plan verification: `hit` H1-H3 dry -> three `HIT DRY OK`; `focus --target witness --cycles 3 --dry-run` -> `FOCUS DRY OK H1`; `firstframe --launches 2` -> `FIRSTFRAME bottom=2/2`.
