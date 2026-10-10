---
phase: 01-risiko-spikes
plan: 03
subsystem: testing
tags: [wpf, cswin32, window-host, z-order, spike-harness, hwnd-bottom]

requires:
  - phase: 01-risiko-spikes
    provides: "Plan 01-02 spike skeleton (CsWin32 base list, RepoPaths, WindowHost/Driver projects)"
provides:
  - "WindowHost bar role: transparent ~1900x250 window, WS_EX_TOOLWINDOW|WS_EX_NOACTIVATE, HWND_BOTTOM already at the first frame, WM_MOUSEACTIVATE -> MA_NOACTIVATE"
  - "Host CSV rows lbuttondown/rbuttondown/rbuttonup/mouseactivate (immediate), counters (250 ms), self-check, first-frame, render-mode, pinned"
  - "IHostVariant / HostVariantAttribute / HostContext / VariantRegistry (reflection) so plans 01-09..01-12 only add files under Variants/"
  - "Variant H1 (alpha-1 plate + five sentinels) and reusable PlateDrawing.Add"
  - "Witness role (cyan, activatable, cover full/partial/none, activation and focus log)"
  - "Common: SpikeGeometry (IntRect, probes, witness rectangles, colours) and RunLog (CSV ts_ms,case,variant,guard,event,detail)"
  - "Full CLI: --role --variant --size --pos --transparent --render --sentinel --cover --title --log --exit-after-ms --list-variants --show-mode"
affects: [01-04, 01-07, 01-09, 01-10, 01-11, 01-12, 01-17]

actuals:
  tokens: 18000
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Host types are internal: CsWin32 types (HWND, RECT) are internal, so any public API exposing them does not compile"
    - "EnumWindows with allowMarshaling false takes a function pointer: [UnmanagedCallersOnly] static method plus a [ThreadStatic] collector"
    - "Host-side CsWin32 names live in spikes/Common/native/host/NativeMethods.txt; merging with the base list builds cleanly in WindowHost and Driver"

key-files:
  created:
    - spikes/Common/SpikeGeometry.cs
    - spikes/Common/RunLog.cs
    - spikes/Common/native/host/NativeMethods.txt
    - spikes/WindowHost/HostWindow.cs
    - spikes/WindowHost/VariantRegistry.cs
    - spikes/WindowHost/Variants/PlateH1.cs
    - spikes/WindowHost/WitnessWindow.cs
  modified:
    - spikes/WindowHost/App.xaml.cs

key-decisions:
  - "Initial show is pinned to the bottom by rewriting WPF's own SWP_SHOWWINDOW call in WM_WINDOWPOSCHANGING (until the first frame is logged); --show-mode wpf keeps the unmodified WPF behaviour as baseline"
  - "All host types are internal (CsWin32 types are internal); only App stays public because of the XAML partial"
  - "READY is printed on ContentRendered, i.e. after self-check and first-frame are in the CSV, so a driver that sees READY can rely on those rows"
  - "Environment.ExitCode is set in App.OnExit because the generated WPF Main discards Run()'s result"

patterns-established:
  - "Host verify command: dotnet build spikes/WindowHost -c Release -warnaserror, then WindowHost.exe ... --exit-after-ms N | grep READY"

requirements-completed: []

coverage:
  - id: D1
    description: "Bar window host: TOOLWINDOW|NOACTIVATE set, HWND_BOTTOM applied, self-check bottom=true and first-frame bottom=true (zindex 271, nothing visible below the bar), exactly one READY line, unknown variant exits 2, --list-variants prints H1"
    verification:
      - kind: other
        ref: "dotnet build spikes/WindowHost -c Release -warnaserror && WindowHost.exe --variant H1 --log spikes/results/dry/host-tracer.csv --exit-after-ms 2500 (READY count 1, self-check+first-frame rows 2, bottom=true 2)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Witness role prints READY with role=witness; placement full/partial/none logged; activation and keyboard focus rows written; option passthrough (--render sw, --transparent false, --size) works and the render-mode row is logged"
    verification:
      - kind: other
        ref: "WindowHost.exe --role witness --cover none --exit-after-ms 1000 (READY count 1); WindowHost.exe --render sw --transparent false --size 1000x160 (render-mode row: effective=software tier=2)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Host actually keeps focus away from other applications and passes clicks through outside the plate (needs input)"
    verification: []
    human_judgment: true
    rationale: "No input was sent in this plan by design; the Driver oracles (01-04, 01-07) and the Q1 spike plans measure click catch, pass-through and focus"

