# Spike Q3 — Menütechnik am nicht aktivierenden Leistenfenster

**Stand:** 2026-10-12 · Plan 01-15 (Messung abgeschlossen, Grenzfälle offen) · Code: `spikes/WindowHost/Variants/MenuWpf.cs` (M-A, M-B), `spikes/WindowHost/Variants/MenuNative.cs` (M-C, M-D), `spikes/Driver/Cases/MenuCase.cs`, Lauf: `spikes/tools/run-menu.ps1`

## Frage

Q3 (ROADMAP, Erfolgskriterium 3): Welche Menütechnik funktioniert an der nicht aktivierenden Leiste (`TOOLWINDOW|NOACTIVATE`, `MA_NOACTIVATE`, Trefferfläche H1 aus Q1) so, dass das Menü zuverlässig schließt und sich bedienen lässt — WPF-`ContextMenu` (M-A) oder nativ `TrackPopupMenu` (M-C)? Falls keine von beiden alle Pflichtpunkte schafft, gilt D-15: die Leiste darf sich **nur für die Dauer des Menüs aktivieren** (M-B) bzw. ein unsichtbares Hilfsfenster als Besitzer verwenden (M-D).

Pflichtpunkte (D-15, alle müssen für eine Variante bestehen):

| Punkt | Name im Driver | Prüfung |
|---|---|---|
| Menü öffnet an der Mausposition | `open` | Rechtsklick auf die Platte, Host meldet `menu-open` binnen 1 s |
| Schließt bei Klick auf den Desktop | `close-desktop` | Klick daneben, `menu-close` binnen 1 s |
| Schließt bei Klick auf ein anderes Fenster | `close-window` | Klick auf das Prüffenster („Zeuge“) |
| Schließt bei Klick auf eine andere Stelle der Leiste | `close-bar` | Klick auf eine andere Stelle der Platte |
| Schließt mit Escape | `close-escape` | Taste Escape |
| Pfeile und Enter wählen einen Eintrag | `keys-enter` | `VK_DOWN`, `VK_DOWN`, `VK_RETURN`; der Host meldet den gewählten Eintrag |
| Gestartetes Programm kommt nach vorn | `launch-front` | Eintrag 3 startet ein Testfenster („Launched“); es muss binnen 2 s im Vordergrund sein |
| Zweites Öffnen funktioniert | `reopen` | Menü schließen, erneut öffnen; es bleibt mindestens 500 ms offen |

Zusätzlich erfasst (nicht Pflicht, aber entscheidend für die Folgen): Mausauswahl (`mouse-select`), Z-Reihenfolge der Leiste während des Menüs (`z-during`, Annahme A5: `SetForegroundWindow` hebt die Leiste nach vorn) und Fokus nach dem Schließen (`focus-after`).

## Aufbau

**Harness:** `WindowHost` mit Variante H1 (Sieger aus Q1) ist die Leiste unter Test; ein zweites Prozessfenster, der **Zeuge**, liefert Klickziel und Vordergrund-Vorher/Nachher. Der `Driver` ist Eingabequelle und Orakel; alle Eingaben laufen in einer `SafetySession` (Notabbruch Strg+Alt+Q, Abbruch bei echter Eingabe, Rücknahmen: offenes Menü per Escape schließen, gestartete Testfenster beenden, Win+M zurücknehmen). Ein Lauf misst pro Variante alle Punkte nacheinander gegen eine frisch gestartete Leiste; zwischen den Punkten setzt der Driver eine angehobene Leiste auf `HWND_BOTTOM` zurück (Ereignis `repin`) und stellt den Zeugen nach vorn.

**Varianten (Rangfolge der Einfachheit M-A < M-C < M-B < M-D):**

| Variante | Verfahren |
|---|---|
| M-A | WPF-`ContextMenu` an der Leiste, ohne Eingriff in Vordergrund oder Fokus |
| M-C | Natives `CreatePopupMenu` + `TrackPopupMenuEx(TPM_RETURNCMD)` mit `SetForegroundWindow(Besitzer)` davor und `PostMessage(WM_NULL)` danach (Pflicht laut Microsoft Learn); Besitzer ist die Leiste |
| M-B | WPF-`ContextMenu` mit **temporärer Aktivierung**: `SetForegroundWindow(Leiste)` vor dem Öffnen, danach Fokus an das vorherige Fenster zurück (D-15) |
| M-D | Natives Menü mit **unsichtbarem Hilfsfenster** als Besitzer; die Leiste selbst wird nicht aktiviert |

