# Spike Q1 — Trefferfläche, Klick-Durchlass, erstes Bild, Fokus

**Stand:** 2026-10-12 · Plan 01-13 (gemessen und entschieden) · Code: `spikes/WindowHost/` (Varianten H1–H3), `spikes/Driver/Cases/` (`OracleCases.cs`, `Q1Cases.cs`), Lauf: `spikes/tools/run-q1.ps1`

## Frage

Q1 (ROADMAP, Erfolgskriterium 1): Fängt ein Wegwerf-Fenster (ca. 1900×250 px, transparent, Trefferfläche mit Alpha 1) Klicks und Mausbewegung über der Leistenfläche, lässt es Klicks 1 px daneben zum Desktop durch, erscheint es ohne Aufblitzen ganz unten (`HWND_BOTTOM`, `TOOLWINDOW|NOACTIVATE`) und nimmt es einem aktiven Notepad beim Klick nicht den Fokus? Welches Verfahren für die Trefferfläche gewinnt (D-14)?

Zwei Vorbedingungen werden im selben Lauf geprüft, weil Q2 und Q3 von ihnen abhängen:

- **Orakel (Annahme A1):** Liefert `GetPixel` auf dem Bildschirm-DC das zusammengesetzte DWM-Bild? Prüfung in beide Richtungen: Leiste unverdeckt → Sentinel sichtbar, Prüffenster darüber → nicht sichtbar. Schlägt das fehl, ist die Pixelmessung unbrauchbar und die Fenster-Spikes sind UNKLAR statt vertraut.
- **Basislinie (D-12):** Ist die Leiste unter `HWND_BOTTOM` auf Build 26200 über dem Desktop überhaupt sichtbar? Wenn nicht, gibt es keinen Ausweichweg über eine andere Ebene, sondern einen zweiten, gezielten Spike.

## Aufbau

**Harness:** `WindowHost` ist die Leiste unter Test (WPF, transparent, 1900×250; Platte 600×56 px bei x=650, y=8). Der `Driver` ist Eingabequelle und unabhängiges Orakel; er schreibt je Schritt eine CSV nach `spikes/results/q1/`. Alle Eingaben laufen in einer `SafetySession` (Notabbruch Strg+Alt+Q, Abbruch bei echter Eingabe, Win+M wird im `finally` rückgängig gemacht).

**Varianten (D-14, das beste Verfahren gewinnt):**

| Variante | Verfahren |
|---|---|
| H1 | Platte mit Alpha 1 im Fenster (Hit-Test auf Pixelalpha) |
| H2 | Hit-Region per `SetWindowRgn` auf die Platte; zusätzlich 5 s Region-Churn zur Kostenmessung |
| H3 | `WS_EX_TRANSPARENT`, per 16-ms-Abfrage der Zeigerposition umgeschaltet; zusätzlich Eintrittslatenz |

**Klickplan (`hit`, Seed 4711):** 100 Klicks innen (gleichverteilt auf der Platte, 2 px vom Rand) und 100 Klicks außen, gleichmäßig auf fünf Kategorien verteilt — 1 px links, rechts, oberhalb, unterhalb der Platte und im Überhang-Spalt (der alpha-0-Bereich, in dem später das vergrößerte Symbol steht). Innen und außen wechseln sich ab; zwei Klicks liegen immer mindestens Doppelklickzeit + 100 ms auseinander (kein Doppelklick möglich). Zugeordnet wird über die Host-Zeilen (`lbuttondown` an der Klickposition); „durchgelassen“ = keine Host-Zeile, als Zusatzbeleg zählt der Vordergrundwechsel weg vom Prüffenster. `WindowFromPoint` wird nur als Zusatzdatum erfasst (Annahme A2).

**Weitere Schritte:** Mausbewegung (Sweep innen/außen, Zählung der `WM_MOUSEMOVE`), Fokus bleibt auf aktivem Prüffenster (100 Zyklen aus Linksklick, Rechtsklick und Bewegung, also 300 Einzelprüfungen je Variante) und auf Notepad mit leerer Datei (20 Zyklen, 60 Einzelprüfungen; Notepad bekommt nie eine Taste und wird nie geschlossen), erstes Bild bereits ganz unten (10 Starts, Host-Zeile `first-frame … bottom=true`; die Sicht von außen als Zusatz).

