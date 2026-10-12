---
phase: 01-risiko-spikes
plan: 12
subsystem: testing
tags: [render-cost, wpf, drawingvisual, onrender, dwm, sendinput, presentmon, spike-harness]

requires:
  - phase: 01-risiko-spikes
    provides: "01-03 WindowHost variant registry and render-mode row, 01-04 SafetySession/Input, 01-07 HostProcess/StartHost/DesktopPrep"
provides:
  - "Host variants R1 (one OnRender redraw per frame) and R2 (retained DrawingVisual per icon, transform-only updates) for the transparent 1900x250 window, with synthetic 256 px Pbgra32 icons"
  - "Per-frame host log (qpc_ms, RenderingTime, duplicate flag, handler/OnRender microseconds, GC counts, allocation delta) and idle-proof cursor-gated Rendering subscription"
  - "Driver case `render`: synthetic, real SendInput sine sweep (8 ms steps) and manual mode; CPU of host and dwm.exe via GetProcessTimes, DWM timing counters, one summary row per cell"
  - "spikes/tools/run-render-matrix.ps1 (D-17 matrix, control K, fallback sizes, -DryRun, -PresentMon) and spikes/tools/get-presentmon.ps1 (SHA-256 gated)"
affects: [01-16, 01-17]

actuals:
  tokens: 19000
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Host and Driver share the system-wide QueryPerformanceCounter clock (Stopwatch ticks in ms): the host frames CSV carries qpc_ms, the Driver places warm-up / measured / idle windows on the same clock, so no handshake is needed"
    - "Synthetic runs: host option synthetic-stop-ms is relative to a `synthetic-origin` host row (qpc_ms) that the Driver reads after READY"
    - "A real (input) path can be rehearsed without any input: `render --record-input` swaps the sink for a RecordingSink and turns the real-input watcher off"

key-files:
  created:
    - spikes/WindowHost/Variants/SyntheticIcons.cs
    - spikes/WindowHost/Variants/RenderCost.cs
    - spikes/Driver/Cases/RenderCase.cs
    - spikes/Common/native/q4/NativeMethods.txt
    - spikes/tools/run-render-matrix.ps1
    - spikes/tools/get-presentmon.ps1
  modified: []

key-decisions:
  - "Sweep waits use a high-resolution waitable timer plus a short spin: timeBeginPeriod(1) was measured ineffective for the windowless Driver on Windows 11 (13.45 ms median step instead of 8 ms; with the waitable timer 8.00 ms median, p95 8.80 ms)"
  - "pm_rows is written as n/a by the Driver and patched in by the matrix script, which is the only party that knows the PresentMon output"
  - "PresentMon --timed covers warmup+seconds+idle+10 s, not warmup+seconds, because PresentMon's timer starts at its own launch, before the host exists"

patterns-established:
  - "Render cell command: Driver.exe render --variant R1|R2 --count N --render hw|sw [--transparent false] [--size WxH] --synthetic --out csv (no input); dry matrix: run-render-matrix.ps1 -DryRun -Label x"

requirements-completed: []

coverage:
  - id: D1
    description: "R1 and R2 render variants, both modes (hw / SoftwareOnly via --render sw, effective mode read back from the host), control K (--transparent false), 20 and 64 icons run end to end and produce a statistics row without moving the real mouse"
    verification:
      - kind: other
        ref: "Driver.exe render --variant R1 --count 20 --render hw --synthetic ... -> RENDER R1 20 hw frames=275..291 idle_frames=0; R2 64 sw frames=223 effective_mode=software; R1 20 hw --transparent false frames=176"
        status: pass
    human_judgment: false
  - id: D2
    description: "Real sweep path (BeginSafety, ClearTopArea, sine path y=36 around the window centre, park below the bar) rehearsed against a RecordingSink: 899 moves, first move:981,36, last move:960,330, Win+M recorded, step interval p50 8.00 ms"
    verification:
      - kind: other
        ref: "Driver.exe render --variant R2 --count 64 --render hw --seconds 6 --warmup 1 --idle 1 --record-input -> INPUT RECORDED ... nothing was sent, exit 0"
        status: pass
    human_judgment: false
  - id: D3
    description: "Matrix runner and PresentMon gate: dry matrix writes a data row, -PresentMon is refused without elevation (exit 2), the fetch script holds the full digest and Get-FileHash, the target path is git-ignored, both scripts are ASCII"
    verification:
      - kind: other
        ref: "run-render-matrix.ps1 -DryRun -Label dry -> exit 0, spikes/results/dry/q4-dry-summary.csv has the R1 row; run-render-matrix.ps1 -PresentMon -> REFUSED, exit 2; get-presentmon.ps1 -CheckOnly -> MISSING, exit 3; git check-ignore exit 0"
        status: pass
    human_judgment: false
  - id: D4
    description: "The download path of get-presentmon.ps1 (GitHub API lookup, asset download, SHA-256 comparison) and the PresentMon 2.6.0 command-line flag names"
    verification: []
    human_judgment: true
    rationale: "The dispatch forbade downloading or running PresentMon in this plan; the hash gate and the flags are first exercised in 01-16 (run get-presentmon.ps1, then PresentMon --help)"