**Entscheidungskriterium (D-08):** Die einfachste Variante, die jeden Pflichtpunkt besteht, gewinnt (M-A < M-C < M-B < M-D). Besteht weder M-A noch M-C, aber M-B oder M-D, bleibt es GO mit dem D-15-Rückfall; die Notiz nennt die Variante und ihre Nebenwirkungen (stieg die Leiste vor die Fenster, wurde der Fokus zurückgegeben?). Besteht keine, ist die Entscheidung NO-GO mit dem scheiternden Punkt. Ein Punkt, der erst nach einer Wiederholung oder mit einer Zeit innerhalb von 10 % seiner Grenze besteht, ist ein Grenzfall und geht an Stephan.

**Hinweis zur Auswertung:** Der Driver zählt in `MENU RESULT … mandatory=n/6` nur sechs Punkte (`close-desktop`, `close-window`, `close-bar`, `close-escape`, `keys-enter`, `launch-front`). Für die Entscheidung gelten zusätzlich `open` und `reopen` (acht Pflichtpunkte, wie im Plan); sie wurden aus `menu.csv` von Hand gegengelesen. Beide bestehen in allen vier Varianten, die Auswertung ändert sich dadurch nicht.

**Einwilligung und Messbedingungen:** Stephan antwortete am Haltepunkt von Plan 01-15 mit **„los“**. Der echte Lauf (`run-menu.ps1`, Exit 0, 62 s) lief am 2026-10-12 gegen 17:25, vom Orchestrator aus dem Hauptverzeichnis gestartet, weil der Sandbox der ausführenden Sitzung PowerShell verwehrt ist; die Dateien unter `spikes/results/q3/` sind unverändert übernommen. Bedingungen: ein Monitor, 1920×1080 (Sitzungszeile des Parallellaufs `primary=1920x1080`), Leiste am oberen Rand (Menü öffnete bei 960,36); Skalierung wie bei Q1/Q2 (100 %), für diesen Lauf nicht neu geprüft; dass RocketDock beendet war, ist aus den Dateien nicht belegt (das Skript fordert es vor dem Lauf an). Kein Abbruch durch echte Eingabe (Exit 0). Nach dem Lauf: „No WindowHost process left running“ (kein Prozess mit „Launched“ zurückgeblieben, Abnahmekriterium der Aufgabe erfüllt). Der Lauf enthält **eine Messung je Punkt und Variante** (keine Wiederholungen).

**Eigenschaft der Messleiste, wichtig für die Folgen:** Der Host pinnt die Leiste nur beim ersten Anzeigen auf `HWND_BOTTOM` (`HostWindow.cs`, „later z-order changes are left alone“). Er enthält **nicht** den S0-Unterbau aus Q2 (jede Z-Änderung in `WM_WINDOWPOSCHANGING` auf `HWND_BOTTOM` umschreiben). Was `z-during` zeigt, ist also die rohe Wirkung von `SetForegroundWindow`.

**Nachmessen:**

```
powershell -NoProfile -ExecutionPolicy Bypass -File spikes/tools/run-menu.ps1
```

Der echte Lauf sendet Eingaben und fragt vorher nach „GO“. Nur einzelne Varianten: `-Variants M-C` (schreibt `menu-m-c.csv` usw. und ersetzt das Gesamtergebnis nicht). Ein vorhandenes Ergebnis wird nur mit `-Overwrite` ersetzt. Pipeline-Prüfung ohne Eingaben: `-DryRun` (Ausgabe unter `spikes/results/dry/q3/`, nicht im Repo; erwartete Zeile `MENU DRY OK M-A`). Handprüfung je Variante (D-07): `Driver.exe menu --variant <v> --manual --seconds 120`.

## Messwerte

Quelle: `spikes/results/q3/` — `menu.csv` (eine Zeile je Variante und Punkt: Variante, Punkt, bestanden, Zeit in ms, Vordergrundklasse vorher/nachher, Z-Position der Leiste, Bemerkung; 44 Zeilen = 4 × 11), `menu.md` (Tabelle Variante × Punkt), `menu-events.csv` (Driver-Ereignisse: `pass`/`fail`, `repin`, `restart-bar`). Bildschirmfotos der Menüs liegen unter `spikes/results/private/q3-menu-*.bmp` und gehören nicht ins Repo.

**Pflichtpunkte (D-15)** — ✓ bestanden, ✗ nicht bestanden, Zeit bis zur Wirkung in ms; aus `menu.csv` übertragen:

