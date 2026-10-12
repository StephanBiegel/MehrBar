# Spike Q2 — Ebene „ganz unten“ gegen Win+D und Verwandte

**Stand:** 2026-10-12 · Plan 01-14 (Gerüst, Messung nach Haltepunkt ausstehend) · Code: `spikes/WindowHost/Variants/ZOrderGuard.cs` (Wächter S0/S1/S2), `spikes/Driver/Cases/ZOrderCase.cs`, `spikes/Driver/Cases/ZOrderTriggers.cs`, Lauf: `spikes/tools/run-zorder.ps1`

## Frage

Q2 (ROADMAP, Erfolgskriterium 2): Bleibt die Leiste unter `HWND_BOTTOM` über dem Desktop sichtbar, wenn Windows den Desktop zeigt oder die Fenster umordnet, und wenn nicht — kommt sie von selbst zurück, ohne vor andere Fenster zu springen (D-10, D-11)?

Auslöser (Testmatrix D-10), jeweils **ohne Wächter (S0)** und **mit Wächter (S1)**:

| Auslöser | Name im Driver | Eingriff |
|---|---|---|
| Win+D | `win-d` | Taste gesendet, am Ende zurückgenommen |
| Ecke „Desktop anzeigen“ in der Taskleiste | `corner` | Klick, `TaskbarSd` für den Test eingeschaltet und zurückgesetzt |
| Win+M | `win-m` | Alle Fenster minimieren, am Ende Win+Umschalt+M |
| Aero Peek per Ecke | `peek` | Maus 2 s auf der Ecke |
| Aero Peek per Tastatur | `peek-key` | Win+, 2 s gehalten |
| Alt+Tab | `alt-tab` | Fensterwechsel |
| Explorer-Neustart | `explorer` | `explorer.exe` beenden und neu starten (nur mit Einwilligung) |
| Auflösungswechsel | `display` | Kurz auf eine andere Auflösung und zurück (nur mit Einwilligung) |
| Sperren/Entsperren | `lock` | Handteil (D-06, D-07) |

Entscheidungskriterium **D-11** je Auslöser, ohne Rückfrage (D-08): größte Lücke ≤ ca. 500 ms, die Leiste kommt in jeder Wiederholung von selbst zurück, 0 Abtastwerte vor dem verdeckenden Zeugenfenster. 1–2 Abtastwerte davor oder eine Lücke zwischen 450 und 550 ms sind ein Grenzfall und gehen an Stephan. Es gewinnt die **einfachste** Strategie, die alle Auslöser besteht (S0 < S1 < S2). Besteht keine, ist die Entscheidung NO-GO und ein zweiter, gezielter Spike nötig (D-12) — nie eine andere Ebene oder Autohide.

Vorbedingung aus Q1 (`01-SPIKE-window-hit.md`): Das Pixel-Orakel ist gültig (Selbsttest 5/5 und 5/5) und die Leiste ist unter `HWND_BOTTOM` über dem Desktop sichtbar (5/5). Beides war im Lauf 3 von Plan 01-13 erfüllt; ohne diese Vorbedingung wäre die Q2-Matrix wertlos.

## Aufbau

**Harness:** `WindowHost` mit Variante H1 (Alpha-1-Platte, Sieger aus Q1) ist die Leiste unter Test; ein zweites Prozessfenster, der **Zeuge** („partial“), deckt die Prüfpunkte 3 und 4 der Leiste ab. Der `Driver` ist Eingabequelle und unabhängiges Orakel; alle Eingaben laufen in einer `SafetySession` (Notabbruch Strg+Alt+Q, Abbruch bei echter Eingabe, Rücknahmen in umgekehrter Reihenfolge der Registrierung).

**Wächter (Strategien):**

| Strategie | Verfahren |
|---|---|
| S0 | Kein Wächter: jede Z-Änderung der Leiste (`WM_WINDOWPOSCHANGING`) wird auf `HWND_BOTTOM` umgeschrieben |
| S1 | S0 plus Wächter: WinEvent-Haken `EVENT_SYSTEM_FOREGROUND` und 250-ms-Takt (100 ms, solange der Desktop gezeigt wird). Liegt das Desktop-Host-Fenster (`Progman` bzw. `WorkerW` mit `SHELLDLL_DefView`, nachgeschlagen) über der Leiste, wird die Leiste vorübergehend auf topmost gehoben und fällt zurück nach unten, wenn die Fenster wieder da sind |
| S2 | Gleiche Erkennung, aber statt topmost wird die Leiste direkt über dem Desktop-Host eingefügt; läuft nur für Auslöser, an denen S1 scheitert |

