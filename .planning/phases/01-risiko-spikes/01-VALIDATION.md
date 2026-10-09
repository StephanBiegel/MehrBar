---
phase: "01"
slug: "risiko-spikes"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: "2026-10-09"
---

# Phase 01 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: `01-RESEARCH.md` § Validation Architecture. This phase delivers mostly measurement notes;
> "validation" means: every note exists, is complete, carries exactly one decision line, and the
> measurement runs are repeatable. The only true unit test is the zoom-anchor test (SC5).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 `xunit.v3.mtp-v2` 4.0.1 on `net10.0` (Core tests); spikes: Driver runs + note checker script |
| **Config file** | `global.json` (MTP runner), `Directory.Packages.props` — none yet, Wave 0 installs |
| **Quick run command** | `dotnet test` |
| **Full suite command** | `dotnet test` + `powershell -File spikes/tools/check-notes.ps1` + `dotnet build` of all spike projects |
| **Estimated runtime** | ~30 seconds (excluding render/z-order measurement runs) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test` (Core) or the affected spike run in short form (`--repeat 1`)
- **After every plan wave:** Run `dotnet test` + `check-notes.ps1` + `dotnet build` of all spike projects
- **Before `/gsd-verify-work`:** All seven notes present and complete, summary note, manual part H1–H7 checked off, Core test green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

Criterion-level map from research; the planner binds task IDs in the PLAN files.

| Criterion | Behavior | Test Type | Automated Command | Plan/Task | File Exists | Status |
|-----------|----------|-----------|-------------------|-----------|-------------|--------|
| SC5 (Zoom) | Cursor anchor predicts uncropped edges of the four series ±6 px from independently measured inputs; `center` misses (falsification control) and keeps the middle | unit | `dotnet test` (filter syntax recorded in 01-SPIKE-tooling.md) | 01-05 T1–T3 | ❌ W0 | ⬜ pending |
| SC5 (Tooling) | Spike host builds with CsWin32; `dotnet test` green | smoke | `dotnet build spikes/WindowHost -c Release -warnaserror && dotnet test` | 01-01 T1, 01-02 T1, 01-08 T1–T2; H6 in 01-17 T2; fallback in 01-18 T2 | ❌ W0 | ⬜ pending |
| SC1 | Click capture/pass-through, focus, base visibility, first frame at bottom | integration (Driver, after checkpoint) | `spikes/Driver/bin/Release/net10.0-windows/Driver.exe hit --variants H1,H2,H3 …` via `powershell -File spikes/tools/run-q1.ps1` | instrument 01-03, 01-04, 01-07, 01-09; run 01-13 T1–T3 | ❌ W0 | ⬜ pending |
| SC2 | Trigger × guard matrix, gap/cover | integration (Driver, after consent checkpoint) | `powershell -File spikes/tools/run-zorder.ps1 -AllowExplorer [-AllowDisplay]` | instrument 01-10 T1–T2; run 01-14 T1–T3 | ❌ W0 | ⬜ pending |
| SC3 | Menu points per variant (synthetic input) | integration (Driver, after checkpoint) | `powershell -File spikes/tools/run-menu.ps1` | instrument 01-11 T1–T2; run 01-15 T1–T3 | ❌ W0 | ⬜ pending |
| SC4 | Render runs: p95 cost, drop share per PresentMon only (D-18; UNKLAR + labelled substitute when PresentMon has no data), CPU %, core equivalents | integration (Driver + Host, PresentMon elevated if usable) | `powershell -File spikes/tools/run-render-matrix.ps1` + `render-report.ps1` | instrument 01-12 T1–T3; run 01-16 T1–T3 | ❌ W0 | ⬜ pending |
| SC5 (Icons) | Cases × size/alpha/latency, contact sheet | integration | `spikes/Icons/bin/Release/net10.0-windows/Icons.exe --all` / `--latency` | 01-06 T1–T3; H7 in 01-17 T2 | ❌ W0 | ⬜ pending |
| all | One note per spike with exactly one decision line | smoke | `powershell -File spikes/tools/check-notes.ps1` | 01-01 T2 (checker), each note task, 01-17 T3 (seven notes), 01-18 T3 (all) | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `global.json`, `Directory.Packages.props`, solution, `src/MehrBar.Core`, `src/MehrBar.Tests` — scaffold per D-02, plus a first trivial green test
- [ ] `src/MehrBar.Tests/ZoomAnchorTests.cs` — red tests from the fixture (Q5)
- [ ] `spikes/Common/Oracles.cs` + **oracle self-test** (pixel probe visible/covered) — without it Q1–Q3 are worthless
- [ ] Spike host, Driver, Icons project skeletons with the CsWin32 name list; `spikes/tools/check-notes.ps1`; PresentMon download (hash check) and layered-visibility pre-test
- [ ] `.gitignore` additions: `spikes/tools/PresentMon*.exe`, `spikes/results/private/`, `spikes/fixtures/*.exe`
- [ ] Framework install: SDK present; packages via `dotnet restore`

---

## Manual-Only Verifications

| Behavior | Criterion | Why Manual | Test Instructions |
|----------|-----------|------------|-------------------|
| "No flash" on window appearance | SC1 | Human eye | Manual part H1 |
| Lock/unlock, real key per trigger | SC2 | Secure desktop / real hardware keys | Manual part H2/H3 |
| Real keyboard in menus, foreground visible, look | SC3 | D-07 requires real keyboard once per variant | Manual part H4 |
| Real-mouse feel of the render load (D-07 cross-check of the sweep) | SC4 | Perceived smoothness, real input | Manual part H5 |
| VS Code Test Explorer discovers tests | SC5 | Dev Kit UI, no CLI | Manual part H6 |
| Eye check of icon contact sheet | SC5 | Visual alpha quality | Manual part H7 |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