| Variante | open | close-desktop | close-window | close-bar | close-escape | keys-enter | launch-front | reopen | Pflicht |
|---|---|---|---|---|---|---|---|---|---|
| M-A | ✓ 188 | ✗ 1001 | ✗ 1001 | ✓ 65 | ✗ 1001 | ✗ 1000 | ✓ 378 | ✓ 157 | **4/8** |
| M-C | ✓ 94 | ✓ 79 | ✓ 62 | ✓ 61 | ✓ 32 | ✓ 281 | ✓ 390 | ✓ 64 | **8/8** |
| M-B | ✓ 172 | ✓ 66 | ✓ 77 | ✓ 77 | ✓ 62 | ✓ 279 | ✓ 389 | ✓ 77 | **8/8** |
| M-D | ✓ 78 | ✓ 74 | ✓ 77 | ✓ 60 | ✓ 31 | ✓ 282 | ✓ 390 | ✓ 78 | **8/8** |

**Nicht Pflicht, aber für die Folgen entscheidend:**

| Variante | mouse-select | z-during („Leiste unten“) | focus-after („Fokus zurück“) |
|---|---|---|---|
| M-A | ✓ 91 | ✓ Fenster über der Leiste 264 → 265 → 264, nicht gehoben | ✓ Vordergrund blieb beim Zeugen (Menü wurde per Mausklick auf Eintrag 1 geschlossen, Escape kam nicht an) |
| M-C | ✓ 77 | **✗** 267 → 27 → 26, Leiste vor sichtbaren Fenstern, bleibt es nach dem Schließen | **✗** Vordergrund danach: die Leiste, nicht der Zeuge |
| M-B | ✓ 93 | **✗** 267 → 27 → 30, ebenso | ✓ Fokus an den Zeugen zurückgegeben |
| M-D | ✓ 93 | ✓ 264 → 265 → 264, nicht gehoben | **✗** Vordergrund danach: das unsichtbare Hilfsfenster (`HwndWrapper[WindowHost;;…]`), nicht der Zeuge |

Die Zählung „Fenster über der Leiste“ ist je Variante eine andere Momentaufnahme des Desktops (264 gegenüber 267 am Anfang); maßgeblich ist das Kennzeichen „Leiste vor einem sichtbaren Fenster“ (`lifted before/during/after`), das der Driver aus der Fensterreihenfolge ableitet. Der Driver zählt `MENU RESULT M-A mandatory=2/6 all=7/11`, `M-B 6/6 all=10/11`, `M-C 6/6 all=9/11`, `M-D 6/6 all=10/11`; die sechs Pflichtpunkte stimmen mit der Tabelle oben überein (M-A: `close-bar`, `launch-front`).

**Beobachtungen**

- **M-A scheitert an vier von acht Pflichtpunkten.** Das WPF-Menü an der nicht aktivierenden Leiste schließt weder bei Klick auf den Desktop noch auf das Zeugenfenster noch mit Escape, und Pfeile/Enter erreichen es nicht (die Tasten kamen nicht an: kein `menu-item`). Es schließt nur bei Klick auf die Leiste selbst (65 ms). Der Driver musste die Leiste in M-A neunmal neu starten (`restart-bar`), weil das Menü offen blieb. Mausauswahl, Programmstart und Wiederöffnen gehen, wenn man das Menü mit der Maus bedient.
- **M-B, M-C, M-D bestehen alle acht Pflichtpunkte**, jeweils mit großem Abstand zu den Grenzen: längste Zeit 390 ms (`launch-front`, Grenze 2000 ms), die Schließ- und Auswahlpunkte höchstens 282 ms gegenüber 1000 ms. Kein Punkt brauchte eine Wiederholung (`menu-events.csv` enthält in M-B/M-C/M-D weder `retry` noch `restart-bar`).
- **A5 (SetForegroundWindow auf die NOACTIVATE-Leiste hebt sie nach vorn): bestätigt.** Aktiviert das Verfahren die Leiste selbst (M-B, M-C), steigt sie während des Menüs vor die sichtbaren Fenster (Fenster darüber 267 → 27) und **bleibt nach dem Schließen oben** (30 bzw. 26); der Driver musste sie nach jedem der zehn Punkte mit Menü zurück auf `HWND_BOTTOM` setzen (10 von 10 `repin`-Ereignissen je Variante). Aktiviert nur ein Hilfsfenster (M-D) oder gar nichts (M-A), bleibt die Leiste unten. Einschränkung: gemessen mit dem rohen Host (siehe Aufbau), nicht mit dem Q2-Unterbau.
- **Fokus:** Nur M-B gibt ihn zurück (Absicht der Variante, `SetForegroundWindow(vorheriges Fenster)`). M-C lässt die Leiste im Vordergrund, M-D das Hilfsfenster. Die Option `--restore-focus true` des Drivers (Fokus nach dem Menü zurückgeben) gibt es für M-C und M-D, sie wurde in diesem Lauf **nicht** gemessen.
- **Instrument:** Kein Fehler im Instrument erkennbar; Quellcode unverändert. Die Auswertung der Pflichtpunkte aus dem Driver (6 statt 8) wurde von Hand ergänzt (siehe Aufbau). Gelesen aber nicht beurteilt: das Aussehen der Menüs (Bildschirmfotos nur privat, geht in H4).