**Entscheidungskriterium (D-08):** H1 gewinnt, wenn Fangquote innen ≥ 99 %, außen 0 Host-Klicks in jeder 1-px-Kategorie und im Überhang-Spalt, Mausbewegung innen erkannt und außen nicht, Fokus 100 % (Prüffenster und Notepad), erstes Bild unten 10/10. Sonst die nächste Variante in der Rangfolge Korrektheit > keine Kosten je Frame (H2 Region-Churn, H3 Abfrage-Latenz) > Einfachheit. Werte innerhalb von 1 % bzw. 1 Stichprobe einer Schwelle sind Grenzfälle und gehen an Stephan.

**Nachmessen:**

```
powershell -NoProfile -ExecutionPolicy Bypass -File spikes/tools/run-q1.ps1
```

Der echte Lauf sendet Eingaben (Klicks, Win+M, Notepad-Fenster) und fragt vorher nach „GO“. Pipeline-Prüfung ohne Eingaben: `-DryRun` (Ausgabe unter `spikes/results/dry/q1/`, nicht im Repo). Einzelschritte: `Driver.exe selftest | firstframe | hit | focus` mit `--out` und `--summary-md`.

**Lauf-Historie (der dritte Lauf ist das amtliche Ergebnis):**

| Lauf | Ergebnis | Ursache |
|---|---|---|
| 1 | Selbsttest fehlgeschlagen, sichtbar 0/5 (gelesen `#FF16FF` statt Sentinel `#FF00FF`); `hit` danach durch echte Eingabe abgebrochen | Stephans laufende RocketDock-Leiste am oberen Rand färbte die Prüfpixel ein; zusätzlich hat Stephan die Maus berührt |
| 2 | RocketDock beendet: Selbsttest 5/5 und 5/5 bestanden; `hit` nach 97 Klicks durch echte Eingabe abgebrochen | Stephan bestätigt, den Rechner berührt zu haben (gewollter Schutz, kein Instrumentfehler) |
| 3 | vollständig, alle Schritte Exit 0 | amtliche Ergebnisse in `spikes/results/q1/` |

Lehre für alle Pixel-Orakel-Läufe: andere Leisten und Werkzeuge am oberen Bildschirmrand (RocketDock) müssen vorher beendet sein. Das gilt auch für Q2, Q3 und die D-17-Läufe. Am Instrumentcode (`HitRegion.cs`, `Q1Cases.cs`) wurde nichts geändert.

## Messwerte

Quelle: `spikes/results/q1/summary.md` (Gesamtauswertung, Seed 4711, Lauf 3 vom 2026-10-12) und die Einzelzeilen in `spikes/results/q1/selftest.csv`, `selftest.zorder-covered.csv`, `selftest.zorder-uncovered.csv`, `firstframe.csv`, `hit.csv`, `focus-witness.csv` und `focus-notepad.csv`. Die Dateien sind geschwärzt: keine fremden Fenstertitel, keine Pfade; Fenster fremder Programme erscheinen nur als Klasse `other` ohne Prozessname, eigene Fenster als `HwndWrapper[WindowHost…]`, der Desktop als `Progman`.

### Orakel und Basislinie (selftest)

| Messung | Wert |
|---|---|
| Leiste unverdeckt, Sentinel `#FF00FF` sichtbar (D-12-Basislinie, a) | 5/5 |
| Prüffenster darüber, Deckfarbe `#00FFFF` gelesen, Sentinel nicht mehr sichtbar (Orakel A1, b) | 5/5 |

### Erstes Bild ganz unten (firstframe, 10 Starts je Variante)

| Variante | Start unten (Host) | von außen unten bei READY (Zusatz, Annahme A6) |
|---|---|---|
| H1 | 10/10 | 10/10 |
| H2 | 10/10 | 10/10 |
| H3 | 10/10 | 10/10 |

### Trefferfläche und Durchlass (hit, 100 Klicks innen, 100 außen)

