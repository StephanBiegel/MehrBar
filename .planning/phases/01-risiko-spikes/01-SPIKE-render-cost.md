# Spike Q4 — Zeichenkosten des transparenten Leistenfensters

**Stand:** 2026-10-13 · Plan 01-16 (gemessen) · Code: `spikes/WindowHost/Variants/RenderCost.cs`, `spikes/Driver/Cases/RenderCase.cs` · Läufe: `spikes/tools/run-render-matrix.ps1` · Auswertung: `spikes/tools/render-report.ps1`

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

Gemessen am 2026-10-13 auf Build 26200 (100 % Skalierung, 60 Hz), Messlast mit der Produktmathematik (siehe Aufbau). Zwei vollständige Läufe: **erhöht mit PresentMon** (8 Zellen, `spikes/results/q4-elevated-summary.csv`) und **normal** mit Kontrolle K (10 Zellen, `spikes/results/q4-normal-summary.csv`). Gesamtauswertung mit Urteilsspalten: `spikes/results/q4-report.md`. Rohdaten (Frames je Zelle) unter `spikes/results/private/q4-raw/` (ignoriert).

**Vorversuch A3** (`spikes/results/q4-pretest.md`): PresentMon sieht das Layered-Fenster **nicht**: 0 Zeilen im Vorversuch und in allen 8 erhöhten Zellen, auch ohne Prozessfilter. Dasselbe Fenster undurchsichtig sieht PresentMon sofort (370 Präsentationen in 14 s). Die Flags sind gegen `--help` geprüft. Damit ist die D-18-Ausfallquote für das transparente Fenster mit PresentMon **nicht messbar**.

| Zelle (erhöht / normal) | Kosten p95 ms | CPU % gesamt | Kernäquiv. | dwm.exe % | Ersatz: Host-Kadenz > 25 ms (nicht D-18) | DWM verworfen/verpasst |
|---|---|---|---|---|---|---|
| R1 / 20 / hw | 0,014 / 0,017 | 0,40 / 0,43 | 0,08 | 0,36 | 5,8 % / 5,2 % | 0 / 0 |
| R2 / 20 / hw | 0,033 / 0,036 | 0,37 / 0,38 | 0,07 | 0,29 | 14,2 % / 11,3 % | 0 / 0 |
| R1 / 20 / sw | 0,018 / 0,022 | 0,31 / 0,41 | 0,06 | 0,25 | 16,3 % / 10,6 % | 0 / 0 |
| R2 / 20 / sw | 0,044 / 0,050 | 0,29 / 0,23 | 0,06 | 0,28 | 21,1 % / 18,8 % | 0 / 0 |
| R1 / 64 / hw | 0,031 / 0,038 | 0,53 / 0,60 | 0,11 | 0,42 | 3,6 % / 1,6 % | 0 / 0 |
| R2 / 64 / hw | 0,099 / 0,091 | 0,71 / 0,70 | 0,14 | 0,49 | 6,3 % / 4,2 % | 0 / 0 |
| R1 / 64 / sw | 0,077 / 0,085 | 0,86 / 0,91 | 0,17 | 0,51 | 1,5 % / 0,1 % | 0 / 0 |
| R2 / 64 / sw | 0,168 / 0,172 | 0,61 / 0,53 | 0,12 | 0,47 | 6,6 % / 8,9 % | 0 / 0 |
| K: R1 / 64 / hw, undurchsichtig | – / 0,047 | – / 0,39 | 0,08 | n/a | – / 11,8 % | 0 / 0 |
| K: R2 / 64 / hw, undurchsichtig | – / 0,080 | – / 0,35 | 0,07 | n/a | – / 16,8 % | 0 / 0 |