duration: 20min
completed: 2026-10-12
plan_head_before: f6b48374aaa72bd5dfec75ed902a6558cfd484d6
plan_head_after: 5d2b7bdfbaef8b71752ddd30668c959b7ba7ec67
commits: 3
status: complete
---

# Phase 1 Plan 12: Q4 Render-Cost Instrument Summary

**Render-cost instrument for the transparent bar window: R1 (OnRender per frame) and R2 (retained DrawingVisuals with transform-only updates) with a per-frame host log, a Driver `render` case that condenses it with host/dwm.exe CPU and DWM counters into one CSV row, a matrix runner and a SHA-256 gated PresentMon fetch. Only synthetic and recorded-input runs were executed; nothing moved the real mouse.**

## Performance

- **Duration:** ~20 min (start time not recorded at the beginning; from the commit times 09:19 to 09:39)
- **Completed:** 2026-10-12
- **Tasks:** 3 (1 tracer, 2 auto)
- **Files:** 6 created

## Accomplishments

- **Workload and variants.** `RenderCostOptions` / `ZoomLayout`: 20 icons (base 32, pitch 36) or 64 icons (base 24, pitch 28), sizes per frame from an own cosine (zoom 121, radius 2.5 pitches) around the cursor, laid out from the window centre, growing away from the top edge. `SyntheticIcons` generates 256 px Pbgra32 frozen discs with radial gradients. R1 (`RenderSurface`) calls `DrawImage` for every icon in `OnRender` and `InvalidateVisual()` once per non-duplicate tick; R2 (`IconVisualHost`) draws each icon once into a `DrawingVisual` and only updates a reused `ScaleTransform` + `TranslateTransform` pair, and only for icons whose rectangle changed.
- **Measurement in the host.** `RenderLoop` subscribes `CompositionTarget.Rendering` only while the cursor is inside the window band (re-checked on a 100 ms timer), so an idle bar renders nothing: every synthetic run showed `idle_frames=0`. Each frame row carries `qpc_ms, rendering_time_ms, is_duplicate, handler_us, onrender_us, gc0..gc2, alloc_bytes`; duplicate `RenderingTime` values are flagged and skipped by the Driver. The log is buffered and flushed every 100 ms (no per-frame I/O).
- **Driver case `render`.** Phases (warm-up, measured window, idle with a 500 ms grace) sit on the shared QPC clock; snapshots of `GetProcessTimes` (host, dwm.exe) and `DwmGetCompositionTimingInfo(NULL)` (dropped/missed frames) at the window edges; percentiles through `Stats.Summarize`; CPU as `dCPU / (wall * ProcessorCount)` and as core equivalents; one row per run appended to `--out`. Output line: `RENDER <variant> <count> <mode> frames=.. cost_p95=.. drop_pct=.. cpu_total_pct=.. idle_frames=..`. Three drivers: host-side synthetic cursor (`--synthetic`, forced by `--dry-run`), real `SendInput` sine sweep, and `--manual` (developer moves the mouse, hand part H5).
- **Real sweep.** `RealSweep` runs inside a `SafetySession` after `DesktopPrep.ClearTopArea`, moves on `x = window centre + 3.5 pitches * sin(2 pi t / 2 s)` at `y = plate centre` every 8 ms, parks the mouse 80 px below the bar for the idle phase and reports its own step jitter. `BeginSafety` and `ClearTopArea` are called on this path only.
- **Scripts.** `run-render-matrix.ps1` builds both projects once, runs the D-17 matrix (+ `-Control`, `-Sizes` for the 1000x160 fallback), writes the summary CSV and per-cell raw files, patches `pm_rows`, refuses `-PresentMon` unelevated (exit 2) before anything is downloaded. `get-presentmon.ps1` fetches only the `PresentMon-2.6.0-x64.exe` release asset over HTTPS from GameTechDev/PresentMon, compares SHA-256 `b2a706bc...f1af`, deletes on mismatch, never executes the file.
- **Observed on this machine (pipeline check, not measurements).** Synthetic runs of a few seconds while other sessions were building: R1/20/hw about 0.4-1.1 % total CPU, cost_p95 0.04-0.09 ms, drop_pct 2-16 % (a busy machine, which is why the plan says no thresholds are read from these), no GC in the window, about 3.4-4.1 KB allocated per frame. `effective_mode=hardware tier=2` for hw and `software tier=2` for sw. dwm.exe CPU is `n/a` at normal integrity here.