| Variante | innen gefangen | außen durchgelassen | Bewegung erkannt | Fokus gehalten | Bemerkung |
|---|---|---|---|---|---|
| H1 | 100/100 | 100/100 | innen 24, außen 0 | 100/100 | Durchlass durch Vordergrundwechsel bestätigt 100/100 |
| H2 | 100/100 | 100/100 | innen 24, außen 0 | 100/100 | Region `SIMPLEREGION` 650,8–1250,64 = Platte; Region-Churn 5 s: 158 Aufrufe, Median 706 µs, p95 2100 µs, p99 2695 µs, max 2946 µs |
| H3 | **95/100** | 100/100 | innen **22**, außen 0 | **95/100** | Eintrittslatenz 20/20, Median 17,1 ms, p95 33,9 ms, max 34,3 ms, verpasst 0 |

Außen je Kategorie, durchgelassen / geklickt, aus `hit.csv` nachgezählt (Host-Zeile `arrived=False` = kein Klick im Host):

| Variante | links | rechts | oben | unten | Überhang-Spalt |
|---|---|---|---|---|---|
| H1 | 20/20 | 20/20 | 20/20 | 20/20 | 20/20 |
| H2 | 20/20 | 20/20 | 20/20 | 20/20 | 20/20 |
| H3 | 20/20 | 20/20 | 20/20 | 20/20 | 20/20 |

Alle Kategorien: 0 Host-Klicks außen. `WindowFromPoint` (nur Zusatzdatum, A2): außen liefert es durchgehend `Progman`, innen `-` (nicht erfasst).

H3 hat fünf Klicks innen verloren (Folge 33, 57, 71, 99, 171): der Klick ging an den Desktop (`fg-after=Progman`). Vermutete Ursache, nicht gesondert nachgewiesen: Die 16-ms-Abfrage hatte `WS_EX_TRANSPARENT` noch nicht zurückgenommen, als der Zeiger auf die Platte sprang.

### Fokus (focus-witness: aktives Prüffenster, 100 Zyklen = 300 Prüfungen; focus-notepad: Notepad, 20 Zyklen = 60 Prüfungen)

| Variante | Prüffenster aktiv | Notepad aktiv |
|---|---|---|
| H1 | 300/300 | 60/60 |
| H2 | 300/300 | 60/60 |
| H3 | 300/300 | 60/60 |

(H3 hat im Schritt `hit` bei 95/100 den Fokus nicht gehalten, weil dort die fünf Klicks am Host vorbeigingen; die Fokus-Schritte selbst bestehen in allen drei Varianten.)

### Handprüfung (D-07)

Handteil H1 am 2026-10-19 (Stephan, `spikes/tools/handteil.ps1`, Ergebnis `spikes/results/handteil/summary.csv`):

| Punkt | Messung | Von Hand |
|---|---|---|
| Kein Aufblitzen (10 Starts, Fenster über dem oberen Rand) | erstes Bild unten 10/10 | nichts gesehen — bestanden |
| Echte Maus auf und neben der Platte | innen 100/100, außen 100/100 | bestanden; 93 Linksklicks innen gefangen, Vordergrund blieb jedes Mal beim Desktop |
| Notabbruch Strg+Alt+Q | nie von Hand ausgelöst | bestanden (Exit 3, Fenster zurück) |

Der erste Versuch des Notabbruchs fand einen Fehler im Messwerkzeug: Im manuellen z-order-Fenster wirkte die Taste erst nach dessen Ende (Lauf endete mit Exit 0). Behoben in `c9352ff`, danach bestanden. Für die Handprüfung zeigt der Host die sonst unsichtbare Platte mit Rahmen und Quadraten (`--plate-outline on`, Trefferfläche unverändert).

## Entscheidung

Auswertung in der Reihenfolge Orakel → Basislinie → Kriterium:

1. **Orakel (A1) hält:** 5/5 Sentinel sichtbar, 5/5 vom Prüffenster verdeckt. Pixelmessungen sind für Q2 und Q3 verwendbar — mit der Auflage aus der Lauf-Historie (keine fremden Leisten am Rand).
2. **Basislinie (D-12) hält:** die Leiste ist unter `HWND_BOTTOM` über dem Desktop sichtbar, 5/5. Kein zweiter Spike nötig.
3. **Kriterium (D-08/D-14):** H1 trifft jede Schwelle eindeutig: innen 100/100 (≥ 99 %), außen 0 Host-Klicks in allen fünf Kategorien, Bewegung innen 24 / außen 0, Fokus 300/300 und 60/60, erstes Bild unten 10/10. H2 erfüllt dasselbe, ist aber komplexer (Region muss mit dem Layout nachgeführt werden, Region-Aufrufe kosten bis 3 ms). H3 scheitert: innen 95/100 und 22 statt 24 Bewegungen. H1 ist die einfachste Variante und gewinnt.