- **D-18 p95 (Aufbaukosten):** überall bestanden, höchstens 0,17 ms gegen 16,7 ms (rund 1 %).
- **D-19 CPU:** überall bestanden, höchstens 0,91 % gegen 15 %; `dwm.exe` zusätzlich höchstens 0,51 %.
- **Wirksamer Modus:** in jeder Zelle wie verlangt (`hardware` bzw. `software`, Tier 2).
- **Ruhe:** 0 Frames und praktisch 0 % CPU in allen 18 Zellen (Grundlage für ZOOM-06).
- **GC:** 1–3 Gen-0-Sammlungen je 30-s-Zelle, keine Gen 1/2; 10–26 KB Allokation je Frame (nur als Mittel aussagekräftig), ein Teil davon aus `ZoomEngine.Compute` (neue Arrays je Aufruf).
- **R1 gegen R2:** Mit der Produktmathematik ist R1 in jeder Zelle billiger (etwa die Hälfte der p95-Kosten); R2 muss jetzt fast jedes Symbol je Frame anfassen.
- **Ausgelassene Frames (Ersatzwert):** Die Host-Kadenz zeigt in allen Zellen Abstände von etwa 33 ms (ein ausgelassener Frame), 0,1 bis 21 %, ohne Bezug zu Symbolzahl oder Zeichenkosten, **auch im undurchsichtigen Kontrollfenster K**. Die DWM-Zähler melden überall 0 verworfene und 0 verpasste Frames; sie zählen aber nur, was der DWM selbst nicht schafft, nicht Frames, die die Anwendung gar nicht liefert. Gegenprobe im undurchsichtigen Fenster mit PresentMon (`q4-pretest.md`): PresentMon sieht dort 8,9 % Abstände über 25 ms, die Host-Kadenz desselben Laufs ~10 %. Beide Quellen sehen dieselben Lücken, gleichmäßig über die Mausbahn verteilt. Die Lücken sind also **echt** (WPF liefert in diesen Frames kein neues Bild) und **nicht durch die Transparenz verursacht**. Ursache ungeklärt.

Die Fallback-Stufe (64 Symbole, 1000×160) entfällt: 64 Symbole bestehen D-18 p95 und D-19 in jeder Variante.

### Handprüfung (D-07)

Handteil H5 am 2026-10-19 (Stephan, echte Maus, R1, Hardware, geglättete Zoom-Rechnung, je 30 s):

| Punkt | Messung | Von Hand |
|---|---|---|
| 20 Symbole | Bildschirm 10,4 % ausgelassene Takte (01-19), Zittern 0 | flüssig, äußere Symbole ruhig — bestanden |
| 64 Symbole | Aufbau p95 0,017 ms; am Bildschirm ungemessen | flüssig — bestanden |

## Entscheidung

**Entscheidung:** GO — R1, Hardware: billigste Kombination, besteht D-18 p95 (0,017 ms) und D-19 (0,43 % CPU) bei 20 und 64 Symbolen; die Ausfallquote wird am Bildschirm gemessen (01-19: 10,4 % bei R1/20/hw, RocketDock 7–12 %) mit RocketDock als Maßstab (Stephan, D-08, 2026-10-19); H5 von Hand flüssig bei 20 und 64 Symbolen.

## Folgen für Phase 2/3

- **Zeichentechnik für `IBarSurface`:** R1 (ein `OnRender` je Frame, alles neu zeichnen). Unter der echten Layoutmathematik ist R1 billiger als R2 und einfacher; R2 bringt keinen Vorteil, weil sich mit der Verankerung `cursor` jedes Symbol bewegt.
- **Render-Modus:** Hardware (Standard). `SoftwareOnly` besteht ebenfalls und bleibt Ausweichweg für Rechner ohne brauchbare GPU.
- **Fenstergröße:** Die Kosten sind bei 1900×250 so klein, dass die Fenstergröße für die Zeichenkosten keine Rolle spielt. Für 64 Symbole muss das Fenster die gezoomte Breite fassen (~2050 px bei 24 px Grundgröße, Raster 28); das ist eine Layoutfrage für Phase 2/3, keine Kostenfrage.
- **Allokation:** `ZoomEngine.Compute` erzeugt je Aufruf neue Arrays; bei 60 fps unbedenklich (keine Gen-1/2-Sammlungen), in Phase 3 bei Bedarf auf wiederverwendete Puffer umstellen.
- **Phase 3 muss noch beweisen,** dass die ausgelassenen Frames (siehe Messwerte) mit echter Maus nicht sichtbar sind oder sich beheben lassen; sonst ist „flüssig“ (Core Value) nicht erfüllt. Kandidaten zum Prüfen: Taktung von `CompositionTarget.Rendering` gegen die DWM-Periode, Hintergrundlast (VS Code, parallele Sitzungen), Energiesparzustand der GPU.

## Grenzfälle für Stephan

Keine. Entschieden am 2026-10-19 (Stephan, D-08): Die Ausfallquote wird ab hier am Bildschirm gemessen (Desktop Duplication, Plan 01-19), und der Maßstab ist RocketDock (7–12 % auf diesem Maß) statt der 1-%-Grenze von D-18, die auch RocketDock nicht erreicht.