## Task Commits

1. **Task 1: R1 end to end with synthetic cursor (tracer)** - `a63b673` (feat)
2. **Task 2: R2, SoftwareOnly, control K, real sweep, manual mode** - `c298525` (feat)
3. **Task 3: matrix runner and hash-gated PresentMon fetch** - `5d2b7bd` (feat)

Tracer gate: `human_verify_mode` is `end-of-phase` and the tracer `<verify>` is automated-only, so it was re-run end to end after the final tracer edit (builds with `-warnaserror`, `render ... --synthetic`) before Task 2 started; it passed and expansion continued.

## Files Created/Modified

- `spikes/WindowHost/Variants/SyntheticIcons.cs` - generated 256 px Pbgra32 frozen icons
- `spikes/WindowHost/Variants/RenderCost.cs` - options, zoom layout, `RenderLoop`, `RenderSurface` (R1), `IconVisualHost` (R2), `RenderCostVariant` (`R1`, `R2`)
- `spikes/Driver/Cases/RenderCase.cs` - `RenderCase`, `RenderRun`, `FrameStats`, `Sweep` / `RealSweep` / `ManualSweep`
- `spikes/Common/native/q4/NativeMethods.txt` - OpenProcess, PROCESS_ACCESS_RIGHTS, CloseHandle, CreateWaitableTimerEx, SetWaitableTimer, WaitForSingleObject, CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, SYNCHRONIZATION_ACCESS_RIGHTS
- `spikes/tools/run-render-matrix.ps1`, `spikes/tools/get-presentmon.ps1`

## Decisions Made

- Interface additions beyond the plan's frames description: the first frames column is `qpc_ms` (system-wide QPC milliseconds), which is what lets the Driver cut the measured and idle windows without a handshake; the host also logs a `render-config` row and, for synthetic runs, a `synthetic-origin` row. The summary CSV header is exactly the one in the plan.
- `alloc_per_frame` is the mean over the measured window (total allocation delta / frames): `GC.GetAllocatedBytesForCurrentThread` advances in allocation-context chunks, so single-frame values are quantized and only means are meaningful.
- Dropped frame = cadence above 25 ms, cadence only between consecutive non-duplicate frames inside the measured window, as in the plan's definitions.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Sweep steps came out at 13 ms instead of 8 ms**
- **Found during:** Task 2 (rehearsal of the real path with `--record-input`)
- **Issue:** The first version raised the system timer with `timeBeginPeriod(1)`; the recorded step interval had a median of 13.45 ms and a maximum of 268 ms. Windows 11 ignores timer-resolution requests of a windowless process.
- **Fix:** Waits use a high-resolution waitable timer (`CREATE_WAITABLE_TIMER_HIGH_RESOLUTION`) for the bulk and a short spin for the last ~0.6 ms. Re-run: median 8.00 ms, p95 8.80 ms, p99 13.12 ms, max 60 ms (a one-off).
- **Files modified:** spikes/Driver/Cases/RenderCase.cs, spikes/Common/native/q4/NativeMethods.txt
- **Commit:** c298525

**2. [Rule 1 - Bug] `IconVisualHost` crashed the host on start**
- **Found during:** Task 2 (first R2 run)
- **Issue:** Setting `IsHitTestVisible` in the constructor walks `VisualChildrenCount` before the `VisualCollection` existed (NullReferenceException, host exit before READY).
- **Fix:** The collection is created first in the constructor.
- **Files modified:** spikes/WindowHost/Variants/RenderCost.cs
- **Commit:** c298525

**3. [Rule 2 - Missing critical] `--record-input` rehearsal of the real input path**
- **Found during:** Task 2
- **Issue:** The real sweep (SendInput, Win+M) must not run in this plan, so its geometry, timing and ordering would otherwise be unverified until 01-16.
- **Fix:** Option `--record-input` on the real path: the sink becomes a `RecordingSink`, the real-input watcher is off, and the run ends with `INPUT RECORDED events=.. moves=.. first=.. last=.. win_m=..; nothing was sent`. The abort hotkey is still registered.
- **Files modified:** spikes/Driver/Cases/RenderCase.cs
- **Commit:** c298525

**4. [Rule 3 - Blocking] PresentMon download and flag check not executed**
- **Found during:** Task 3 verification
- **Issue:** The plan's second `<automated>` command runs `get-presentmon.ps1`, which downloads PresentMon; the dispatch for this run explicitly forbids downloading or running PresentMon. The same applies to "check `--help` of 2.6.0 for exact flag names".
- **Fix:** Added `get-presentmon.ps1 -CheckOnly` (no network) and verified it, the unelevated refusal and the git-ignore; the download path and the flag names remain unverified (see D4 in the coverage block). The flags used (`--process_name`, `--output_file`, `--timed`, `--terminate_after_timed`) are the plan's; the script header says they are unchecked and a rejected flag shows up as `pm_rows = 0` plus a warning.
- **Files modified:** spikes/tools/get-presentmon.ps1, spikes/tools/run-render-matrix.ps1
- **Commit:** 5d2b7bd

