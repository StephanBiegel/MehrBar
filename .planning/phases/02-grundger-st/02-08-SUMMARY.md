---
phase: 02-grundger-st
plan: 08
subsystem: layout
tags: [layout-contract, window-plan, hit-test, axis-neutral, tdd]

requires:
  - phase: 02-grundger-st
    provides: "02-01 BarPlacement; 02-05 model; phase 1 ZoomEngine.ComputeSmooth"
provides:
  - "BarMetrics (pitch, depth, radius 0.645 x zoomWidth, padding 6, min slots 3)"
  - "AxisFrame (window pixels <-> u/v for all edges)"
  - "RestLayout/RestLayoutBuilder (only app and folder become slots, whole-list index kept)"
  - "FrameLayout.Compute(rest, pointerU, progress): layout contract, progress 0 = rest"
  - "WindowPlan.Compute: bar rect plus sampled max zoom growth per side and depth growth, clamped along the edge"
  - "HitTest.At: Item (whole-list index), Bar or None"
affects: [02-11, 02-12, 02-13, phase-03-zoom]

actuals:
  tasks: 2
  commits: 1

key-files:
  created:
    - src/MehrBar.Core/Layout/BarMetrics.cs
    - src/MehrBar.Core/Layout/AxisFrame.cs
    - src/MehrBar.Core/Layout/RestLayout.cs
    - src/MehrBar.Core/Layout/FrameLayout.cs
    - src/MehrBar.Core/Layout/WindowPlan.cs
    - src/MehrBar.Core/Layout/HitTest.cs
    - src/MehrBar.Tests/Layout/RestLayoutTests.cs
    - src/MehrBar.Tests/Layout/FrameLayoutTests.cs
    - src/MehrBar.Tests/Layout/WindowPlanTests.cs
    - src/MehrBar.Tests/Layout/HitTestTests.cs
  modified:
    - src/MehrBar.Tests/Layout/BarPlacementTests.cs

key-decisions:
  - "Max zoom growth with the defaults is about 116 px per side (ComputeSmooth splits the extra room about half and half), not the 150-300 px the plan estimated from the cursor split; the window test checks the computed growth instead"
  - "Window coordinates round outwards (floor/ceil) so the growth always fits"
  - "BarPlacement unchanged; the offset convention of 02-01 already satisfied the new facts"

requirements-completed: []

coverage:
  - id: D1
    description: "Layout contract (rest, frame, axis frame)"
    verification:
      - kind: other
        ref: "frame.trx 12 x Passed (RestLayout 5 incl. 4 AxisFrame theory cases, FrameLayout 4)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Window plan, hit test, offset facts"
    verification:
      - kind: other
        ref: "WindowPlan 7, HitTest 5, BarPlacement 6+5 theory cases all green; Release build 0 warnings; suite 98/98; no Win32/WPF namespaces in Core/Layout"
        status: pass
    human_judgment: false

completed: 2026-10-20
status: complete
---

# Phase 2 Plan 08: Layout contract in Core Summary

**The layout contract for phase 3 is in Core and tested: rest layout, frame layout with zoom progress (0 = rest), a window plan that already holds the future zoom area (about 116 px per side with the defaults), and a hit test by whole-list index.**

## Task Commits

- this commit: `feat(02-08): layout contract, window plan and hit test in Core`

## Deviations from Plan

- **Rule 1, test expectation:** WindowPlan test 1 expected 150-300 px growth per side (plan estimate from the cursor split). The real computation with `ComputeSmooth` gives about 116 px; the test now asserts 80-300 px and that the window covers the computed growth.
- The session was interrupted once during verification; build and tests were rerun from scratch afterwards (98/98).

## Self-Check: PASSED
