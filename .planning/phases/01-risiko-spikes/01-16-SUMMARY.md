---
phase: 01-risiko-spikes
plan: 16
subsystem: rendering
tags: [render-cost, layered-window, presentmon, wpf, spike-note, go-no-go]

requires:
  - phase: 01-risiko-spikes
    provides: "01-12 render instrument (RenderCost variants, Driver render case), 01-15 Q5 product zoom maths"
provides:
  - "spikes/tools/render-report.ps1: Q4 report pipeline over the render matrix"
  - "Normal-integrity D-17 render matrix with the product layout (ZoomEngine) under spikes/results/"
  - "01-SPIKE-render-cost.md: R1 + hardware is the cheapest combination; D-18 drop share left UNKLAR for Stephan"
affects: [01-17, 01-18, 01-19, phase-02-bar-window, phase-03-zoom]

actuals:
  tasks: 3
  commits: 6
plan_head_before: c994f1ec8a22da971604354c41a3192185cce1c8
plan_head_after: 950362e

key-files:
  created:
    - spikes/tools/render-report.ps1
    - .planning/phases/01-risiko-spikes/01-SPIKE-render-cost.md
  modified:
    - spikes/tools/run-render-matrix.ps1
    - spikes/WindowHost/Variants/RenderCost.cs

key-decisions:
  - "Q4: drawing technique R1 (one OnRender per frame, redraw all), render mode hardware; SoftwareOnly stays the fallback"
  - "R1 cost p95 0.017 ms, CPU 0.43 % at 20 and 64 icons: D-18 cost and D-19 CPU pass with a large margin"
  - "D-18 drop share UNKLAR: PresentMon does not see the layered window (A3 confirmed); host cadence 5.2-5.8 % as proxy, same gaps in the opaque window -> follow-up plan 01-19 measures on screen"
  - "Workload laid out with the product zoom maths (ZoomEngine) instead of the plan's synthetic layout"

requirements-completed: []

coverage:
  - id: D1
    description: "D-17 matrix run at normal integrity with the product layout; summaries committed"
    verification:
      - kind: other
        ref: "run-render-matrix.ps1 + render-report.ps1 (commit 6b623f3)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Elevated PresentMon run for the D-18 drop share"
    verification: []
    human_judgment: true
    rationale: "PresentMon cannot see the layered window; replaced by the on-screen measurement of 01-19"
  - id: D3
    description: "01-SPIKE-render-cost.md with one decision line and consequences"
    verification:
      - kind: other
        ref: "commit 950362e"
        status: pass
    human_judgment: false

completed: 2026-10-13
status: complete
---

# Phase 1 Plan 16: Q4 render cost of the transparent bar window Summary

**R1 (one OnRender per frame) on hardware is the cheapest drawing technique and passes the D-18 cost and D-19 CPU thresholds at 20 and 64 icons; the D-18 drop share stayed UNKLAR because PresentMon cannot see the layered window, which led to plan 01-19.**

## Accomplishments

- Report pipeline (`render-report.ps1`) and note skeleton; the matrix now stops at once on a safety abort.
- The Q4 workload is laid out with the product zoom maths (`ZoomEngine`), so cost and motion match the real bar.
- Normal-integrity matrix: R1/hw cost p95 0.017 ms, CPU 0.43 %; R2 brings nothing because with cursor anchoring every icon moves.
- PresentMon (elevated) does not list the layered window; the host cadence shows 5.2-5.8 % late frames, the same in an opaque window.

## Task Commits

- `e3f0295` feat: report pipeline and note skeleton
- `99e36e6` fix: stop the matrix at once on a safety abort
- `f06889d`, `2dcc95b` fix/docs: product zoom maths in the workload
- `6b623f3` test: normal-integrity matrix
- `950362e` docs: Q4 note, decision UNKLAR on the drop share

## Deviations from Plan

- **Product layout instead of synthetic layout** (Rule 1): the synthetic layout did not reflect cursor anchoring.
- **D-18 drop share not measurable with PresentMon:** carried into the follow-up plan 01-19 (Desktop Duplication on screen), decided with Stephan on 2026-10-13. 01-19 later replaced the proxy: on screen R1/20/hw loses 10.4-26.3 % of the 60 Hz periods depending on machine state, RocketDock 7-12 %.

## Items for Stephan

1. Borderline 1 of the note (accept proxy or other measuring path) is answered by 01-19: on-screen measurement, benchmark RocketDock instead of 1 %. To be confirmed in the hand pass 01-17.
2. H5: real-mouse feel over 20 and 64 icons (in 01-17).

## Self-Check: PASSED

- Commits listed above are ancestors of HEAD; the note exists with one decision line.
