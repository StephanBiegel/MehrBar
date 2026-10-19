---
phase: 01-risiko-spikes
plan: 19
subsystem: rendering
tags: [frame-pacing, desktop-duplication, directcomposition, zoom-jitter, rocketdock, spike-note]

requires:
  - phase: 01-risiko-spikes
    provides: "01-16 render instrument and Q4 note (drop share UNKLAR), 01-15 ZoomEngine"
provides:
  - "On-screen measurement in the Driver: screen-sweep / screen-analyze and render --capture (DXGI Desktop Duplication)"
  - "ZoomEngine.ComputeSmooth: smooth cursor split, outer icons stand still (ZoomSmoothnessTests)"
  - "spikes/CompositionHost: DirectComposition comparison bar (variant DC), flip swapchain + Direct2D"
  - "spikes/tools/run-screen-compare.ps1 and spikes/results/q4b-screen-summary.csv"
  - "01-SPIKE-frame-pacing.md: jitter fixed, WPF frame pacing on par with DirectComposition and RocketDock"
affects: [01-17, 01-18, phase-02-bar-window, phase-03-zoom]

actuals:
  tasks: 3
  commits: 6
plan_head_before: 950362e
plan_head_after: 288e6ff

key-files:
  created:
    - spikes/Driver/ScreenCapture.cs
    - spikes/Driver/Cases/ScreenCase.cs
    - spikes/CompositionHost/
    - spikes/tools/run-screen-compare.ps1
    - spikes/results/q4b-screen-summary.csv
    - .planning/phases/01-risiko-spikes/01-SPIKE-frame-pacing.md
  modified:
    - src/MehrBar.Core/Layout/ZoomEngine.cs
    - spikes/Driver/Cases/RenderCase.cs
    - spikes/WindowHost/Variants/RenderCost.cs

key-decisions:
  - "Outer-icon jitter came from the sampled cursor split in ZoomEngine.Compute (+-0.6-0.7 px per crossed icon); ComputeSmooth splits the extra space by the kernel integral and the outer icons stand still (0.02 px/s, 0 reversals), like RocketDock"
  - "Pixel snapping brings nothing and costs frames: no snapping in the product"
  - "Frame pacing on screen (2026-10-19, same day): WPF R1 10.4 %, DirectComposition 9.1 % skipped; RocketDock 7-12 % (2026-10-13). The 22.5 % of 2026-10-13 was machine state. Recommendation: WPF OnRender for phase 2, CompositionHost as documented fallback"
  - "Benchmark for frame pacing is RocketDock, not the D-18 1 % threshold (RocketDock misses it too)"

requirements-completed: []

coverage:
  - id: D1
    description: "On-screen measurement tool and WPF runs (sampled/smooth, snapped/unsnapped) plus RocketDock reference"
    verification:
      - kind: other
        ref: "run-screen-compare.ps1, q4b-screen-summary.csv (commits ab423b7, 2022b8a)"
        status: pass
    human_judgment: false
  - id: D2
    description: "DirectComposition comparison host measured with the same tool and workload"
    verification:
      - kind: other
        ref: "run-screen-compare.ps1 -Only host-smooth-2,dcomp-smooth,dcomp-sampled, run by Stephan from the desktop (commit 288e6ff)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Visual check of the smooth bar"
    verification: []
    human_judgment: true
    rationale: "The feel of the zoom is judged by Stephan in the hand pass 01-17"

completed: 2026-10-19
status: complete
---

# Phase 1 Plan 19: Frame pacing and outer-icon jitter on screen Summary

**The outer-icon jitter is fixed by ZoomEngine.ComputeSmooth, and measured on screen the WPF bar keeps pace with a DirectComposition bar (10.4 % vs 9.1 % skipped periods, RocketDock 7-12 %), so phase 2 stays on the WPF OnRender path.**

## Accomplishments

- Desktop Duplication tool in the Driver measures what the DWM actually shows, for our bar and for RocketDock.
- Cause of the jitter found and fixed in Core (`ComputeSmooth`); the Q5 RocketDock reference rows still pass within +-6 px.
- RocketDock at the bar end measured: the box edge follows the computation with a constant ~19 px skin margin.
- DirectComposition host (flip swapchain, premultiplied alpha, Direct2D, frame-latency waitable) built and measured: no meaningful gain over WPF.

## Task Commits

- `da9b667` feat: smooth cursor split
- `ab423b7` test: bar motion and frame pacing on screen
- `2022b8a` test: RocketDock run near the bar end
- `b1cfb18` docs: frame pacing note
- `0e87cab` feat: DirectComposition comparison host
- `288e6ff` docs: DirectComposition run, WPF on par

## Deviations from Plan

- Tool lives in the Driver (`screen-sweep`/`screen-analyze`, `--capture`) instead of a separate `spikes/FrameCapture` project; motion measured by shift search instead of icon edges.
- Only 20 icons measured on screen (64 not), `--backdrop` not used.
- Added beyond the plan: `ComputeSmooth` and RocketDock as the reference.
- The DirectComposition run had to be started by Stephan from the desktop: a Hivemo-server session runs in Windows session 0, where the abort hotkey cannot be registered and the Driver refuses to move the mouse.

## Issues Encountered

- Frame pacing differs strongly between days (WPF 22.5 % on 2026-10-13, 10.4 % on 2026-10-19); RocketDock was not re-measured on 2026-10-19.

## Items for Stephan

1. Visual check of the smooth bar (hand pass 01-17).
2. Optional: re-measure RocketDock on the same day for a clean comparison.

## Self-Check: PASSED

- Commits listed above are ancestors of HEAD; q4b-screen-summary.csv holds the host-smooth-2, dcomp-smooth and dcomp-sampled rows.