duration: 13min
completed: 2026-10-10
plan_head_before: a73dacc9cd494dd5c2c7ee81365cb2f9a9093698
plan_head_after: 30925250391548361ffc78c0dc0747e4c497e858
commits: 2
status: complete
---

# Phase 1 Plan 03: Window host (bar and witness) Summary

**Host side of the two-process spike harness: a transparent, non-activating WPF bar window that is at HWND_BOTTOM from its very first frame (via an initial-show z-order rewrite, because plain WPF Show() lifts it to z-index 21), baseline variant H1 with alpha-1 plate and five sentinels, a reflection-based variant registry, a cyan witness role and the shared geometry/CSV-log helpers.**

## Performance

- **Duration:** 13 min
- **Started:** 2026-10-10T07:55Z (approx.)
- **Completed:** 2026-10-10T08:08Z
- **Tasks:** 2
- **Files:** 8 (7 created, 1 modified)

## Accomplishments

- **Measured Q1 input for 01-09 (first appearance):** with a plain WPF `Show()` the bar is NOT at the bottom at its first frame, even though `SetWindowPos(HWND_BOTTOM)` ran in `OnSourceInitialized` and the self-check reported `bottom=true` at that moment. WPF issues its own `SetWindowPos(SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE|SWP_SHOWWINDOW)` with a `hwndInsertAfter` that is not `HWND_BOTTOM`; the first-frame row then reads `zindex=21 ... bottom=false blocking=Chrome_WidgetWin_1` (reproduced in 3 of 3 runs, both with and without `SWP_SHOWWINDOW` of our own). Rewriting that call to `HWND_BOTTOM` in `WM_WINDOWPOSCHANGING` (only until the first frame is logged) gives `zindex=271 ... bottom=true blocking=none` (reproduced 4 of 4 runs). `--show-mode wpf` retains the unmodified behaviour so the Q1 note can cite both. Whether the raise is visible (flash) is still the manual part (D-06).
- Host logs everything the oracles rely on: immediate button/mouse-activate rows with message-time screen coordinates, `counters` every 250 ms when changed, `self-check` (ex-style flags, bottom, class below), `first-frame` (EnumWindows index, next window class, bottom, blocking class), `render-mode` (effective, tier, requested), `pinned`.
- Witness role works for cover full/partial/none (rectangles `610,0,700,120` and `910,0,260,120` at the default bar position x=10) and logs `activate` (WM_ACTIVATE state) and keyboard focus.
- Merging a second CsWin32 topic list (`native/host`) with the base list builds cleanly in both WindowHost and Driver under `-warnaserror` (the open question from 01-02; 01-04 will add a third list).
- `Driver` still builds under `-warnaserror` with the new Common files; `stats-selftest` passes.

## Task Commits

1. **Task 1: End-to-end bar at the bottom (tracer)** - `c372406` (feat)
2. **Task 2: Witness role and option passthrough** - `3092525` (feat)

Tracer gate: `human_verify_mode` is `end-of-phase` and the tracer `<verify>` is automated-only, so it was re-run (build with `-warnaserror`, host run with READY/self-check/first-frame/bottom=true checks) before the commit; it passed ("Tracer verified end-to-end - expanding").

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] WPF Show() lifts the bar above other windows at the first frame**
- **Found during:** Task 1 (first tracer run; self-check said `bottom=true`, first-frame said `bottom=false`)
- **Issue:** Must-have "placed at HWND_BOTTOM before the first content frame" was not met with the planned `OnSourceInitialized` SetWindowPos alone. A diagnostic hook showed WPF's `SWP_SHOWWINDOW` call with a non-bottom `hwndInsertAfter`.
- **Fix:** `SWP_SHOWWINDOW` in our own SetWindowPos plus a `WM_WINDOWPOSCHANGING` rewrite to `HWND_BOTTOM` until the first frame is logged (`pinned` row). New option `--show-mode swp|wpf` (default swp) keeps the plain-WPF baseline. Added `WINDOWPOS` to the host name list.
- **Files modified:** `spikes/WindowHost/HostWindow.cs`, `spikes/Common/native/host/NativeMethods.txt`
- **Verification:** first-frame `bottom=true` in swp mode (4/4), `bottom=false` in wpf mode (3/3).
- **Commit:** `c372406`

