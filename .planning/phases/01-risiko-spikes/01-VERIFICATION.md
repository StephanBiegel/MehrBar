---
phase: 01-risiko-spikes
verified: 2026-10-19T14:30:00Z
status: human_needed
score: 4/5 roadmap success criteria verified (1 uncertain, needs a human decision)
covered_files: [".planning/phases/01-risiko-spikes/01-01-PLAN.md", ".planning/phases/01-risiko-spikes/01-01-SUMMARY.md", ".planning/phases/01-risiko-spikes/01-02-PLAN.md", ".planning/phases/01-risiko-spikes/01-02-SUMMARY.md", ".planning/phases/01-risiko-spikes/01-03-PLAN.md", ".planning/phases/01-risiko-spikes/01-03-SUMMARY.md", ".planning/phases/01-risiko-spikes/01-04-PLAN.md", ".planning/phases/01-risiko-spikes/01-04-SUMMARY.md", ".planning/phases/01-risiko-spikes/01-05-PLAN.md", ".planning/phases/01-risiko-spikes/01-05-SUMMARY.md", ".planning/phases/01-risiko-spikes/01-06-PLAN.md", ".planning/phases/01-risiko-spikes/01-06-SUMMARY.md", ".planning/phases/01-risiko-spikes/01-07-PLAN.md", ".planning/phases/01-risiko-spikes/01-07-SUMMARY.md", ".planning/phases/01-risiko-spikes/01-08-PLAN.md", ".planning/phases/01-risiko-spikes/01-08-SUMMARY.md", ".planning/phases/01-risiko-spikes/01-09-PLAN.md", ".planning/phases/01-risiko-spikes/01-09-SUMMARY.md", ".planning/phases/01-risiko-spikes/01-10-PLAN.md", ".planning/phases/01-risiko-spikes/01-10-SUMMARY.md", ".planning/phases/01-risiko-spikes/01-11-PLAN.md", ".planning/phases/01-risiko-spikes/01-11-SUMMARY.md", ".planning/phases/01-risiko-spikes/01-12-PLAN.md", ".planning/phases/01-risiko-spikes/01-12-SUMMARY.md", ".planning/phases/01-risiko-spikes/01-13-PLAN.md", ".planning/phases/01-risiko-spikes/01-13-SUMMARY.md", ".planning/phases/01-risiko-spikes/01-14-PLAN.md", ".planning/phases/01-risiko-spikes/01-14-SUMMARY.md", ".planning/phases/01-risiko-spikes/01-15-PLAN.md", ".planning/phases/01-risiko-spikes/01-15-SUMMARY.md", ".planning/phases/01-risiko-spikes/01-16-PLAN.md", ".planning/phases/01-risiko-spikes/01-16-SUMMARY.md", ".planning/phases/01-risiko-spikes/01-17-PLAN.md", ".planning/phases/01-risiko-spikes/01-17-SUMMARY.md", ".planning/phases/01-risiko-spikes/01-18-PLAN.md", ".planning/phases/01-risiko-spikes/01-18-SUMMARY.md", ".planning/phases/01-risiko-spikes/01-19-PLAN.md", ".planning/phases/01-risiko-spikes/01-19-SUMMARY.md", ".planning/phases/01-risiko-spikes/01-HANDTEIL.md", ".planning/phases/01-risiko-spikes/01-SPIKE-ERGEBNISSE.md", ".planning/phases/01-risiko-spikes/01-SPIKE-frame-pacing.md", ".planning/phases/01-risiko-spikes/01-SPIKE-icons.md", ".planning/phases/01-risiko-spikes/01-SPIKE-menu.md", ".planning/phases/01-risiko-spikes/01-SPIKE-render-cost.md", ".planning/phases/01-risiko-spikes/01-SPIKE-tooling.md", ".planning/phases/01-risiko-spikes/01-SPIKE-window-hit.md", ".planning/phases/01-risiko-spikes/01-SPIKE-zoom-anchor.md", ".planning/phases/01-risiko-spikes/01-SPIKE-zorder-wind.md", "Directory.Packages.props", "global.json", "spikes/README.md", "spikes/WindowHost/HostWindow.cs", "spikes/WindowHost/Variants/MenuNative.cs", "spikes/WindowHost/Variants/ZOrderGuard.cs", "spikes/results/handteil/summary.csv", "spikes/results/q1/summary.md", "spikes/results/q2/s2-followup/summary-s2.md", "spikes/results/q3/menu.md", "spikes/results/q4-normal-summary.csv", "spikes/results/q4b-screen-summary.csv", "spikes/results/q6-icons.csv", "spikes/tools/check-notes.ps1", "spikes/tools/collect-decisions.ps1", "spikes/tools/handteil.ps1", "src/MehrBar.Core/Layout/ZoomEngine.cs", "src/MehrBar.Core/Layout/ZoomKernel.cs", "src/MehrBar.Tests/Fixtures/RocketDockReference.cs", "src/MehrBar.Tests/MehrBar.Tests.csproj", "src/MehrBar.Tests/ZoomAnchorTests.cs", "src/MehrBar.Tests/ZoomSmoothnessTests.cs"]
covered_digest: "v3:sha256:b6d9a02c2995a37b3c9bd2bc8e1f0d69cb8a81c09c5c8c9b3afe8863de764905"
behavior_unverified: 0
overrides_applied: 0
deferred:
  - truth: "SC4 (partial): on-screen frame pacing with 64 icons (build cost and CPU at 64 icons are measured; the displayed frame rate at 64 is not)"
    addressed_in: "Phase 3"
    evidence: "Phase 3 success criterion 3: 'Mit 60+ Symbolen (kleinere Grundgröße) läuft der Zoom mit 60 fps'; also listed in 01-SPIKE-ERGEBNISSE.md 'Offen vor Phase 3'"