**Messung je Lauf:** Lückenabtaster auf den Prüfpunkten 1, 2 und 5 (Leiste gilt als sichtbar, wenn mindestens zwei von drei den Sentinel zeigen). Die Abtastung wartet auf die Bildwiederholung, die echte Taktung ist daher eine Bildperiode (16,7 ms bei 60 Hz); das Ergebnis meldet den größten gesehenen Abstand. Nach der Rücknahme des Auslösers prüft der **Deckungstest** drei Sekunden lang Prüfpunkt 3: zeigt er den Sentinel statt der Zeugenfarbe, sprang die Leiste vor das Fenster. Zusätzlich Schnappschüsse der Fensterreihenfolge bei T+0, 0,5, 1 und 2 s (geschwärzt) und die Host-Zeilen (WinEvents, Wächter-Übergänge, `taskbar-created`, `display-change`).

**Einwilligungsregeln (RESEARCH Pitfall 3):** Jeder Eingriff in die laufende Sitzung braucht Stephans ausdrückliche Einwilligung am Haltepunkt von Plan 01-14. Explorer-Neustart nur mit `--allow-explorer`, Auflösungswechsel nur mit `--allow-display`, Sperren nur mit `--allow-lock`. Jede Rücknahme (`TaskbarSd`, minimierte Fenster, Explorer, Anzeigemodus) ist vor dem ersten Eingriff registriert. Der Trockenlauf sendet nichts und feuert nur den Auslöser `none`. RocketDock und andere Werkzeuge am oberen Rand müssen vorher beendet sein (Lehre aus Q1, Lauf 1).

**Einwilligung und Messbedingungen:** (wird nach dem Haltepunkt eingetragen: Antwort von Stephan, mit oder ohne Auflösungswechsel; Zustand von `TaskbarSd`, `explorer.exe` und Anzeigemodus vor und nach dem Lauf.)

**Nachmessen:**

```
powershell -NoProfile -ExecutionPolicy Bypass -File spikes/tools/run-zorder.ps1 -AllowExplorer -AllowDisplay
```

Der echte Lauf sendet Eingaben und fragt vorher nach „GO“. Ohne Explorer-Neustart und Auflösungswechsel: ohne die beiden Schalter. S2 nur für die in `-S2Triggers` genannten Auslöser: `-Guards S2 -S2Triggers win-d,corner`. Pipeline-Prüfung ohne Eingaben: `-DryRun` (Ausgabe unter `spikes/results/dry/q2/`, nicht im Repo).

## Messwerte

Quelle: `spikes/results/q2/` — `summary.md` (Wächter × Auslöser: größte Lücke, von selbst zurück, größte Zeit vor dem Zeugen), `S0.md`, `S1.md` (und `S2.md`, falls gemessen), Einzelläufe in `runs.csv`, Ereignisse in `events.csv`.

Noch nicht gemessen: Der Trockenlauf (`spikes/results/dry/q2/`, nicht im Repo) prüft nur die Pipeline. Die Messwerte, die WinEvents von Win+D auf Build 26200, der Explorer-Neustart (`TaskbarCreated` gesehen, Zeit bis die Leiste wieder sichtbar ist) und das Ergebnis des Auflösungswechsels (oder „nicht durchgeführt“) werden nach dem Haltepunkt eingetragen.

## Entscheidung

Die Entscheidung folgt der Reihenfolge Auslöser für Auslöser nach D-11, dann die einfachste Strategie, die alle besteht (S0 < S1 < S2). Gilt S0 bereits auf Build 26200, ist kein Wächter nötig (Annahme A11).

**Entscheidung:** UNKLAR — Messung nach Haltepunkt ausstehend

## Folgen für Phase 2/3

Vorläufig, bis die Messung entschieden ist:

- **Phase 2 — `ZOrderKeeper`:** Umfang hängt von der Strategie ab (kein Wächter, ereignisgetrieben oder mit Abfrage). Bei S1/S2 gehören dazu: Neu-Nachschlagen des Desktop-Hosts bei `TaskbarCreated` und `WM_DISPLAYCHANGE`, Abfrageintervall (falls S2) und eine Sicherung gegen „hängen geblieben oben“.
- **Fällt die Messung auf NO-GO (D-12):** Phase 2 bleibt ungeplant, bis der zweite Spike entschieden ist; der Unterabschnitt `### Folgespike (D-12)` benennt Frage, Wege, Zeitlimit, GO-Kriterium und Verantwortlichen.

## Grenzfälle für Stephan

Offen bleibt der Handteil (D-06, D-07), nach der Messung zu präzisieren:

1. **H2 — Sperren/Entsperren:** `Driver.exe zorder --guard <gewählt> --trigger lock --allow-lock` (Stephan sperrt und entsperrt selbst).
2. **H3 — echte Tasten je Auslöser:** `Driver.exe zorder --guard <gewählt> --trigger manual --seconds 120`, Win+D, Win+M, Alt+Tab, Ecke und Aero Peek von Hand; bei Abweichung gilt das Handergebnis (D-07).
3. **Auflösungswechsel**, falls am Haltepunkt ausgelassen.
4. **Messbedingungen:** ein Monitor, 100 % Skalierung, Leiste am oberen Rand, RocketDock beendet; 125/150 % und die zweite Leiste kommen mit Phase 2.
