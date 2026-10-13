# Spike Q4b — Bildtempo und Ruhe der Leiste auf dem Bildschirm

**Stand:** 2026-10-13 · Plan 01-19 (Aufgaben 1 und 2 gemessen, Aufgabe 3 DirectComposition offen) · Code: `spikes/Driver/ScreenCapture.cs`, `spikes/Driver/Cases/ScreenCase.cs`, `spikes/Driver/Cases/RenderCase.cs` (`--capture`, `--layout`, `--snap`), `spikes/WindowHost/Variants/RenderCost.cs` · Zoom-Rechnung: `src/MehrBar.Core/Layout/ZoomEngine.cs` (`ComputeSmooth`) · Läufe: `spikes/tools/run-screen-compare.ps1` · Werte: `spikes/results/q4b-screen-summary.csv`

## Frage

1. Stephan sah im Q4-Lauf (01-16) die kleinen Symbole links und rechts des Zoombereichs hin und her zittern. Woher kommt das, und ist es behoben?
2. 01-16 konnte die D-18-Ausfallquote nicht messen, weil PresentMon das Layered-Fenster nicht sieht (A3). Wie viele Bilder fehlen **auf dem Bildschirm**, und wie steht RocketDock im Vergleich?

## Aufbau

**Messwerkzeug:** DXGI Desktop Duplication auf einem eigenen Thread im Driver. Für jedes vom DWM zusammengesetzte Bild mit neuem Inhalt (`LastPresentTime ≠ 0`) werden drei Pixelzeilen des Leistenrechtecks kopiert (Symbolmitte und ±4 px), mit QPC-Zeitstempel. Das misst, was tatsächlich auf dem Bildschirm steht, unabhängig vom Fenstertyp und auch für fremde Programme. Der Mauszeiger ist in Desktop Duplication nicht enthalten.

**Auswertung (`screen-analyze`):**

| Maß | Bedeutung |
|---|---|
| `interval_p50/p95` | Abstand zweier Bilder, in denen sich die Leistenzeile geändert hat |
| `skipped_pct` | Anteil dieser Abstände ≥ 1,5 Perioden (bei 60 Hz ≥ 25 ms) — **Ersatz für die D-18-Ausfallquote, am Bildschirm gemessen** |
| `missed_periods_pct` | Anteil der 60-Hz-Takte ohne neues Leistenbild |
| `far_*` | Bewegung von 48-px-Fenstern der Zeile, die weiter als `--far` vom Mauszeiger entfernt sind (Verschiebungssuche ±8 px mit Subpixel-Verfeinerung): `far_path_px_s` zurückgelegter Weg, `far_net_px_s` Nettoweg, `far_reversals_s` Richtungswechsel je Sekunde. Pendeln heißt: Weg ≫ Netto und viele Wendungen. |

**Lauf:** wie Q4: Maus per `SendInput` mit 125 Hz auf einer Sinusbahn (Periode 2 s), 3 s Vorlauf, 20 s Messung, `SafetySession` mit Strg+Alt+Q und Abbruch bei echter Eingabe. Vorher minimiert Win+M alle Fenster, danach werden sie wiederhergestellt. Unsere Leiste: R1 (`OnRender`), 20 Symbole, Hardware, Fenster 1900×250 bei x 10, Mausbahn ±150 px um die Mitte, `far` 345 px. RocketDock (ZoomWidth 4, IconMin 32, IconMax 121; 15 Symbole und 3 Trenner, x 696–1224): Mausbahn um x 960 ±150 (Mitte) und um x 1070 ±145 (Ende, bis an das letzte Symbol), `far` 250 px. Beide Leisten sitzen oben am Hauptbildschirm und wurden deshalb getrennt gemessen.

**Abweichungen vom Plan:** Das Werkzeug lebt im Driver (Fall `screen-sweep`/`screen-analyze` und Option `--capture`), nicht als eigenes Projekt `spikes/FrameCapture`. Die Bewegung wird über Verschiebungssuche gemessen, nicht über Symbolkanten. Gemessen wurde nur mit 20 Symbolen. Neu gegenüber dem Plan: die geglättete Zoom-Aufteilung (`ComputeSmooth`) als Prüfgröße und RocketDock als Referenz.

## Ursache des Zitterns

Die Cursor-Verankerung in `ZoomEngine.Compute` teilt den Zusatzplatz des Symbols unter der Maus nach dem Anteil f der Maus in diesem Symbol. Die Platzbedarfe aller anderen Symbole sind abgetastet (Kernel an der Ruhemitte), deshalb ist die Summe vor dem Zeiger keine glatte Funktion der Mausposition. Folge: Alle Symbole außerhalb des Zoomradius schwingen je überquertem Symbol einmal um etwa ±0,6–0,7 px hin und her, ohne Nettobewegung.

`ZoomEngine.ComputeSmooth` (Commit `da9b667`) behält die abgetasteten Symbolgrößen, also den gemessenen Abfall, und teilt nur die Summe des Zusatzplatzes nach dem Anteil des Kernel-Integrals vor dem Zeiger. Abseits der Enden ist dieser Anteil genau ½: Die äußeren Symbole stehen still. Der Preis: Der Ruhepunkt unter der Maus wandert um höchstens 0,63 px in der Mitte und um 2,50 px im Radius eines Leistenendes (Tests in `ZoomSmoothnessTests`). Die vier RocketDock-Referenzzeilen aus Q5 bestehen weiterhin innerhalb ±6 px.

## Messwerte

Gemessen am 2026-10-13, Build 26200, 100 % Skalierung, 60 Hz.