## Entscheidung

Nach der Regel aus dem Plan (D-08/D-15): einfachste Variante in der Reihenfolge M-A < M-C < M-B < M-D, die alle acht Pflichtpunkte besteht.

- **M-A scheidet aus:** vier Pflichtpunkte verfehlt (`close-desktop`, `close-window`, `close-escape`, `keys-enter`). Das WPF-Menü ohne Aktivierung ist an dieser Leiste nicht brauchbar.
- **M-C besteht alle acht Pflichtpunkte** und ist damit die einfachste bestehende Variante. M-B und M-D bestehen ebenfalls, sind aber komplizierter und nicht nötig.
- **Nebenwirkungen von M-C (informativ, nicht entscheidend):** Die Leiste steigt während des Menüs vor die Fenster und bleibt danach oben (A5 bestätigt), und der Vordergrund bleibt nach dem Menü bei der Leiste statt beim vorherigen Fenster. Beides ist die Folge der Aktivierung, die `TrackPopupMenu` verlangt (Besitzer muss Vordergrundfenster sein). Beides lässt sich in Phase 2 beheben (siehe Folgen) und ist kein Grund, auf eine kompliziertere Variante zu wechseln.
- Kein NO-GO, kein D-15-Rückfall nötig: eine der beiden Grundvarianten (M-C) besteht.

**Entscheidung:** GO — Menütechnik M-C (natives `TrackPopupMenuEx` mit der Leiste als Besitzer, `SetForegroundWindow(Leiste)` davor, `WM_NULL` danach); M-A scheitert an 4 von 8 Pflichtpunkten, M-B und M-D bestehen ebenfalls; Nebenwirkungen von M-C: Leiste steigt vor die Fenster (A5 bestätigt), Fokus wird nicht zurückgegeben; offen für Stephan (D-08): Handprüfung H4

## Folgen für Phase 2/3

