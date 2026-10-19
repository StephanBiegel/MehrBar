# Phase 1 — Ergebnisse der Risiko-Spikes

**Stand:** 2026-10-19 · Plan 01-18 · Grundlage: die sieben Notizen `01-SPIKE-*.md` nach dem Handteil (01-17, `01-HANDTEIL.md`) und die Bildtempo-Notiz `01-SPIKE-frame-pacing.md` (01-19) · Gemessen auf Windows 11 Build 26200, ein Monitor, 100 % Skalierung, 60 Hz

## Ergebnisse je Spike

| Spike | Notiz | Entscheidung | Begründung in einer Zeile |
|---|---|---|---|
| Q1 Trefferfläche, Durchlass, erstes Bild, Fokus | `01-SPIKE-window-hit.md` | GO — H1 | Alpha-1-Platte fängt 100/100 innen, lässt 100/100 daneben durch, erstes Bild unten 10/10, Fokus 300/300; von Hand bestätigt |
| Q2 Ebene unten gegen Win+D und Verwandte | `01-SPIKE-zorder-wind.md` | GO — S2 | Wächter setzt die Leiste direkt über den Desktop-Host, nie topmost; besteht D-11 bei allen Auslösern, auch mit echten Tasten (H2/H3) |
| Q3 Kontextmenü | `01-SPIKE-menu.md` | GO — M-C | natives `TrackPopupMenuEx` mit der Leiste als Besitzer besteht alle Pflichtpunkte, auch von Hand; M-A scheitert |
| Q4 Zeichenkosten | `01-SPIKE-render-cost.md` | GO — R1, Hardware | Aufbau p95 0,017 ms, 0,43 % CPU bei 20 und 64 Symbolen; Ausfallquote am Bildschirm gemessen, Maßstab RocketDock (Stephan) |
| Q5 Zoom-Verankerung | `01-SPIKE-zoom-anchor.md` | GO — `cursor` | sagt alle unbeschnittenen Ränder auf ±6 px voraus, `center` verfehlt bis 37,9 px; Kalibrierung in Phase 3 |
| Q6 Symbole in hoher Auflösung | `01-SPIKE-icons.md` | GO — STA | 12/13 Fälle 256 px mit Straight-Alpha, `.url` nur auf STA, Zeitlimit blockiert nichts; Kontaktbogen von Hand sauber |
| Q7 Werkzeugkette | `01-SPIKE-tooling.md` | GO — VSTest | CsWin32 0.3.360 belegt; Tests über VSTest (Rückfall B), weil der VS-Code-Testexplorer MTP nicht fand |

Zusatz aus Plan 01-19 (`01-SPIKE-frame-pacing.md`): Das Zittern der äußeren Symbole kam aus der abgetasteten Cursor-Aufteilung und ist mit `ZoomEngine.ComputeSmooth` behoben. Am Bildschirm verliert die WPF-Leiste 10,4 % der Takte, eine DirectComposition-Leiste 9,1 %, RocketDock 7–12 %: WPF reicht.

## Änderungen am Umfang von Phase 2

- **Ebene und Wächter (Q2, `01-SPIKE-zorder-wind.md`):** `ZOrderKeeper` ist nötig und folgt S2: jede Z-Änderung in `WM_WINDOWPOSCHANGING` auf `HWND_BOTTOM` umschreiben, dazu WinEvent `EVENT_SYSTEM_FOREGROUND` plus Takt 250 ms (100 ms, solange der Desktop gezeigt wird); liegt der Desktop-Host über der Leiste, die Leiste direkt über ihn setzen, nie topmost. Desktop-Host neu suchen bei `TaskbarCreated` und `WM_DISPLAYCHANGE`. Abnahme mit `run-zorder.ps1 -Guards S2`.
- **Trefferfläche (Q1, `01-SPIKE-window-hit.md`):** Alpha-1-Platte unter den Symbolen, kein `SetWindowRgn`, kein Umschalten von `WS_EX_TRANSPARENT`. `TOOLWINDOW|NOACTIVATE`, `MA_NOACTIVATE` und `HWND_BOTTOM` vor dem ersten Bild (Hook auf das erste WPF-Show). Rückfall: H2 (Region je Layoutwechsel).
- **Menütechnik für MENU-03 (Q3, `01-SPIKE-menu.md`):** M-C, natives Menü mit der Leiste als Besitzer, `SetForegroundWindow` davor, `WM_NULL` danach; das vorherige Vordergrundfenster ausdrücklich zurückgeben und die Leiste danach auf `HWND_BOTTOM` setzen. Im Abnahmetest nachmessen: Zusammenspiel mit dem S2-Wächter und `EndMenu` von außen.
- **Zeichentechnik und Render-Modus (Q4/Q4b, `01-SPIKE-render-cost.md`, `01-SPIKE-frame-pacing.md`):** `IBarSurface` mit R1 (ein `OnRender` je Bild, alles neu), transparentes WPF-Fenster, Hardware; `SoftwareOnly` als Ausweichweg. Zoom-Rechnung `ComputeSmooth`, keine Pixelrundung. DirectComposition (`spikes/CompositionHost`) bleibt dokumentierter Rückfall.
- **Symbole (Q6, `01-SPIKE-icons.md`):** IconCache auf 2–4 STA-Arbeitsthreads, `SIIGBF_ICONONLY`, einmal nach `Pbgra32` und `Freeze()`; Zeitlimit 3 s je Eintrag, Platzhalter während des Ladens; Ersatzregeln für 48-px-Quellen und `.url`.
- **Testläufer und Lösungsformat (Q7, `01-SPIKE-tooling.md`):** xUnit v3 über VSTest (`xunit.v3.mtp-off`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`), seit 2026-10-19 im Repo; klassische `MehrBar.sln`. Der CsWin32-Pin 0.3.360 kommt mit `MehrBar.App` ins Wurzel-`Directory.Packages.props`.
- **Arbeitsweise:** Bildschirmläufe (Maus, Notabbruch, Desktop Duplication) laufen nur vom Desktop aus, nicht aus einer Hivemo-Sitzung (Windows-Sitzung 0).

## Offen vor Phase 3

- **64 Symbole (D-13):** Zeichenkosten bestehen (Aufbau p95 0,017 ms), von Hand flüssig (H5); das Bildtempo am Bildschirm ist mit 64 Symbolen ungemessen. In Phase 3 mit `run-screen-compare.ps1` nachholen.
- **Zoom-Kalibrierung:** Vollbild-Neuaufnahmen des Originals und die Reihe Zoom-Breite 2/6/8 (Stephan, braucht RocketDock); danach `r` neu festlegen und den Kernel prüfen. RocketDock am selben Tag wie die eigene Leiste nachmessen.
- **Trefferfläche im Zoom-Überhang:** muss mit dem Zoom mitwachsen (aus Q1 abgeleitet, nicht gemessen); Prüfung in der Abnahme von ZOOM-02.
- **Messbedingungen:** 125/150 % Skalierung, zweite (rechte) Leiste, Symbole mit 64/128-px-Rahmen und eine echte Netzdatei sind ungemessen und kommen mit Phase 2.
- Keine Notiz ist UNKLAR.

## Entscheidung

**Entscheidung:** GO — Phase 2 kann geplant werden
