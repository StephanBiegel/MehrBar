# Handteil Phase 1 — Checkliste H1–H7 und Grenzfälle

**Stand:** 2026-10-19 · Plan 01-17 · Ablauf: `spikes/tools/handteil.ps1` (Übersicht: `-List`, einzelner Schritt: `-Step H4`) · Entscheidungen und Grenzfälle: `spikes/tools/collect-decisions.ps1`

**Start:** auf dem Desktop in einem eigenen PowerShell-Fenster im Repo, nicht aus einer Hivemo-Sitzung (die läuft in Windows-Sitzung 0 ohne Bildschirm und Notbremse). RocketDock vorher beenden.

```
powershell -NoProfile -ExecutionPolicy Bypass -File spikes/tools/handteil.ps1
```

Antworten: j, n oder s (überspringen) plus Bemerkung. Freitext und Netzwerkpfad landen nur in `spikes/results/private/handteil-answers.csv`; committet wird `spikes/results/handteil/summary.csv` (Schritt, Punkt, Ergebnis). Weicht ein Handergebnis von der Messung ab, gilt das Handergebnis (D-07).

## Checkliste

| Schritt | Punkt | Automatisches Ergebnis (Notiz) | Stephan |
|---|---|---|---|
| H1 | Kein Aufblitzen beim Start (H1) | erstes Bild unten 10/10 (window-hit) | |
| H1 | Echte Klicks auf/neben die Leiste | innen 100/100, außen 100/100 durchgereicht (window-hit) | |
| H1 | Notabbruch Strg+Alt+Q, Fenster kommen zurück | in jedem Lauf vorhanden, nie von Hand ausgelöst | |
| H2 | Leiste nach Entsperren sichtbar und unten (S2) | nicht automatisierbar (sicherer Desktop) | |
| H3 | Win+D | S2: 20/20, größte Lücke 236 ms (zorder-wind) | |
| H3 | Ecke „Desktop anzeigen“ | S2 besteht D-11, Lücke bis 271 ms (zorder-wind) | |
| H3 | Win+M / Win+Umschalt+M | S2 besteht (zorder-wind) | |
| H3 | Aero Peek per Taste (Win+,) | Peek hing ≥ 4 s nach gesendetem Loslassen, alle Strategien (Grenzfall 2) | |
| H3 | Aero Peek per Ecke | Auslöser ohne Wirkung (Grenzfall 3) | |
| H3 | Alt+Tab | S2 besteht (zorder-wind) | |
| H4 | M-C: schließen, Pfeile/Enter, Vordergrund, zweites Öffnen | alle Pflichtpunkte, längste Zeit 390 ms (menu) | |
| H4 | M-C: Leiste bleibt unten, Fokus kehrt zurück | ungemessen (Grenzfälle 2 und 3) | |
| H4 | M-A zur Gegenprobe | scheitert an 4 von 8 Pflichtpunkten (menu) | |
| H4 | Aussehen der Menüs | Bilder `q3-menu-*.bmp` | |
| H5 | 20 Symbole flüssig, äußere Symbole ruhig | R1/hw: Bildschirm 10,4 % ausgelassen, Zittern 0 (frame-pacing) | |
| H5 | 64 Symbole flüssig | Aufbau p95 0,017 ms (render-cost); am Bildschirm ungemessen | |
| H6 | `MehrBar.sln` lädt im C# Dev Kit | CLI: Build ohne Warnung (tooling) | |
| H6 | Testexplorer zeigt und startet die Tests | CLI: `dotnet test` grün (tooling) | |
| H7 | Kontaktbogen sauber auf hell/dunkel/Schachbrett | 12/13 Fälle 256 px, Straight-Alpha umgerechnet (icons) | |
| H7 | Eine Netzwerkdatei (optional) | nur Laufwerkswurzel geprüft (icons) | |

## Grenzfälle zur Entscheidung (D-08)

Das Skript fragt sie im Schritt G der Reihe nach ab; die Handschritte selbst sind dort ausgelassen.

| Notiz | Grenzfall | Stephan |
|---|---|---|
| render-cost | D-18-Ersatz: Ausfallquote am Bildschirm messen (Plan 01-19), Maßstab RocketDock statt 1 % | |
| zorder-wind | Aero Peek per Taste: Messartefakt oder hinnehmen, falls H3 es bestätigt | |
| zorder-wind | Aero Peek per Ecke: gibt es das auf diesem System? | |
| menu | Leiste bleibt nach dem Menü oben (M-C): hinnehmbar oder M-D | |
| menu | Fokus nach dem Menü: Phase 2 gibt ihn zurück wie M-B | |
| menu | Einmal gemessen: Wiederholung nur bei abweichendem Handergebnis | |
| zoom-anchor | Dünner Rand der Genauigkeit: GO stehen lassen, `r` in Phase 3 neu kalibrieren | |
| zoom-anchor | Trennlinien-Regel nachträglich gefunden: gelten lassen oder Ränder neben Trennlinien UNKLAR | |
| icons | Ungemessen: 64/128-px-Rahmen, 125/150 %, eigener Bestand an `.url` | |
| alle | Messbedingungen: ein Monitor, 100 %, Leiste oben; 125/150 % und zweite Leiste mit Phase 2 | |