human_verification:
  - test: "SC2, lock/unlock: decide whether the missing 'without guard' record for Sperren/Entsperren is acceptable, or run it once"
    expected: "Either accept the override below (lock/unlock was recorded only with guard S2 in hand step H2; the guard is mandatory anyway because S0 fails Win+D and the corner), or run 'Driver.exe zorder --guard S0 --trigger lock --allow-lock' from the desktop and add the result to 01-SPIKE-zorder-wind.md"
    why_human: "Lock/unlock needs the secure desktop and Stephan unlocking by hand; it cannot run from session 0. SC2 literally asks for 'mit und ohne Wächter' for every listed trigger, D-10 says the same, but plan 01-17 only scheduled the chosen guard"
  - test: "Plan 01-19 truth: confirm that the DirectComposition bar does not need an eye comparison against the WPF bar"
    expected: "Accept that WPF vs DirectComposition was decided on numbers only (10.4 % vs 9.1 % skipped periods), or look at CompositionHost once ('run-screen-compare.ps1 -Only dcomp-smooth' from the desktop) and note the impression in 01-SPIKE-frame-pacing.md"
    why_human: "The plan promised a comparison 'by numbers and by Stephan's eye'; H5 checked only the WPF bar by eye"
  - test: "Plan 01-17 truth / SC5 'Netzpfad': confirm that network drive roots (L:, M:) plus an unreachable path are enough, and that a real network file stays open for Phase 2"
    expected: "Accept as recorded in 01-SPIKE-icons.md (H7 net-file = skip), or run 'Icons.exe --case net-file --netfile <path> --threads sta --out spikes/results/private/q6-netfile.csv'"
    why_human: "Choosing a company network file is Stephan's call; the hand step was skipped"
---

# Phase 1: Risiko-Spikes Verification Report

**Phase Goal:** Die architekturbestimmenden Unbekannten sind auf diesem Rechner (Windows 11, Build 26200) mit Wegwerf-Prototypen gemessen und je mit einer Go/No-go-Notiz entschieden, bevor das Grundgerüst gebaut wird
**Verified:** 2026-10-19T14:30:00Z
**Status:** human_needed
**Re-verification:** No, initial verification

