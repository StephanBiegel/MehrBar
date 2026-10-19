---
phase: "02"
slug: "grundger-st"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: "2026-10-19"
---

# Phase 02 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Quelle: `02-RESEARCH.md` § Validation Architecture. Alles ohne Oberfläche Prüfbare liegt in
> `MehrBar.Core` und wird mit xUnit geprüft; Fenster, Tray, Drag & Drop und Menüs werden von Hand
> **am Desktop** abgenommen (nicht aus einer Hivemo-Sitzung / Windows-Sitzung 0, siehe `spikes/README.md`).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 4.0.1 (`xunit.v3.mtp-off`) über VSTest (`xunit.runner.visualstudio` 4.0.0, `Microsoft.NET.Test.Sdk` 18.10.1) — D-06 |
| **Config file** | `src/MehrBar.Tests/MehrBar.Tests.csproj` (`net10.0`, `OutputType=Exe`); `global.json` ohne `test`-Block |
| **Quick run command** | `dotnet test src/MehrBar.Tests --nologo --filter "FullyQualifiedName~<Bereich>"` |
| **Full suite command** | `dotnet test --nologo` und `dotnet build MehrBar.sln -c Release -warnaserror` |
| **Estimated runtime** | ~10 Sekunden (Basis heute: 28 Tests, ~3 s) |

Hinweis: Unter VSTest meldet ein Filter ohne Treffer nur „keine Tests“ — Verifikationsbefehle prüfen die
**Anzahl** gelaufener Tests, nicht nur den Exitcode (01-SPIKE-tooling.md).

---

## Sampling Rate

- **After every task commit:** `dotnet test src/MehrBar.Tests --nologo --filter "FullyQualifiedName~<betroffener Bereich>"`
- **After every plan wave:** `dotnet test --nologo` + `dotnet build MehrBar.sln -c Release -warnaserror`
- **Before `/gsd-verify-work`:** volle Suite grün + `dotnet publish src/MehrBar.App -c Release` (genau eine `MehrBar.exe`) + Handprüfliste abgehakt
- **Max feedback latency:** 15 Sekunden

---

## Per-Task Verification Map

Wird beim Planen bzw. von `/gsd-validate-phase` je Task gefüllt. Ausgangsbelegung nach Anforderung:

| Requirement | Behavior | Test Type | Automated Command | File Exists | Status |
|-------------|----------|-----------|-------------------|-------------|--------|
| LEIS-01 | Standardwerte, Erststart schreibt `config.json` | unit | `--filter "FullyQualifiedName~DefaultsTests\|FullyQualifiedName~ConfigStoreTests"` | ❌ W0 | ⬜ pending |
| LEIS-02 | `BarPlacement` je Ausrichtung/Versatz/negativem `edgeOffset`, Monitorwechsel | unit | `--filter "FullyQualifiedName~BarPlacementTests"` | ❌ W0 | ⬜ pending |
| LEIS-03/05 | `ZOrderPolicy`-Zustandsfolge, Host verschwunden | unit | `--filter "FullyQualifiedName~ZOrderPolicyTests"` | ❌ W0 | ⬜ pending |
| LEIS-06 | Fensterplan = Ruhebreite + Wachstum, Platte ⊂ Fenster | unit | `--filter "FullyQualifiedName~WindowPlanTests"` | ❌ W0 | ⬜ pending |
| LEIS-07 | `FrontMode` Ende bei Drop/30 s/Beenden (injizierte Uhr) | unit | `--filter "FullyQualifiedName~FrontModeTests"` | ❌ W0 | ⬜ pending |
| ITEM-01/03 | `LaunchResolver` (.lnk bleibt, `%USERPROFILE%`, Ordner, Missing) | unit | `--filter "FullyQualifiedName~LaunchResolverTests"` | ❌ W0 | ⬜ pending |
| DND-01 | `DropClassifier`, Drop-Effekt nie `Move` | unit | `--filter "FullyQualifiedName~Drop"` | ❌ W0 | ⬜ pending |
| CONF-01/02 | Round-Trip, unbekannte Felder/Typen, Backup, defekte Datei | unit | `--filter "FullyQualifiedName~Config"` | ❌ W0 | ⬜ pending |
| APP-05 | `RollingFileLog` Rotation, wirft nie | unit | `--filter "FullyQualifiedName~RollingFileLogTests"` | ❌ W0 | ⬜ pending |
| (Vertrag) | `FrameLayout(progress 0)` == Ruhelayout; Treffer über Gesamtlisten-Index | unit | `--filter "FullyQualifiedName~FrameLayoutTests\|FullyQualifiedName~HitTestTests"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `src/MehrBar.Tests/Config/*Tests.cs` — CONF-01/02, LEIS-01/02
- [ ] `src/MehrBar.Tests/Layout/*Tests.cs` — LEIS-02/06, Layoutvertrag
- [ ] `src/MehrBar.Tests/Windowing/*Tests.cs` — LEIS-03/05/07
- [ ] `src/MehrBar.Tests/Drop/*Tests.cs` — DND-01
- [ ] `src/MehrBar.Tests/Launch/LaunchResolverTests.cs` — ITEM-01/03
- [ ] `src/MehrBar.Tests/Logging/RollingFileLogTests.cs` — APP-05
- [ ] Fixtures unter `src/MehrBar.Tests/Fixtures/` (z. B. `config.future.json` mit `group`, unbekannten Feldern, `type` nicht zuerst; defekte Dateien)

*Testframework ist vorhanden (Phase 1); keine neuen Testpakete.*

---

## Manual-Only Verifications

Alle Punkte **vom Desktop aus** starten, nicht aus einer Hivemo-Sitzung (Sitzung 0 hat keinen Desktop,
`UserInteractive=False`). Vorher RocketDock beenden.

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Erster Start: leerer Kasten oben mittig, `config.json` angelegt | LEIS-01 | echtes Fenster | H-FIRST (02-RESEARCH.md) |
| Leiste hinter jedem Fenster, Fokus bleibt, Klick-Durchlass | LEIS-03/04/06 | Fensterebene/Fokus nur am Desktop sichtbar | H-Z, H-FOCUS, H-CLICK |
| Win+D, „Desktop anzeigen“, Explorer-Neustart, Auflösungswechsel | LEIS-05 | Shell-Verhalten | H-WIND |
| „Leiste nach vorn“, Ende nach Drop/30 s | LEIS-07 | Explorer-Interaktion | H-FRONT |
| Drop aus dem Explorer, Quelle unverändert | DND-01 | OLE-Drag & Drop | H-DND |
| „Eintrag hinzufügen…“ per Dateiauswahl | DND-02 | Dialog | H-ADD |
| Menüs schließen bei Klick daneben/Escape | MENU-03 | natives Menü | H-MENU |
| Start von exe, Datei, Store-App-`.lnk`, Ordner, `%USERPROFILE%`; fehlendes Ziel | ITEM-01/03 | Prozessstart, Vordergrund | H-LAUNCH, H-MISSING |
| Überlebt Neustart von App und Windows, Backup vorhanden | CONF-01/02 | Neustart | H-PERSIST |
| Tray-Einträge, Explorer-Neustart | APP-01 | Tray | H-TRAY |
| Fehler im Log | APP-05 | Zusammenspiel | H-LOG |
| Einzelinstanz, Publish als eine Datei, Startzeit notiert | — | Prozess/Publish | H-ONCE, H-PUBLISH |
| Stichprobe 125/150 % | — | Skalierung | H-SCALE (Befund notieren, Behebung Phase 7) |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
