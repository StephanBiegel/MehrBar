# Spike Q4 — Zeichenkosten des transparenten Leistenfensters

**Stand:** 2026-10-12 · Plan 01-16 (Auswertungskette steht, Messung nach Haltepunkt ausstehend) · Code: `spikes/WindowHost/Variants/RenderCost.cs`, `spikes/Driver/Cases/RenderCase.cs` · Läufe: `spikes/tools/run-render-matrix.ps1` · Auswertung: `spikes/tools/render-report.ps1`

## Frage

Q4 (ROADMAP, Erfolgskriterium 4): Kann ein transparentes WPF-Fenster von rund 1900×250 px (`AllowsTransparency=true`) den Zoom mit 64 Symbolen bei 60 fps zeichnen — gemessen als Frame-Zeit und CPU, Hardware gegen `SoftwareOnly` — und welche Zeichentechnik und welcher Render-Modus gelten dafür in Phase 2?

Maßgeblich für das Go ist die **reale Leiste mit etwa 20 Symbolen** (D-13). Verfehlen 64 Symbole die Ziele, ist das kein No-go, sondern ein offener Punkt vor Phase 3 (ZOOM-06) mit gemessenem Ausweichweg aus der Leiter in `PITFALLS.md` Pitfall 3.

## Aufbau

**Arbeitslast (D-17):** 20 und 64 Symbole aus synthetischen 256-px-`Pbgra32`-Bitmaps (eingefroren). Größe und Lage je Frame kommen aus der Produktmathematik `MehrBar.Core` `ZoomEngine` mit Verankerung `cursor` (Basis 32 px bei 20 Symbolen, 24 px bei 64; Zoom 121 px; Radius 2,5 Symbolabstände) um die per `GetCursorPos` gelesene Mausposition: Das Symbol unter der Maus bleibt stehen, die Nachbarn weichen aus, die Leiste wächst in der Breite, und damit bewegt sich in jedem Frame jedes Symbol. Eine erste Fassung ließ die Symbole nur um ihre feste Ruhemitte wachsen (Nachbarn überlappten); das bevorzugte R2, weil sich dort nur die ~5 Symbole im Zoombereich änderten. Die 7 Zellen eines abgebrochenen Laufs mit dieser Fassung (2026-10-13) zählen nicht. Bei 64 Symbolen (Ruhebreite 1792 px, gezoomt bis gut 2050 px) ragen die äußersten Symbole über das 1900 px breite Fenster hinaus und werden abgeschnitten.

**Zeichenvarianten:**

| Variante | Verfahren |
|---|---|
| R1 | ein `FrameworkElement.OnRender` mit `InvalidateVisual()` je Frame |
| R2 | ein `DrawingVisual` je Symbol, je Frame ändern sich nur Transformationen (retained) |
| K | Kontrolle: gleiche Zeichnung in einem **nicht** transparenten Fenster (trennt Transparenzkosten von Zeichenkosten), nur mit `-Control` |

**Render-Modus:** Hardware gegen `RenderOptions.ProcessRenderMode = SoftwareOnly` (vor dem ersten Fenster gesetzt). Die Dokumentation nennt das eine Präferenz; deshalb gibt der Host den **tatsächlich wirksamen** Modus aus (`effective_mode`, `tier`), und die Notiz nennt ihn je Zelle.

**Lauf (D-17):** Der Driver fährt die Maus 30 s lang auf einer festen Sinusbahn per `SendInput` über die Leiste (alle Eingaben in einer `SafetySession`, Notabbruch Strg+Alt+Q), davor 5 s Einlaufzeit (verworfen), danach 5 s Ruhelauf ohne Maus. Matrix: 20 und 64 Symbole × Hardware und `SoftwareOnly` × R1 und R2 (plus K bei 64 Symbolen) — 8 Zellen plus K, etwa 6 bis 10 Minuten reine Laufzeit.

**Drei Messquellen (D-16):**

1. *Host-Selbstmessung:* Aufbaukosten je Frame (Stopwatch um Handler und `OnRender`), Kadenz (Abstand zweier `CompositionTarget.Rendering`-Ereignisse, doppelte Zeitstempel übersprungen), GC-Zähler und Allokation je Frame.
2. *Prozess-CPU:* `GetProcessTimes` für den Host und `dwm.exe`; CPU in Prozent aller logischen Prozessoren (die Task-Manager-Spalte, D-19) **und** Kernäquivalente (Δ CPU / Wandzeit), weil 15 % auf diesem Rechner (20 logische Prozessoren) drei Kernen entsprechen. `dwm.exe` ist ohne Erhöhung nicht lesbar (`n/a`).
3. *PresentMon 2.6.0* (D-16, nur erhöht, ETW): unabhängig davon, was der DWM tatsächlich darstellt. Ob PresentMon das Layered-Fenster überhaupt sieht, ist Annahme A3 und wird im Vorversuch (`spikes/results/q4-pretest.md`) geklärt. Die Spaltennamen der CSV werden zur Laufzeit aus der Kopfzeile gelesen (A4). Die Binärdatei wird per SHA-256 geprüft (`spikes/tools/get-presentmon.ps1`) und liegt in einem ignorierten Ordner, nie im Repo.

Host-Kadenz und DWM-Zähler (`DwmGetCompositionTimingInfo`: verworfene und verpasste Frames) sind **Eigenmessung** und erscheinen immer; sie ersetzen PresentMon aber nie stillschweigend.

