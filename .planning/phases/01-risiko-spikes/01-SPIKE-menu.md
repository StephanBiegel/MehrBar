# Spike Q3 — Menütechnik am nicht aktivierenden Leistenfenster

**Stand:** 2026-10-12 · Plan 01-15 (Gerüst; Messung nach dem Haltepunkt ausstehend) · Code: `spikes/WindowHost/Variants/MenuWpf.cs` (M-A, M-B), `spikes/WindowHost/Variants/MenuNative.cs` (M-C, M-D), `spikes/Driver/Cases/MenuCase.cs`, Lauf: `spikes/tools/run-menu.ps1`

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

**Harness:** `WindowHost` mit Variante H1 (Sieger aus Q1) ist die Leiste unter Test; ein zweites Prozessfenster, der **Zeuge**, liefert Klickziel und Vordergrund-Vorher/Nachher. Der `Driver` ist Eingabequelle und Orakel; alle Eingaben laufen in einer `SafetySession` (Notabbruch Strg+Alt+Q, Abbruch bei echter Eingabe, Rücknahmen: offenes Menü per Escape schließen, gestartete Testfenster beenden, Win+M zurücknehmen). Ein Lauf misst pro Variante alle Punkte nacheinander gegen eine frisch gestartete Leiste.

**Varianten (Rangfolge der Einfachheit M-A < M-C < M-B < M-D):**

| Variante | Verfahren |
|---|---|
| M-A | WPF-`ContextMenu` an der Leiste, ohne Eingriff in Vordergrund oder Fokus |
| M-C | Natives `CreatePopupMenu` + `TrackPopupMenuEx(TPM_RETURNCMD)` mit `SetForegroundWindow(Besitzer)` davor und `PostMessage(WM_NULL)` danach (Pflicht laut Microsoft Learn) |
| M-B | WPF-`ContextMenu` mit **temporärer Aktivierung**: `SetForegroundWindow(Leiste)` vor dem Öffnen, danach Fokus an das vorherige Fenster zurück (D-15) |
| M-D | Natives Menü mit **unsichtbarem Hilfsfenster** als Besitzer; die Leiste selbst wird nicht aktiviert |

**Entscheidungskriterium (D-08):** Die einfachste Variante, die jeden Pflichtpunkt besteht, gewinnt (M-A < M-C < M-B < M-D). Besteht weder M-A noch M-C, aber M-B oder M-D, bleibt es GO mit dem D-15-Rückfall; die Notiz nennt die Variante und ihre Nebenwirkungen (stieg die Leiste vor die Fenster, wurde der Fokus zurückgegeben?). Besteht keine, ist die Entscheidung NO-GO mit dem scheiternden Punkt. Ein Punkt, der erst nach einer Wiederholung oder mit einer Zeit innerhalb von 10 % seiner Grenze besteht, ist ein Grenzfall und geht an Stephan.

**Hinweis zur Auswertung:** Der Driver zählt in `MENU RESULT … mandatory=n/6` nur sechs Punkte (`close-desktop`, `close-window`, `close-bar`, `close-escape`, `keys-enter`, `launch-front`). Für die Entscheidung gelten zusätzlich `open` und `reopen` (acht Pflichtpunkte, wie im Plan); sie werden aus `menu.csv` von Hand gegengelesen.

**Einwilligung und Messbedingungen:** Ausstehend (Haltepunkt von Plan 01-15). Die Messung braucht das freigegebene Fenster: RocketDock und andere Werkzeuge am oberen Rand beendet, ca. 8 Minuten ohne Eingabe. Bedingungen werden nach dem Lauf eingetragen (ein Monitor, Skalierung, Leiste am oberen Rand).

**Nachmessen:**

```
powershell -NoProfile -ExecutionPolicy Bypass -File spikes/tools/run-menu.ps1
```

Der echte Lauf sendet Eingaben und fragt vorher nach „GO“. Nur einzelne Varianten: `-Variants M-B` (schreibt `menu-m-b.csv` usw. und ersetzt das Gesamtergebnis nicht). Ein vorhandenes Ergebnis wird nur mit `-Overwrite` ersetzt. Pipeline-Prüfung ohne Eingaben: `-DryRun` (Ausgabe unter `spikes/results/dry/q3/`, nicht im Repo; erwartete Zeile `MENU DRY OK M-A`). Handprüfung je Variante (D-07): `Driver.exe menu --variant <v> --manual --seconds 120`.

## Messwerte

Quelle: `spikes/results/q3/` — `menu.csv` (eine Zeile je Variante und Punkt: Variante, Punkt, bestanden, Zeit in ms, Vordergrundklasse vorher/nachher, Z-Position der Leiste, Bemerkung), `menu.md` (Tabelle Variante × Punkt), `menu-events.csv` (Host-Ereignisse). Bildschirmfotos der Menüs liegen unter `spikes/results/private/q3-menu-*.bmp` und gehören nicht ins Repo.

Noch nicht gemessen. Dieser Abschnitt wird nach dem Lauf mit der Tabelle aus `menu.md` und der Auswertung von `z-during` (Annahme A5) und `focus-after` gefüllt.

## Entscheidung

Ausstehend. Der Ablauf der Auswertung steht fest: Pflichtpunkte je Variante aus `menu.csv`, dann die einfachste bestehende Variante in der Reihenfolge M-A < M-C < M-B < M-D.

**Entscheidung:** UNKLAR — Messung nach Haltepunkt ausstehend

## Folgen für Phase 2/3

Noch offen; nach der Messung festzuhalten: Menütechnik für MENU-03 in Phase 2 und für die vollen Menüs der Phase 5, Strategie für das Besitzerfenster, Behandlung von `WM_NULL`/`EndMenu`, und ob die Leiste während des Menüs vor die Fenster steigt (Annahme A5, Zusammenspiel mit dem Wächter S2 aus Q2).

## Grenzfälle für Stephan

Noch offen. Sicher bleibt der Handteil (D-06, D-07): H4 — je Variante ein Durchgang mit echter Tastatur und Maus (`Driver.exe menu --variant <v> --manual --seconds 120`), das gestartete Programm sichtbar vorn, und die Optik der Menüs nach `spikes/results/private/q3-menu-*.bmp`.