**Mode note:** ROADMAP marks phase 1 `mode: mvp`, but the goal is not a user story (`user-story.validate` returns `valid=false`). A spike phase has no user flow; the orchestrator requested the standard format, so standard goal-backward verification was applied and the User Flow Coverage table is omitted. Consider removing `mode: mvp` from phase 1 in ROADMAP.md.

## Goal Achievement

### Observable Truths (ROADMAP success criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | A throwaway window (~1900x250, transparent, alpha-1 hit area) catches clicks and moves over the bar, passes clicks 1 px beside it to the desktop, appears at the bottom without flashing (HWND_BOTTOM, TOOLWINDOW/NOACTIVATE) and does not take focus from an active Notepad | ✓ VERIFIED | `spikes/Common/SpikeGeometry.cs` 1900x250; `HostWindow.cs` sets TOOLWINDOW/NOACTIVATE, HWND_BOTTOM before first show, MA_NOACTIVATE. Raw `spikes/results/q1/hit.csv` recounted independently: H1 inside 100/100 arrived, outside 0/100 arrived (all five 1-px categories). `focus-notepad` 60/60, `firstframe` 10/10. Hand step H1 (no-flash, real-mouse, abort) = j in `spikes/results/handteil/summary.csv`. Decision GO H1 in `01-SPIKE-window-hit.md` |
| 2 | For Win+D, the taskbar "show desktop" corner, Win+M, Explorer restart and lock/unlock it is recorded whether the window stays visible with and without guard; the layer strategy is chosen | ? UNCERTAIN | Win+D, corner, Win+M, Explorer (and Alt+Tab, display, Peek) are recorded for S0, S1 and S2: `spikes/results/q2/runs.csv` (72 runs), `q2/s2-followup/runs-s2.csv` (106 runs, recomputed: win-d 20/20 back, max gap 236 ms, corner 271 ms, 0 in front). Strategy S2 chosen. **Lock/unlock is recorded only with guard S2** (hand step H2 `unlock-visible` = j); no "without guard" record exists. Strategy choice does not depend on it, so this is a human acceptance decision, not a blocker |
| 3 | WPF ContextMenu and TrackPopupMenu compared on the non-activating window (closes on outside click and Escape, arrows and Enter work, launched programs come to the front); menu technique decided | ✓ VERIFIED | `spikes/results/q3/menu.md`: M-A 4/8 mandatory points (fails close-desktop, close-window, close-escape, keys-enter), M-C 8/8 incl. launch-front 390 ms. Hand step H4: M-C all j, M-A close/keys n. `MenuNative.cs` implements SetForegroundWindow, TrackPopupMenuEx, WM_NULL. Decision GO M-C |
| 4 | With 64 icons at 60 fps frame time and CPU of the transparent window are measured (target <= 16 ms per frame, <= 15 % CPU; hardware vs SoftwareOnly), drawing technique decided with Go or fallback | ✓ VERIFIED | `spikes/results/q4-normal-summary.csv` and `q4-elevated-summary.csv`: 20 and 64 icons x hw/sw x R1/R2 (+opaque control). R1/64/hw cost p95 0.038 ms, CPU 0.60 %; R1/64/sw 0.085 ms, 0.91 %. Effective mode reported per cell. Decision GO R1, hardware; SoftwareOnly and DirectComposition (`spikes/CompositionHost`, builds clean) as fallbacks. The D-18 drop-share threshold (~1 %) was replaced by the RocketDock benchmark by Stephan (D-08, recorded in `01-SPIKE-render-cost.md`). On-screen pacing at 64 icons deferred to Phase 3 |
| 5 | A Core test reproduces the original's four measurement rows with anchor `cursor` within +-6 px; icon spike delivers 256-px icons with clean alpha for .exe (48-only and 256-only), .lnk incl. Store app, folder, .url, PDF and network path; CsWin32 build and VS Code test discovery settled | ✓ VERIFIED | `dotnet test` run by verifier: 28/28 green (VSTest). `ZoomAnchorTests.Cursor_anchor_predicts_reference_row(0..3)` with `Tolerance = 6`, plus `Center_anchor_misses_reference_rows` as falsification. `spikes/results/q6-icons.csv` contains exe-only48, exe-only256, lnk-file, lnk-store, appsfolder-store, folder, url-plain, url-iconfile, pdf, net-root-L/M, net-unreachable (timeout, non-blocking). H7 contact sheet = j. All five spike projects build `-c Release -warnaserror` with 0 warnings (run by verifier). Test discovery: MTP failed in H6, VSTest fallback B applied in `796053d` (`Directory.Packages.props`, `MehrBar.Tests.csproj`, `global.json` without test runner), H6 vstest-tests-listed/green = j |