### Small deviations (no rule needed)

- `RenderCost.cs` freezes any icon that is not frozen (acceptance criterion names `Freeze` in that file; `SyntheticIcons` already freezes).
- PresentMon `--timed` is `warmup + seconds + idle + 10` instead of `warmup + seconds` (PresentMon's timer starts before the host does). Extra switch `-Yes` on the matrix runner (a real run otherwise asks for `GO`).
- The supporting types (`RenderRun`, `FrameStats`, `Sweep`, `RealSweep`, `ManualSweep`) live in `RenderCase.cs`; the file list of the plan was kept.
- One tracer re-run printed "no READY line within 10 s" right after a rebuild; the next two runs were fine (first start of a freshly built exe), no code change.
- Windows PowerShell parse check was done by running the scripts; no separate parser run.

**Total deviations:** 3 auto-fixed (2 bugs, 1 missing critical), 1 blocking-by-dispatch, 5 small. **Impact:** none on scope; 01-16 gets a rehearsed instrument.

## Issues Encountered

- `dwm_cpu_total_pct` is `n/a` on this machine: `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)` on dwm.exe fails at normal integrity. The DWM frame counters (`dwm_frames_dropped/missed`) work without elevation. If the DWM CPU is needed in 01-16, run the matrix elevated (together with `-PresentMon`) or read it in the Task Manager details tab by hand.
- Synthetic windows are short and the machine was busy (other sessions building), so the drop rates seen here (2-16 %) say nothing about the bar. Do not compare them with the 60 fps criterion.
- `CsWin32` maps `FILETIME` to `System.Runtime.InteropServices.ComTypes.FILETIME` and `GetProcessTimes` takes `out` parameters; `CREATE_WAITABLE_TIMER_HIGH_RESOLUTION` is a plain constant and `TIMER_ALL_ACCESS` lives in `SYNCHRONIZATION_ACCESS_RIGHTS` (topic list entries).

## Known Stubs

None.

## Threat Flags

None. T-01-15 mitigated as planned in `get-presentmon.ps1` (release asset name fixed, HTTPS, URL prefix check, SHA-256 gate, delete on mismatch, ignored folder, never executed); the download path is not yet exercised (D4). T-01-16: rendering is subscribed only while the cursor is in the band, the host exits by itself (`--exit-after-ms` = planned duration + 25 s), killed by HostProcess dispose. T-01-SC: no packages added.

## Next Phase Readiness

For 01-16 (real measurement after the hands-off checkpoint): `powershell -File spikes/tools/run-render-matrix.ps1 -Label <x> -Control -Sizes 1900x250,1000x160 -Yes` (real sweep, moves the mouse about 40 s per cell), optionally elevated with `-PresentMon` after `get-presentmon.ps1` and a look at `PresentMon-2.6.0-x64.exe --help` for the flag names. Hand part H5 uses `Driver.exe render ... --manual --seconds N`. The instrument is ready; thresholds and the technique decision (D-13, D-18, D-19) are 01-16's job.

## Self-Check: PASSED

- Files exist: SyntheticIcons.cs, RenderCost.cs, RenderCase.cs, native/q4/NativeMethods.txt, run-render-matrix.ps1, get-presentmon.ps1 (all present).
- Commits `a63b673`, `c298525`, `5d2b7bd` are ancestors of HEAD; `commits: 3` measured from the ledger.
- Acceptance criteria re-run: tracer CSV header starts with `label,variant,count,mode,transparent,size,seconds,frames,cost_p50`; RenderCost.cs contains `CompositionTarget.Rendering`, `RenderingTime`, `GetAllocatedBytesForCurrentThread`, `Freeze`, `DrawingVisual`, `VisualCollection`, `ScaleTransform`, `TranslateTransform`; RenderCase.cs contains `GetProcessTimes`, `DwmGetCompositionTimingInfo`, and calls `BeginSafety` / `ClearTopArea` only inside the non-synthetic branch; tracer `idle_frames` 0; `,R2,64,sw,` row present with `effective_mode` software; get-presentmon.ps1 contains the full digest and `Get-FileHash`; `git check-ignore -q spikes/tools/presentmon/PresentMon-2.6.0-x64.exe` exits 0; unelevated `-PresentMon` exits 2; both scripts are ASCII.
- Plan verification: builds with `-warnaserror` clean; synthetic R1/R2, hw/sw and control runs exit 0; `run-render-matrix.ps1 -DryRun` exits 0; `get-presentmon.ps1` was only run with `-CheckOnly` (download forbidden by the dispatch, deviation 4).
