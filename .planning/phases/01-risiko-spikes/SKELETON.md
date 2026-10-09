# Walking Skeleton — MehrBar

**Phase:** 1
**Generated:** 2026-10-09

> Phase 1 is a risk-spike phase, not a feature phase. The skeleton-relevant part is the **real product scaffold** (built in plan 01-01 and extended by the zoom core in 01-05) plus the **spike measurement harness** (skeleton 01-02, host 01-03, safety/input 01-04, driver oracles 01-07). Template elements that do not exist in this project are marked N/A with the reason.

## Capability Proven End-to-End

`dotnet test` in the repo root builds `MehrBar.Core` (net10.0, no packages) and runs `MehrBar.Tests` (xUnit v3 on Microsoft Testing Platform) green — including the zoom-anchor test that reproduces the original RocketDock measurements — while `spikes/WindowHost` (WPF + CsWin32) shows a bottom, non-activating, transparent bar that the separate `spikes/Driver` process measures from outside without sending input (`Driver.exe smoke`).

## Architectural Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Framework | C# / .NET 10 / WPF (App from phase 2); `MehrBar.Core` on plain `net10.0` | Fixed by the concept; Core stays free of WPF/Win32 so zoom, layout, config and skin parser are unit-testable (CLAUDE.md constraints) |
| SDK pin | `global.json`: SDK 10.0.401, `rollForward: latestFeature` | GSD executors and the developer build identically (STACK) |
| Test runner | xunit.v3.mtp-v2 4.0.1 on Microsoft Testing Platform (`global.json` `test.runner`); fallback B = xunit.v3.mtp-off + xunit.runner.visualstudio + Microsoft.NET.Test.Sdk | D-02; VS Code discovery decided in the hand part H6 (plan 01-17), fallback applied in 01-18 — rated costly (D-02) |
| Package management | Central package management with exact pins; root `Directory.Packages.props` for the product (xunit.v3.mtp-v2 4.0.1 now, CsWin32 0.3.360 added with MehrBar.App in phase 2); `spikes/Directory.Packages.props` pins CsWin32 0.3.360 for the throw-away spikes; project references never carry `Version` | Exact pins (CsWin32 0.3.x changes almost daily); spikes stay self-contained so deleting them after phase 2 (D-01) leaves the product untouched; reversible |
| Solution format | Classic `MehrBar.sln` (`dotnet new sln --format sln`) | Concept section 4 names `MehrBar.sln`; `.slnx` support in C# Dev Kit unverified (RESEARCH Pitfall 9); reversible |
| Win32 access | CsWin32 source generation, `allowMarshaling:false`, name lists (`NativeMethods.txt`); in the product only under `App/Interop` | Project decision; no hand-written signatures |
| Zoom core API | `MehrBar.Core.Layout`: `ZoomKernel`, `ZoomEngine.Compute`, `ZoomAnchorMode {Cursor, Center}`, `RestItem`, `ZoomParams`, `ZoomResult` — axis-neutral `u` | Names match `.planning/research/ARCHITECTURE.md` so phase 3 extends (RestLayout/FrameLayout) instead of rewriting |
| Data layer | N/A in phase 1 | No persistence in the spikes; phase 2 adds `ConfigStore` (System.Text.Json, `%APPDATA%\MehrBar\config.json`) |
| Auth | N/A | Local single-user desktop app, no accounts or services |
| Routing | N/A | No web UI or routes; the "entry points" are the bar window and the tray (phase 2) |
| Deployment target | N/A in phase 1 — documented local run commands instead (below) | Self-contained single-file publish of `MehrBar.App` starts in phase 2 (first publish) and is finished in phase 7 |
| Directory layout | `src/MehrBar.Core/Layout`, `src/MehrBar.Tests` (+ `Fixtures/`); throw-away `spikes/{Common,WindowHost,Driver,Icons,ZoomCheck,tools,results}` outside the solution | D-01: spikes committed for re-measurement, deleted or frozen after phase 2 without touching `src/` |
| Spike harness | Two processes: WindowHost (bar under test) + Driver (input source and independent oracle: pixel probe, z-order snapshot, foreground); SafetySession for every input-sending run | D-03/D-05; driver CPU stays out of the host measurement; one oracle set for Q1–Q4 |
| Private data | `spikes/results/private/` (ignored, resolved to the main checkout even from GSD worktrees) | Third-party screenshots/skins and Stephan's paths never enter the repo (CLAUDE.md) |

## Stack Touched in Phase 1

- [x] Project scaffold (framework, build, test runner) — `global.json`, `Directory.Packages.props`, `MehrBar.sln`, Core, Tests (01-01); spike skeleton with CsWin32 (01-02)
- [ ] Routing — N/A (desktop app without routes)
- [ ] Database — N/A (no persistence in phase 1; config store is phase 2)
- [x] UI — one interactive element wired end-to-end: the spike bar window (bottom, non-activating, hit plate) driven and measured by the Driver (01-03, 01-04, 01-07, 01-09)
- [x] Deployment — documented local full-stack run commands:
  - `dotnet test`
  - `dotnet build spikes/WindowHost -c Release -warnaserror && dotnet build spikes/Driver -c Release -warnaserror && spikes/Driver/bin/Release/net10.0-windows/Driver.exe smoke`
  - Re-measure commands per spike: `spikes/README.md` section "Nachmessen" (consolidated in 01-18)

## Out of Scope (Deferred to Later Slices)

- `MehrBar.App` project, BarWindow, ZOrderKeeper, tray, single instance, logging, config store, launching, drag & drop (phase 2 — scope adjusted by `01-SPIKE-ERGEBNISSE.md`)
- RestLayout/FrameLayout/AxisFrame, animation, labels, calibration against full-screen re-shots (phase 3)
- Skin parser and 9-slice rendering (phase 4)
- Publish settings (`IncludeNativeLibrariesForSelfExtract`, ReadyToRun measurement) (phases 2 and 7)
- Any product code inside `spikes/` — spike code is never promoted as-is

## Subsequent Slice Plan

Each later phase adds one vertical slice on top of this skeleton without altering its architectural decisions:

- Phase 2: A usable bar replacing RocketDock daily — bottom layer with the strategy from Q2, hit technique from Q1, menus from Q3, drawing technique from Q4, icons per Q6; config persisted; tray
- Phase 3: Zoom and labels that feel like RocketDock — extends `ZoomEngine` from phase 1
- Phase 4: Skin CrystalXP.net
- Phase 5: Insert, reorder, menus, properties
- Phase 6: Settings window and autostart
- Phase 7: Robustness and release single-file exe