**Score:** 4/5 roadmap truths verified (0 present, behavior-unverified; 1 uncertain)

Plan-level must-haves (01-01 .. 01-19) were checked for the plans that carry the phase result (01-13 .. 01-19). Two plan truths are not fully met and are routed to human verification: 01-19 "compares WPF and DirectComposition ... by Stephan's eye" (only numbers) and 01-17 "H7 ... one network file of his choice" (skipped).

### Deferred Items

| # | Item | Addressed In | Evidence |
|---|------|-------------|----------|
| 1 | On-screen frame pacing with 64 icons (SC4 partial) | Phase 3 | Phase 3 SC3: "Mit 60+ Symbolen ... läuft der Zoom mit 60 fps"; `01-SPIKE-ERGEBNISSE.md` "Offen vor Phase 3" |

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `01-SPIKE-window-hit.md` | Q1 note with decision | ✓ VERIFIED | `check-notes.ps1`: OK; GO H1 |
| `01-SPIKE-zorder-wind.md` | Q2 note with decision | ✓ VERIFIED | OK; GO S2 (header line "Grenzfälle teils erledigt, Rest offen" is stale, body says none open) |
| `01-SPIKE-menu.md` | Q3 note with decision | ✓ VERIFIED | OK; GO M-C |
| `01-SPIKE-render-cost.md` | Q4 note with decision | ✓ VERIFIED | OK; GO R1 hardware |
| `01-SPIKE-frame-pacing.md` | Q4b note (plan 01-19) | ⚠️ PARTIAL | Substantive, numbers match `q4b-screen-summary.csv`; has no `## Entscheidung` / `**Entscheidung:**` line and is not covered by `check-notes.ps1`; its decision is carried in `01-SPIKE-render-cost.md` and `01-SPIKE-ERGEBNISSE.md` |
| `01-SPIKE-zoom-anchor.md` | Q5 note with decision | ✓ VERIFIED | OK; GO cursor |
| `01-SPIKE-icons.md` | Q6 note with decision | ✓ VERIFIED | OK; GO STA |
| `01-SPIKE-tooling.md` | Q7 note with decision | ✓ VERIFIED | OK; GO VSTest |
| `01-SPIKE-ERGEBNISSE.md` | Summary with phase-2 scope changes, phase decision | ✓ VERIFIED | OK; "GO — Phase 2 kann geplant werden"; rows for all seven notes plus 01-19 |
| `01-HANDTEIL.md` + `spikes/results/handteil/summary.csv` | H1-H7 results | ✓ VERIFIED | Every H item has a row; H6 MTP n, VSTest j |
| `src/MehrBar.Core/Layout/ZoomEngine.cs`, `ZoomKernel.cs` | Pure zoom math (Compute, ComputeSmooth) | ✓ VERIFIED | Used by tests and by `spikes/WindowHost/Variants/RenderCost.cs` |
| `src/MehrBar.Tests/ZoomAnchorTests.cs`, `ZoomSmoothnessTests.cs` | SC5 Core test | ✓ VERIFIED | 28/28 green |
| `spikes/README.md` | Re-measure commands | ✓ VERIFIED | Nachmessen table lists Q1-Q5, Q4b, hand part, notes check |
| `01-VALIDATION.md` | Validation contract | ⚠️ STALE | Still `status: draft`, `nyquist_compliant: false`, all rows pending, framework listed as `xunit.v3.mtp-v2` (now VSTest) |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `spikes/results/handteil/summary.csv` | seven spike notes | `### Handprüfung (D-07)` subsections | ✓ WIRED | Each note carries a hand-check subsection matching the CSV rows |
| `01-SPIKE-ERGEBNISSE.md` | seven notes | Ergebnisse table | ✓ WIRED | One row per note, decisions match `collect-decisions.ps1` output |
| `spikes/tools/render-report.ps1` | `q4-*-summary.csv` | Import-Csv | ✓ WIRED | Note numbers match the CSVs |
| `ZoomEngine.ComputeSmooth` | `RenderCost.cs` (`--layout smooth`) | Core reference from WindowHost | ✓ WIRED | Used by H5 and 01-19 runs |
| Note decisions | Phase 2 scope | `## Änderungen am Umfang von Phase 2` | ✓ WIRED | ZOrderKeeper S2, alpha-1 plate, M-C, R1/hw, STA IconCache, VSTest |