- **Phase 2, MENU-03 — Kontextmenü der Leiste:** nativ mit `CreatePopupMenu` und `TrackPopupMenuEx(TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_NONOTIFY)`, **die Leiste ist Besitzer**. Reihenfolge wie in `MenuNative.cs`: `SetForegroundWindow(Leiste)`, Menü anzeigen, danach `PostMessage(Leiste, WM_NULL)`. Ein WPF-`ContextMenu` ohne Aktivierung (M-A) wird nicht verwendet; die nicht aktivierende Leiste bekommt Tastatur und Klick-daneben-Ereignisse nur, wenn sie für die Dauer des Menüs Vordergrundfenster ist.
- **Vorheriges Vordergrundfenster merken und zurückgeben:** Vor `SetForegroundWindow(Leiste)` das aktuelle Vordergrundfenster speichern und nach dem Menü wieder aktivieren (so macht es M-B, `focus-after` ✓). Für M-C ist das mit der Option `--restore-focus true` im Host vorbereitet, aber **nicht gemessen**; im Abnahmetest von Phase 2 nachmessen.
- **Z-Reihenfolge nach dem Menü:** Der Q2-Unterbau (jede Z-Änderung der Leiste in `WM_WINDOWPOSCHANGING` auf `HWND_BOTTOM` umschreiben, siehe `01-SPIKE-zorder-wind.md`, Folgen) soll die Anhebung durch `SetForegroundWindow` verhindern; zusätzlich nach dem Schließen des Menüs die Leiste ausdrücklich auf `HWND_BOTTOM` zurücksetzen. **Das Zusammenspiel ist nicht gemessen:** in Q3 lief die Leiste ohne den Unterbau. Phase 2 muss prüfen, ob die Leiste mit Unterbau unten bleibt, ob das Menü dabei weiter schließt und ob der Wächter S2 (reagiert auf `EVENT_SYSTEM_FOREGROUND`) die Aktivierung der Leiste nicht als „Desktop gezeigt“ missdeutet.
- **Besitzerfenster-Strategie:** Besitzer ist die Leiste selbst (M-C). Ausweichen: M-D (unsichtbares Hilfsfenster als Besitzer) lässt die Leiste unten (`z-during` ✓), gibt den Fokus aber ebenfalls nicht von selbst zurück (`focus-after` ✗) und braucht dieselbe Rückgabe des Vordergrunds; M-B (WPF-`ContextMenu` mit temporärer Aktivierung) besteht auch, hebt die Leiste aber ebenso wie M-C. Sie sind Rückfall, falls sich M-C mit dem Q2-Unterbau als unbrauchbar erweist.
- **`WM_NULL` und `EndMenu`:** `WM_NULL` nach `TrackPopupMenuEx` ist in der Messung enthalten (M-C, M-D). `EndMenu` zum Schließen von außen (Leiste wird versteckt, Anzeigewechsel, Explorer-Neustart während ein Menü offen ist) ist **nicht gemessen**; für Phase 2 als Fall im Abnahmetest vormerken.
- **Phase 5 — volle Menüs:** Die native Technik trägt auch Untermenüs; das Aussehen ist das des Systems, eigene Gestaltung bräuchte Besitzerzeichnung. Ob das native Aussehen reicht, entscheidet Stephan in H4 (Optik nicht beurteilt).
- **Phase 3:** nichts Neues aus Q3.

## Grenzfälle für Stephan

Nach der Regel „Wiederholung oder innerhalb von 10 % der Grenze“ (D-08) ist **kein** Messwert von M-C ein Grenzfall: keine Wiederholung nötig, die längste Zeit liegt bei 390 ms gegenüber 2000 ms (19,5 % der Grenze), alle anderen Zeiten bei höchstens 282 ms gegenüber 1000 ms. Zu entscheiden bzw. von Hand zu prüfen:

1. **H4 — Handteil Menü (D-06, D-07).** Je Variante ein Durchgang mit echter Tastatur und Maus: `Driver.exe menu --variant M-C --manual --seconds 120`, danach nach Belieben `--variant M-B` und `M-D` (Rückfälle) und zur Gegenprobe `M-A`. Prüfen: schließt das Menü bei Klick daneben und mit Escape, wählen Pfeile und Enter, kommt das gestartete Programm **sichtbar** nach vorn, öffnet das Menü ein zweites Mal. Dazu die Optik der Menüs nach `spikes/results/private/q3-menu-*.bmp`. Weicht ein Handergebnis ab, gilt das Handergebnis (D-07) und der Fall wandert hierher.
2. **Leiste bleibt nach dem Menü oben (M-C, A5).** Von Hand in H4 beachten: Bleibt die Leiste nach dem Schließen vor den Fenstern, und stört es? Mit dem Q2-Unterbau soll das nicht mehr auftreten (ungemessen). Ist es nicht hinnehmbar und lässt sich nicht beheben, wäre M-D der Ausweichweg (Leiste unten, Fokus ebenso zu behandeln).
3. **Fokus nach dem Menü (M-C, M-D).** Beide geben den Vordergrund nicht zurück. Die Empfehlung (Phase 2 gibt ihn zurück wie M-B) ist nicht mit M-C gemessen. In H4 beachten: Ist nach dem Schließen des Menüs wieder das Fenster aktiv, in dem man vorher gearbeitet hat? Wer es vorab messen will: `Driver.exe menu --variant M-C --restore-focus true` in einem neuen freigegebenen Fenster.
4. **Einmal gemessen.** Jeder Punkt hat in diesem Lauf einen Wert je Variante; die Abstände zu den Grenzen sind groß (siehe oben), aber ein Streuungsbild fehlt. Wenn H4 ein Handergebnis liefert, das von der Messung abweicht, ist das ein Grund, `run-menu.ps1 -Variants M-C` zu wiederholen.
5. **Messbedingungen:** ein Monitor, Leiste am oberen Rand, Skalierung 100 % (nicht neu geprüft), RocketDock-Zustand nicht belegt; 125/150 % und die zweite Leiste kommen mit Phase 2.
