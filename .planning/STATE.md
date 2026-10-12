---
gsd_state_version: "1.0"
current_phase: 01
current_phase_name: Risiko-Spikes
status: executing
stopped_at: Completed 01-11-PLAN.md
last_updated: "2026-10-12T07:19:16.004Z"
last_activity: 2026-10-10
last_activity_desc: Phase 01 execution started
state_head: 5c9d4537b472a50e817ad3d6d41f93812be85653
progress:
  total_phases: 7
  completed_phases: 0
  total_plans: 18
  completed_plans: 11
  percent: 0
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-10-09)

**Core value:** Der Zoom fühlt sich an wie RocketDock mit CrystalXP.net — flüssig, vertraut, mit demselben Abfall und derselben Verankerung.
**Current focus:** Phase 01 — Risiko-Spikes

## Current Position

Phase: 01 (Risiko-Spikes) — EXECUTING
Plan: 12 of 18
Status: Ready to execute
Last activity: 2026-10-10 — Phase 01 execution started

Progress: [░░░░░░░░░░] 0%

## Performance Metrics

**Velocity:**
- Total plans completed: 0
- Average duration: -
- Total execution time: 0.0 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| - | - | - | - |

**Recent Trend:**
- Last 5 plans: -
- Trend: -

*Updated after each plan completion*
**Per-Plan Metrics:**

| Plan | Duration | Tasks | Files |
|------|----------|-------|-------|
| Phase 01 P01 | 2 min | 2 tasks | 9 files |
| Phase 01 P02 | 5 min | 2 tasks | 13 files |
| Phase 01 P03 | 13min | 2 tasks | 8 files |
| Phase 01 P04 | 10min | 2 tasks | 5 files |
| Phase 01 P06 | 12min | 3 tasks | 12 files |
| Phase 01 P07 | 25 min | 2 tasks | 8 files |
| Phase 01 P08 | 3 min | 2 tasks | 2 files |
| Phase 01 P09 | 12 min | 2 tasks | 3 files |
| Phase 01 P10 | 11 min | 2 tasks | 5 files |
| Phase 01 P11 | 55 min | 2 tasks | 4 files |

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- [Roadmap]: Spike-Phase vor dem Grundgerüst; jede Frage endet mit Go/No-go-Notiz
- [Roadmap]: Win+D-/Explorer-Neustart-Robustheit, Trefferfläche, ConfigStore, Core-Layoutvertrag und erster Publish liegen in Phase 2 (nicht erst im Feinschliff)
- [Roadmap]: Zwischenlösung bis Phase 6 — „Konfiguration“ (Tray, Leisten-Menü) öffnet die config.json im Standard-Editor; Werte wirken nach Neustart
- [Roadmap]: Trennlinien werden in Phase 4 gezeichnet (Einträge per config.json), Einfügen/Verschieben/Löschen über die Leiste kommt in Phase 5
- [Q12 revidiert]: Zoom-Verankerung `cursor` als Standard, `center` wählbar
- [Phase 01]: Classic MehrBar.sln (--format sln) instead of SDK-10 default .slnx; C# Dev Kit .slnx support unverified
- [Phase 01]: Product Directory.Packages.props pins only xunit.v3.mtp-v2; CsWin32 pin comes with MehrBar.App (phase 2), spikes pin in their own file
- [Phase 01]: 01-02: spikes/Directory.Packages.props is independent of the root props (nearest file wins), so deleting spikes/ leaves the product untouched
- [Phase 01]: 01-02: RepoPaths.cs needs explicit 'using System.IO' because WPF projects drop it from implicit usings
- [Phase 01]: 01-03: initial show of the bar window is pinned to HWND_BOTTOM by rewriting WPF's own SWP_SHOWWINDOW call in WM_WINDOWPOSCHANGING (plain WPF Show() lifts it to z-index 21); --show-mode wpf keeps the baseline
- [Phase 01]: 01-03: host types are internal because CsWin32 types (HWND, RECT) are internal; EnumWindows uses an UnmanagedCallersOnly function pointer
- [Phase 01]: 01-04: Restore actions may send input (Restoring flag lifts abort check in Dispose); real SendInputSink refuses to run without an active SafetySession; Begin fails closed without an abort hotkey
- [Phase 01]: 01-06: IconCache uses STA workers (MTA returns blank .url icons); 48 px-only sources come back framed from 64 px up, re-request at 48 px; every shell bitmap is straight alpha, convert once to Pbgra32
- [Phase 01]: 01-07: witness gets --activate false so dry runs never take the foreground; started hosts die via ProcessExit plus SafetySession restore (Program.cs untouched)
- [Phase 01]: 01-07: committed z-order output is redacted (foreign windows as class other); ToPrivateCsv writes only into RepoPaths.PrivateResults
- [Phase 01]: Q7 GO for CLI: CsWin32 0.3.360 multi-list builds warning-free, xunit.v3.mtp-v2 19/19 green; filter syntax is --filter-method/--filter-class, list with --list-tests; VS Code Test Explorer open as H6
- [Phase 01]: 01-10: Q2 sampling cadence is one display refresh (16.7 ms at 60 Hz) because GetPixel/BitBlt wait for the next frame; ProbeStrip reads all five probes with one BitBlt, so D-11 '1-2 samples' means 1-2 frames
- [Phase 01]: 01-10: while the S1/S2 watcher holds the bar lifted, 'desktop shown' = no ordinary window above the desktop host (heuristic, to be confirmed with WinEvent log in 01-14)
- [Phase 01]: Q3: WPF popup item rectangles come from the popup's Win32 rectangle plus in-popup offsets (PointToScreen was wrong 130 ms after open); Driver uses the newest menu-items row >= 150 ms after menu-open

### Pending Todos

- Vor Phase 3: Referenz-Screenshots des Originals als Vollbild neu aufnehmen (linker Rand −3 evtl. Bildbeschnitt) plus Reihe mit Zoom-Breite 2/6/8 (Stephan, braucht RocketDock)

### Blockers/Concerns

- [Phase 1]: Ergebnisse (Win+D-Wächter, Menütechnik, Zeichenkosten des Layered-Fensters) können den Umfang von Phase 2 ändern — Phase 2 erst nach den Go/No-go-Notizen planen
- [Phase 4]: Bedeutung von `Outside-*Margin` und Strecken der Trennlinie unbelegt; `docs/skin-format.md` ist Tor vor dem Code
- [Phase 7]: Startzeit < 1 s mit Single-File ohne Kompression noch ungemessen; ReadyToRun nur nach Messung

## Deferred Items

Items acknowledged and deferred at milestone close, most recent first:

| Category | Item | Status | Deferred At | Milestone |
|----------|------|--------|-------------|-----------|
| *(none)* | | | | |

## Session Continuity

Last session: 2026-10-12T07:19:15.969Z
Stopped at: Completed 01-11-PLAN.md
Resume file: None