Kein Wert liegt auf der falschen Seite einer Schwelle; die Stichprobe ist klein (20 Klicks je Außenkategorie), aber alle Außenwerte sind 0 bei 100 von 100, nicht knapp.

**Entscheidung:** GO — H1, Alpha-1-Platte im Fenster als Trefferfläche für das `BarWindow`; `SetWindowRgn` (H2) bleibt als gemessener Rückfall, `WS_EX_TRANSPARENT`-Umschaltung (H3) ist verworfen (95/100 innen).

## Folgen für Phase 2/3

**Phase 2 — `BarWindow`:**

- **Trefferverfahren:** die Leiste zeichnet unter den Symbolen eine Platte mit Alpha 1 (praktisch unsichtbar); nur dort, und wo Symbolpixel Alpha > 0 haben, fängt das Fenster Klicks und `WM_MOUSEMOVE`. Alles andere, auch 1 px daneben, geht zum Desktop (gemessen 100/100). Kein `SetWindowRgn`, kein Umschalten von `WS_EX_TRANSPARENT`, kein Timer.
- **Reihenfolge der Fenstererzeugung:** Erweiterte Stile `TOOLWINDOW|NOACTIVATE` und `SetWindowPos(HWND_BOTTOM, …, SWP_NOACTIVATE | SWP_SHOWWINDOW)` in `OnSourceInitialized`, bevor das erste Bild gezeichnet wird; das eigene Anzeigen übernimmt das Show von WPF nicht. Der Host hängt zusätzlich einen `WM_WINDOWPOSCHANGING`-Hook ein, der den Z-Wert des ersten WPF-Show bis zum ersten gezeichneten Bild auf `HWND_BOTTOM` umschreibt. Laut Kommentar im Host hatte das reine WPF-Show die Leiste auf Build 26200 über die anderen Fenster gehoben (Z-Index 21, Chrome darunter); diese Absicherung ist der Grund, warum das erste Bild 10/10 unten steht. Der Pfad ohne Absicherung (`--show-mode wpf`) wurde in diesem Lauf nicht neu gemessen.
- **`MA_NOACTIVATE`:** getragen. Die `WM_MOUSEACTIVATE`-Antwort `MA_NOACTIVATE` zusammen mit `WS_EX_NOACTIVATE` hält den Fokus 300/300 auf dem aktiven Prüffenster und 60/60 auf Notepad (Links-, Rechtsklick, Bewegung). Beides übernehmen.
- **Q2/Q3-Voraussetzung:** Das Pixelorakel ist gültig (A1), solange nichts anderes am oberen Rand Pixel färbt.

**Phase 3 — Zoom-Überhang:** Der alpha-0-Bereich um die Platte lässt Klicks durch (Überhang-Spalt 20/20). Wächst der Zoom über die Platte hinaus, muss die Trefferfläche mitwachsen, sonst fällt der Zeiger im Überhang aus dem Fenster, Hover und Zoom brechen ab, und Klicks auf die vergrößerten Symbole gehen an den Desktop. Das ist aus der Messung abgeleitet, nicht gemessen: Phase 3 braucht entweder eine mit dem Zoom mitwachsende Alpha-1-Fläche oder die Hover-Fläche inklusive Überhang als Platte. Eine kleine Prüfung dafür gehört in die Abnahme von Zoom-02. Falls sich das über Alpha nicht sauber lösen lässt, ist H2 (Region pro Layoutwechsel, Median 0,7 ms) der gemessene Rückfall.

## Grenzfälle für Stephan

Keine. Entschieden am 2026-10-19: Die Handprüfung H1 ist bestanden. Die Messbedingungen (ein Monitor, 100 %, Leiste oben) bleiben als offener Punkt für Phase 2 (125/150 %, rechte Leiste); vor Pixel-Orakel-Läufen RocketDock beenden.