**Auslegung der Schwellen (Klarstellung zu D-18, keine Änderung der Zahlen):** Bei 60 Hz hat eine perfekte Kadenz 16,67 ms plus Jitter; „p95 ≤ 16,7 ms“ auf die *Kadenz* wäre durch Jitter allein gerissen (Pitfall 11). Deshalb werden Kosten und Ausfälle getrennt ausgewertet:

| Kriterium | Maß | Schwelle | Quelle |
|---|---|---|---|
| D-18 p95 | 95. Perzentil der **Aufbaukosten** je Frame | ≤ 16,7 ms (daneben PresentMon-p95 des Anzeigewechsel-Abstands, ≤ 17 ms, weil auf Perioden quantisiert) | Host; PresentMon daneben |
| D-18 Ausfälle | Anteil Anzeigewechsel-Abstände ≥ 2 Perioden (bei 60 Hz ≈ 33 ms; Pausen über 250 ms zählen nicht) | ≤ ~1 % | **nur PresentMon** |
| D-19 CPU | `cpu_total_pct` des Hosts (Prozent aller logischen Prozessoren) | ≤ 15 % | Prozesszeit; Kernäquivalente und `dwm.exe`-Mehraufwand werden mitgenannt |

Fehlen PresentMon-Daten für eine entscheidende Zelle (Fenster nicht gesehen, Hash-Fehler, UAC abgelehnt), ist die Ausfallquote dieser Zelle **UNKLAR**; der Host-Kadenzwert (Abstand > 25 ms) und die DWM-Zähler stehen dann in einer eigenen Spalte „Ersatz, nicht D-18“, und der Fall geht als Grenzfall an Stephan (D-08). Werte innerhalb von 10 % einer Schwelle und Zellen, bei denen die Quellen auseinanderliegen, gelten ebenfalls als Grenzfälle.

**Entscheidungskriterium (D-13, D-08):** Beste Variante und bester Modus ist die billigste Kombination (p95-Kosten, dann CPU), die bei 20 Symbolen D-18 und D-19 besteht; die Ausfallquote zählt nur mit PresentMon-Daten als bestanden. Besteht keine Kombination bei 20 Symbolen, ist es NO-GO mit dem nächsten Ausweichschritt aus Pitfall 3 (getrenntes Beschriftungsfenster, `WindowChrome`-Glass, Direct2D-/DirectComposition-Fenster). Verfehlen 64 Symbole die Ziele, bleibt es GO und der Punkt heißt „offen vor Phase 3“, mit der gemessenen ersten Stufe der Leiter (Fenster 1000×160).

**Nachmessen:**

```
powershell -NoProfile -ExecutionPolicy Bypass -File spikes/tools/run-render-matrix.ps1 -DryRun -Label dry
powershell -NoProfile -ExecutionPolicy Bypass -File spikes/tools/render-report.ps1 -Summaries spikes/results/dry/q4-dry-summary.csv -Out spikes/results/dry/q4-report.md
powershell -NoProfile -ExecutionPolicy Bypass -File spikes/tools/get-presentmon.ps1
```

Der echte Lauf sendet Eingaben und fragt vorher nach „GO“ (`-Yes` überspringt das nur nach dem Haltepunkt). Erhöht mit PresentMon: `run-render-matrix.ps1 -PresentMon -Label elevated`; nicht erhöht: `run-render-matrix.ps1 -Label normal -Control`; Fallback-Stufe: `run-render-matrix.ps1 -Label fallback -Counts 64 -Sizes 1000x160 -Variants <beste> -Modes <bester>`. Auswertung: `render-report.ps1 -Summaries <csv,...> -PresentMonDir <Rohordner> -Out spikes/results/q4-report.md`.

## Messwerte

Noch nicht gemessen. Die Läufe folgen nach dem Haltepunkt (ruhiger Rechner, eine UAC-Bestätigung). Die Tabelle mit den D-18-/D-19-Urteilen je Zelle steht dann in `spikes/results/q4-report.md`; die Rohzusammenfassungen liegen als `spikes/results/q4-elevated-summary.csv`, `spikes/results/q4-normal-summary.csv` und gegebenenfalls `spikes/results/q4-fallback-summary.csv`, das Ergebnis des PresentMon-Vorversuchs in `spikes/results/q4-pretest.md`.

Der Trockenlauf (`-DryRun`, synthetisch, ohne Eingabe und ohne PresentMon) belegt nur, dass die Kette Lauf → Zusammenfassung → Auswertung → Urteilsspalten funktioniert; seine Werte sind keine Messwerte und stehen nicht im Repo (`spikes/results/dry/`).

## Entscheidung

**Entscheidung:** UNKLAR — Messung nach Haltepunkt ausstehend

## Folgen für Phase 2/3

Nach der Messung auszufüllen: Zeichentechnik für `IBarSurface` (R1 oder R2), Render-Modus (Hardware oder `SoftwareOnly`), Fenstergrößen-Politik, und was Phase 3 für 60+ Symbole noch beweisen muss.

## Grenzfälle für Stephan

Nach der Messung auszufüllen. Fest eingeplant ist H5 (Handprüfung, D-07): echtes Mausgefühl über 20 und 64 Symbole mit `Driver.exe render --variant <beste> --count 64 --render <Modus> --manual --seconds 30`.
