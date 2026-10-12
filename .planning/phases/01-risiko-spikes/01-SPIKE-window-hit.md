# Spike Q1 — Trefferfläche, Klick-Durchlass, erstes Bild, Fokus

**Stand:** 2026-10-12 · Plan 01-13 (Gerüst; Messung nach dem Haltepunkt) · Code: `spikes/WindowHost/` (Varianten H1–H3), `spikes/Driver/Cases/` (`OracleCases.cs`, `Q1Cases.cs`), Lauf: `spikes/tools/run-q1.ps1`

## Frage

Q1 (ROADMAP, Erfolgskriterium 1): Fängt ein Wegwerf-Fenster (ca. 1900×250 px, transparent, Trefferfläche mit Alpha 1) Klicks und Mausbewegung über der Leistenfläche, lässt es Klicks 1 px daneben zum Desktop durch, erscheint es ohne Aufblitzen ganz unten (`HWND_BOTTOM`, `TOOLWINDOW|NOACTIVATE`) und nimmt es einem aktiven Notepad beim Klick nicht den Fokus? Welches Verfahren für die Trefferfläche gewinnt (D-14)?

Zwei Vorbedingungen werden im selben Lauf geprüft, weil Q2 und Q3 von ihnen abhängen:

- **Orakel (Annahme A1):** Liefert `GetPixel` auf dem Bildschirm-DC das zusammengesetzte DWM-Bild? Prüfung in beide Richtungen: Leiste unverdeckt → Sentinel sichtbar, Prüffenster darüber → nicht sichtbar. Schlägt das fehl, ist die Pixelmessung unbrauchbar und die Fenster-Spikes sind UNKLAR statt vertraut.
- **Basislinie (D-12):** Ist die Leiste unter `HWND_BOTTOM` auf Build 26200 über dem Desktop überhaupt sichtbar? Wenn nicht, gibt es keinen Ausweichweg über eine andere Ebene, sondern einen zweiten, gezielten Spike.

## Aufbau

**Harness:** `WindowHost` ist die Leiste unter Test (WPF, transparent, 1900×250). Der `Driver` ist Eingabequelle und unabhängiges Orakel; er schreibt je Schritt eine CSV nach `spikes/results/q1/`. Alle Eingaben laufen in einer `SafetySession` (Notabbruch Strg+Alt+Q, Abbruch bei echter Eingabe, Win+M wird im `finally` rückgängig gemacht).

**Varianten (D-14, das beste Verfahren gewinnt):**

| Variante | Verfahren |
|---|---|
| H1 | Platte mit Alpha 1 im Fenster (Hit-Test auf Pixelalpha) |
| H2 | Hit-Region per `SetWindowRgn` auf die Platte; zusätzlich 5 s Region-Churn zur Kostenmessung |
| H3 | `WS_EX_TRANSPARENT`, per 16-ms-Abfrage der Zeigerposition umgeschaltet; zusätzlich Eintrittslatenz |

**Klickplan (`hit`, Seed 4711):** 100 Klicks innen (gleichverteilt auf der Platte, 2 px vom Rand) und 100 Klicks außen, gleichmäßig auf fünf Kategorien verteilt — 1 px links, rechts, oberhalb, unterhalb der Platte und im Überhang-Spalt (der alpha-0-Bereich, in dem später das vergrößerte Symbol steht). Innen und außen wechseln sich ab; zwei Klicks liegen immer mindestens Doppelklickzeit + 100 ms auseinander (kein Doppelklick möglich). Zugeordnet wird über die Host-Zeilen (`lbuttondown` an der Klickposition); „durchgelassen“ = keine Host-Zeile, als Zusatzbeleg zählt der Vordergrundwechsel weg vom Prüffenster. `WindowFromPoint` wird nur als Zusatzdatum erfasst (Annahme A2).

**Weitere Schritte:** Mausbewegung (Sweep innen/außen, Zählung der `WM_MOUSEMOVE`), Fokus bleibt auf aktivem Prüffenster (100 Zyklen aus Linksklick, Rechtsklick und Bewegung) und auf Notepad mit leerer Datei (20 Zyklen; Notepad bekommt nie eine Taste und wird nie geschlossen), erstes Bild bereits ganz unten (10 Starts, Host-Zeile `first-frame … bottom=true`; die Sicht von außen als Zusatz).

**Entscheidungskriterium (D-08):** H1 gewinnt, wenn Fangquote innen ≥ 99 %, außen 0 Host-Klicks in jeder 1-px-Kategorie und im Überhang-Spalt, Mausbewegung innen erkannt und außen nicht, Fokus 100/100 (Prüffenster) und 20/20 (Notepad), erstes Bild unten 10/10. Sonst die nächste Variante in der Rangfolge Korrektheit > keine Kosten je Frame (H2 Region-Churn, H3 Abfrage-Latenz) > Einfachheit. Werte innerhalb von 1 % bzw. 1 Stichprobe einer Schwelle sind Grenzfälle und gehen an Stephan.

**Nachmessen:**

```
powershell -NoProfile -ExecutionPolicy Bypass -File spikes/tools/run-q1.ps1
```

Der echte Lauf sendet Eingaben (Klicks, Win+M, Notepad-Fenster) und fragt vorher nach „GO“. Pipeline-Prüfung ohne Eingaben: `-DryRun` (Ausgabe unter `spikes/results/dry/q1/`, nicht im Repo). Einzelschritte: `Driver.exe selftest | firstframe | hit | focus` mit `--out` und `--summary-md`.

## Messwerte

Ausstehend — die Messung läuft erst nach der Bestätigung des Handfrei-Fensters. Die Tabellen werden aus `spikes/results/q1/summary.md` übernommen; die Einzelzeilen liegen in `spikes/results/q1/selftest.csv`, `firstframe.csv`, `hit.csv`, `focus-witness.csv` und `focus-notepad.csv` (geschwärzt: keine fremden Fenstertitel, keine Pfade).

## Entscheidung

**Entscheidung:** UNKLAR — Messung nach Haltepunkt ausstehend

## Folgen für Phase 2/3

Ausstehend (Trefferverfahren für das `BarWindow`, Reihenfolge der Fenstererzeugung, ob `MA_NOACTIVATE` trägt, was der Zoom-Überhang aus Phase 3 von der Trefferfläche braucht).

## Grenzfälle für Stephan

Ausstehend. Der Handteil (D-06, D-07) bleibt in jedem Fall:

1. **H1 — Aufblitzen:** zehn echte Starts der gewählten Variante beobachten, ob die Leiste am oberen Rand kurz aufblitzt (`Driver.exe firstframe --launches 10 --manual`).
2. **H1 — echte Maus:** einmal von Hand gegenprüfen mit `Driver.exe hit --variants <gewählt> --manual --seconds 60`.
3. **H1 — Notabbruch:** den Abbruchschalter Strg+Alt+Q einmal während eines Laufs auslösen und prüfen, dass die Fenster zurückkommen (D-07).
