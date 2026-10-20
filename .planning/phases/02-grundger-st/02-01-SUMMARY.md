---
phase: 02-grundger-st
plan: 01
subsystem: app-shell
tags: [tracer, bar-window, alpha-plate, z-order, placement, wpf, cswin32]

requires:
  - phase: 01-risiko-spikes
    provides: "H1 alpha-1 plate, S0 base of the z-order keeper, R1/hardware drawing, CsWin32 0.3.360, VSTest"
provides:
  - "src/MehrBar.App: WPF app (AssemblyName MehrBar) with its own Main, single-file Release properties, asInvoker/PerMonitorV2 manifest"
  - "BarHwnd: TOOLWINDOW|NOACTIVATE, MA_NOACTIVATE, every z-order change rewritten to HWND_BOTTOM (AllowZOrderChange for 02-10), PrimaryMonitorRect, SetBounds, PlaceBottom, AllowLaunchedProcessForeground"
  - "BarSurface: one OnRender with the alpha-1 plate and the semi-transparent default background (D-07), optional outline"
  - "BarWindow: transparent window in device pixels, Native/Surface, ApplyBounds"
  - "MehrBar.Core.Layout.BarPlacement with BarEdge, BarAlign, RectI, PlacementSpec; 6 tests"
affects: [02-02, 02-03, 02-07, 02-10, 02-11]

actuals:
  tasks: 1
  commits: 1

key-files:
  created:
    - src/MehrBar.App/MehrBar.App.csproj
    - src/MehrBar.App/app.manifest
    - src/MehrBar.App/Interop/NativeMethods.json
    - src/MehrBar.App/Interop/Window/NativeMethods.txt
    - src/MehrBar.App/Interop/BarHwnd.cs
    - src/MehrBar.App/Bars/BarWindow.cs
    - src/MehrBar.App/Bars/BarSurface.cs
    - src/MehrBar.App/Hosting/Program.cs
    - src/MehrBar.Core/Layout/BarPlacement.cs
    - src/MehrBar.Tests/Layout/BarPlacementTests.cs
  modified:
    - Directory.Packages.props
    - MehrBar.sln

key-decisions:
  - "Default background (Claude's discretion): rounded rectangle radius 8, fill #B3202020, 1 px border #33FFFFFF"
  - "Empty bar 108 x 44 px (3 slots of 32 px plus inner margin 6), placed by BarPlacement at top centre with edge offset -5"
  - "Window = bar plus a provisional overhang of 230 px along the edge and 89 px away from it, clamped along the edge (replaced in 02-11)"

requirements-completed: []

coverage:
  - id: D1
    description: "Release build without warnings, all tests green, 6 BarPlacement tests"
    verification:
      - kind: other
        ref: "dotnet build MehrBar.sln -c Release -warnaserror (0 warnings); dotnet test 34/34; placement.trx 6 x Passed"
        status: pass
    human_judgment: false
  - id: D2
    description: "No Win32 outside src/MehrBar.App/Interop, no Windows.Win32 in Core"
    verification:
      - kind: other
        ref: "grep checks of the plan: 0 and 0"
        status: pass
    human_judgment: false
  - id: D3
    description: "The bar appears at the top centre, at the bottom of the z-order, without taking focus"
    verification: []
    human_judgment: true
    rationale: "Visual check on the desktop is the checkpoint 02-03; this session runs in Windows session 0"

completed: 2026-10-20
status: complete
---

# Phase 2 Plan 01: Tracer - empty bar at the bottom Summary

**MehrBar.exe now exists: an empty semi-transparent box of three 32 px slots, placed by the tested Core function at the top centre, shown at HWND_BOTTOM as a non-activating tool window with an alpha-1 plate as its only hit target.**

## Accomplishments

- New WPF project `src/MehrBar.App` (net10.0-windows, AssemblyName MehrBar, CsWin32 pinned to 0.3.360 in the root props), own `Main`, no App.xaml.
- `BarHwnd` carries the Phase-1 window technique: hook before the first show, styles, `MA_NOACTIVATE`, every z-order change rewritten to `HWND_BOTTOM`.
- `BarSurface` draws plate and default background in one `OnRender`, redraws only on change.
- `BarPlacement.Compute` places a bar on any edge (axis-neutral), with signed offset, negative edge offset and clamping; 6 tests.

## Task Commits

- this commit: `feat(02-01): tracer - empty bar at the bottom of the z-order`

## Deviations from Plan

- The project was written directly instead of `dotnet new wpf` plus deleting the template files; the result is the same (no App.xaml, no MainWindow).
- A start with `--exit-after-ms 1500` ran and exited with code 0, but only from Windows session 0 (no visible desktop); the visual check stays with 02-03.

## Self-Check: PASSED

- Release build 0 warnings, 34/34 tests, BarPlacementTests 6/6, interop grep checks 0/0, `dotnet sln list` shows the three projects.