### Data-Flow Trace (Level 4)

Not applicable in the UI sense. Equivalent check: note numbers traced back to committed raw CSVs.

| Artifact | Data | Source | Produces Real Data | Status |
|----------|------|--------|--------------------|--------|
| Q1 note table | H1/H2/H3 inside/outside counts | `spikes/results/q1/hit.csv` (recounted) | Yes, matches | ✓ FLOWING |
| Q2 note table | S2 follow-up per trigger | `q2/s2-followup/runs-s2.csv` (recomputed) | Yes, matches | ✓ FLOWING |
| Q4 note table | cost p95, CPU | `q4-normal-summary.csv` | Yes, matches | ✓ FLOWING |
| Q4b note table | skipped_pct | `q4b-screen-summary.csv` | Yes, matches | ✓ FLOWING |
| Q6 note table | cases, sizes | `q6-icons.csv` | Yes, matches | ✓ FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Product build | `dotnet build` | 0 warnings, 0 errors | ✓ PASS |
| Core tests incl. SC5 zoom test | `dotnet test` | 28/28 passed (VSTest) | ✓ PASS |
| Test discovery lists the four reference rows | `dotnet test --list-tests` | `Cursor_anchor_predicts_reference_row(rowIndex: 0..3)` listed | ✓ PASS |
| Spike projects build with CsWin32 | `dotnet build spikes/{WindowHost,Driver,Icons,CompositionHost,ZoomCheck} -c Release -warnaserror` | 0 warnings, 0 errors each | ✓ PASS |
| Notes carry exactly one decision line | `check-notes.ps1` | 8x OK, exit 0 | ✓ PASS |
| Checker rejects broken notes | `check-notes.ps1 -SelfTest` | SELFTEST OK | ✓ PASS |
| Hand part overview | `handteil.ps1 -List` | H1-H7 + G listed, exit 0 | ✓ PASS |
| Decision collection | `collect-decisions.ps1` | 7 notes, all GO | ✓ PASS |
| Mouse-moving runs (Q1-Q4b) | not run | Session 0 cannot run them (by instruction) | ? SKIP (covered by committed CSVs and the hand part) |

### Probe Execution

No `scripts/*/tests/probe-*.sh` exist and no plan declares one. The phase "runs" are Driver/PowerShell measurement scripts that move the mouse; they were not executed here (session 0). Step 7c: not applicable.

### Requirements Coverage