| Lauf | Bilder mit Leistenänderung | Abstand p50 / p95 (ms) | `skipped_pct` | `missed_periods_pct` | Weg / Netto äußere Symbole (px/s) | Wendungen/s |
|---|---|---|---|---|---|---|
| Host, abgetastet | 868 | 16,7 / 50,2 | 26,3 % | 27,9 % | 8,01 / 0,15 | 7,7 |
| Host, abgetastet, ganze Pixel | 807 | 16,7 / 56,2 | 28,2 % | 32,8 % | 6,51 / 0,15 | 6,3 |
| **Host, geglättet** | 894 | 16,7 / 50,1 | 22,5 % | 25,6 % | **0,02 / 0,02** | **0** |
| Host, geglättet, ganze Pixel | 867 | 16,7 / 50,2 | 24,4 % | 27,7 % | 0,01 / 0,01 | 0 |
| **RocketDock, Mitte** | 1094 | 16,7 / 33,4 | **7,2 %** | 8,8 % | 0,39 / 0,39 | **0** |
| RocketDock, Ende | 1058 | 16,7 / 33,6 | 11,5 % | 11,8 % | 0,29 / 0,29 | 0 |

Zum Vergleich die Eigenmessung des Hosts in denselben Läufen (Host-Kadenz > 25 ms): 11,0–14,8 %. Auf dem Bildschirm fehlt also etwa doppelt so oft ein Bild, wie der Host selbst bemerkt. Die Aufbaukosten blieben bei p95 ≤ 0,02 ms, die CPU des Hosts bei etwa 0,3 %.

**RocketDock am Leistenende** (Lauf „Ende“): Je Bild wurde der äußerste vom Ruhebild abweichende Pixel der Zeile gesucht und neben die Mausposition gelegt. Die Rechnung ist `ComputeSmooth` mit Radius 2,58 auf RocketDocks aktueller Ruhebelegung (Gruppen 5/6/3/1).

| Maus bei x | linker Rand RocketDock | rechter Rand RocketDock | rechter Rand Rechnung | Differenz |
|---|---|---|---|---|
| 920–1140 | 561 (steht) | 1358 (steht) | 1338–1340 | 18,4–19,8 |
| 1160 | 561 | 1351,9 | 1330,4 | 21,5 |
| 1180 | 561 | 1325,2 | 1303,8 | 21,4 |
| 1200 | 561 | 1282,3 | 1262,7 | 19,6 |

In Ruhe endet RocketDocks Kasten bei etwa 1243. Am letzten Symbol bleibt der Kasten rund 40 px weiter außen und zieht sich gegenüber der Mitte um 76 px zurück. Der linke Rand steht dabei fest. Die Rechnung folgt dem mit einem festen Versatz von etwa 19 px: Das ist der Kastenrand, den der Skin über das letzte Symbol hinaus zeichnet und den die Zoom-Rechnung nicht enthält. Der linke Rand der Rechnung steht ebenfalls (580,4–581,8). Die Streuung innerhalb eines Bereichs (15–28 px) kommt vom Nachlauf des Bildes hinter der Maus am Umkehrpunkt.

## Ergebnis

1. **Zittern: behoben.** Auf dem Bildschirm pendeln die äußeren Symbole mit der abgetasteten Rechnung 6–8 Mal je Sekunde. Mit `ComputeSmooth` stehen sie still, wie bei RocketDock. Am Leistenende verhalten sich Rechnung und RocketDock gleich. Das Runden auf ganze Pixel bringt nichts zusätzlich und kostet eher Bilder. **Empfehlung für Phase 2:** `ComputeSmooth` als Produktrechnung, keine Pixelrundung.
2. **Bildtempo: Lücke gegenüber RocketDock.** Die WPF-Leiste (transparentes Fenster, R1, Hardware) verliert auf dem Bildschirm 23–28 % der Takte, RocketDock 7–12 %. Die Lücken sind bei uns oft zwei bis drei Takte lang (p95 50 ms gegen 33 ms). Am Aufwand liegt es nicht. Die Bilder gehen zwischen WPF-Renderthread, Layered-Fenster und DWM verloren. Die Ausfallschwelle von D-18 (~1 %) erreicht übrigens auch RocketDock auf diesem Maß nicht. Der Maßstab für MehrBar ist deshalb RocketDock, nicht 1 %.
3. **Für 01-16:** Die Bildschirmmessung ersetzt den dort genannten Ersatzwert (5,2–5,8 % aus der Host-Kadenz). Gemessen auf dem Bildschirm liegt R1/20/hw bei 22,5–26,3 %.

## Offen

- **Aufgabe 3:** DirectComposition-Vergleichsleiste (Flip-Swapchain mit vormultipliziertem Alpha, Direct2D, getaktet über das Frame-Latency-Waitable), dieselbe Arbeitslast, gemessen mit demselben Werkzeug. Ziel: RocketDocks Bildtempo (um 7 %). Danach die Empfehlung für den Zeichenweg in Phase 2.
- 64 Symbole wurden auf dem Bildschirm nicht gemessen.
- Grenzfall für Stephan: Sichtprüfung der geglätteten Leiste und später der DirectComposition-Leiste.

**Nachmessen:** RocketDock schließen, dann `powershell -NoProfile -ExecutionPolicy Bypass -File spikes/tools/run-screen-compare.ps1` (fragt nach „GO“). RocketDock einzeln: Fensterrechteck auslesen, dann `Driver.exe screen-sweep --rect <x,y,w,h> --sweep-y <Symbolmitte> --rows <y-4,y,y+4> --centre <x> --amplitude 150 --clear-top --label rocketdock --out-dir spikes/results/private/q4b-raw/rocketdock` und `Driver.exe screen-analyze --dir <Ordner> --far 250 --label rocketdock --out spikes/results/q4b-screen-summary.csv`. Achtung: Ohne `--clear-top` misst der Lauf das Fenster, das über RocketDock liegt.