**2. [Rule 3 - Blocking] CsWin32 types are internal**
- **Found during:** Task 1 (first build, CS0053/CS0051)
- **Issue:** Public host types exposed `HWND`/`RECT`.
- **Fix:** Host types made `internal` (App stays public for the XAML partial). `EnumWindows` needed a function pointer (`[UnmanagedCallersOnly]` collector) instead of a lambda.
- **Files modified:** `HostWindow.cs`, `VariantRegistry.cs`, `Variants/PlateH1.cs`, `App.xaml.cs`
- **Commit:** `c372406`

**3. [Rule 1 - Bug] Witness never printed READY**
- **Found during:** Task 2 (verify)
- **Issue:** `ContentRendered` does not fire for a window without content.
- **Fix:** Witness gets an empty `Grid` as content.
- **Files modified:** `spikes/WindowHost/WitnessWindow.cs`
- **Commit:** `3092525`

**4. [Rule 1 - Plan defect] `render-mode effective=` grep cannot match a well-formed CSV row**
- **Found during:** Task 2 (verify)
- **Issue:** The plan's second `<automated>` greps `render-mode effective=` (space). The interface block defines the CSV columns `...,event,detail`, so the row is `...,render-mode,effective=...`; the literal grep always prints 0. The `self-check`/`first-frame` greps in the plan already use the comma form.
- **Fix:** Kept the correct CSV layout; verified with `grep -c "render-mode,effective="` (prints 1). Downstream plans should match the comma form or parse the CSV.
- **Commit:** `3092525`

### Process note

**5. [Process] Protected-branch assertion not applied** - dispatch states this is a sequential executor on `main` (branching_strategy none), as in 01-01/01-02. `.planning/config.json` not touched.

---

**Total deviations:** 4 auto-fixed (3 Rule 1, 1 Rule 3), 1 process note.
**Impact on plan:** Deviation 1 changes the host default (pinning) but is what the must-have demands; baseline preserved via `--show-mode wpf`. Interfaces are otherwise as named (extras: `--show-mode`, `pinned` row, `requested=` in render-mode, `bottom=`/`blocking=`/`flags=` in the detail fields; Options contains all parsed keys, not only unknown ones).

## Issues Encountered

- During verification the witness and bar windows appeared briefly on the live desktop (1-2.5 s each, witness activated once per run); no input was sent.
- `below=` in `first-frame` is the raw next window in EnumWindows order, which is the app's own hidden `HwndWrapper[...]` window; `blocking=` is the first visible, uncloaked, non-Progman/WorkerW window below and is what `bottom=` is derived from.
- Git prints LF-to-CRLF warnings (autocrlf); harmless.

## Known Stubs

None. The `Variants/` folder holds only H1 by design; later plans add variants.

## Threat Flags

None. T-01-26: every run used `--exit-after-ms`, the bar never activates (`ShowActivated` false, `MA_NOACTIVATE`), no input is sent. T-01-SC: no packages added.

## Next Phase Readiness

- Driver plans can start the host via the CLI and the `READY hwnd=0x... pid=... role=...` line; CSV rows are flushed per line and the file is shared-read.
- 01-09 (Q1) should use `--show-mode wpf` vs default for the first-appearance comparison and still needs the manual flash judgement.
- Not yet exercised (needs input): click catch, pass-through outside the plate, foreground retention, the witness against the bar.

## Self-Check: PASSED

- Files exist: all 8 listed under key-files.
- Commits `c372406` and `3092525` are ancestors of HEAD.
- Acceptance criteria re-run: HostWindow.cs contains WS_EX_NOACTIVATE/WS_EX_TOOLWINDOW/HWND_BOTTOM/MA_NOACTIVATE/self-check/first-frame; PlateH1.cs contains `HostVariant("H1")` and `FromArgb(1, 0, 0, 0)`; `--list-variants` prints H1; WitnessWindow.cs contains CoverRgb/#00FFFF, WM_ACTIVATE and READY path (via App); `RenderOptions.ProcessRenderMode` is set before the first window.
- Plan-level verification: tracer run READY 1, self-check+first-frame 2, bottom=true 2; witness READY role=witness 1; options run exit 0 with render-mode row.

---
*Phase: 01-risiko-spikes*
*Completed: 2026-10-10*