Phase 1 has no requirement IDs (ROADMAP: "Requirements: None"). No orphaned requirements.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `spikes/`, `src/` (.cs/.ps1) | - | TBD/FIXME/XXX/TODO/HACK | none found | - |
| `01-SPIKE-menu.md` | Grenzfälle | Says "kein Handergebnis weicht von der Messung ab", but H4 M-C `focus-back` = j while the automated `focus-after` was ✗ | ℹ️ Info | Wording inconsistency; Phase 2 restores focus explicitly anyway |
| `spikes/results/handteil/summary.csv` | H6 | `sln-loads` = n with no post-fallback row; `01-HANDTEIL.md` claims "j nach Rückfall B" | ℹ️ Info | Evidence gap only; test explorer working implies the solution loaded |
| `01-SPIKE-zorder-wind.md` | 3 | Stale header "Grenzfälle teils erledigt, Rest offen" | ℹ️ Info | Contradicts the closing section |
| `01-VALIDATION.md` | frontmatter | Still draft, MTP, all pending | ⚠️ Warning | Run `/gsd-validate-phase 1` or update before milestone audit |
| `.planning/ROADMAP.md` | Phase 1 | "15/19 plans executed", 01-16..01-19 unchecked although all SUMMARYs exist | ℹ️ Info | Orchestrator bookkeeping |

No foreign skins, screenshots or private results are tracked (`git ls-files spikes/results/private samples` empty; `.gitignore` covers `spikes/results/private/`).

### Human Verification Required

#### 1. Lock/unlock without guard (SC2)

**Test:** Decide whether SC2 is met with lock/unlock recorded only under S2, or run `Driver.exe zorder --guard S0 --trigger lock --allow-lock` once from the desktop.
**Expected:** Override accepted, or an S0 lock result added to `01-SPIKE-zorder-wind.md`.
**Why human:** Secure desktop and manual unlock; SC2 and D-10 ask for "mit und ohne Wächter" for every trigger, plan 01-17 scheduled only the chosen guard.

**This looks intentional.** To accept, add to this file's frontmatter:

```yaml
overrides:
  - must_have: "Für Win+D, Desktop anzeigen in der Taskleisten-Ecke, Win+M, Explorer-Neustart und Sperren/Entsperren ist protokolliert, ob das Fenster mit und ohne Wächter sichtbar bleibt; die Strategie für die Fensterebene ist gewählt"
    reason: "Lock/unlock recorded only with the chosen guard S2 (H2 passed); a guard is mandatory anyway because S0 fails Win+D and the corner, so a lock run without guard cannot change the strategy"
    accepted_by: "Stephan"
    accepted_at: "<ISO timestamp>"
```

#### 2. DirectComposition by eye (plan 01-19)

**Test:** Accept the numbers-only comparison, or watch `spikes/CompositionHost` once next to the WPF bar.
**Expected:** Decision noted in `01-SPIKE-frame-pacing.md`.
**Why human:** Plan promised an eye comparison; only the WPF bar was judged by eye (H5).

#### 3. Real network file (plan 01-17, SC5)

**Test:** Accept drive roots + unreachable path as "Netzpfad", or run the net-file case with a company share file.
**Expected:** Acceptance, or a row in `spikes/results/private/q6-netfile.csv` and a line in `01-SPIKE-icons.md`.
**Why human:** File choice is Stephan's; H7 net-file was skipped.

### Gaps Summary

No blocking gaps. Every architecture-relevant unknown (hit area, layer/Win+D, menu, render cost, frame pacing, zoom anchor, icons, tooling) has committed raw data, a note with exactly one GO decision, a hand cross-check, and a phase-2 scope consequence in `01-SPIKE-ERGEBNISSE.md`. The verifier re-ran the build, the tests (28/28), the five spike builds and the note checker; all green. Remaining items are three acceptance decisions for literal contract deviations (lock/unlock without guard, DirectComposition eye check, real network file), plus documentation hygiene (stale VALIDATION.md, frame-pacing note without a decision line, small wording inconsistencies). On-screen pacing with 64 icons is deferred to Phase 3 SC3.

---

_Verified: 2026-10-19T14:30:00Z_
_Verifier: Claude (gsd-verifier)_
