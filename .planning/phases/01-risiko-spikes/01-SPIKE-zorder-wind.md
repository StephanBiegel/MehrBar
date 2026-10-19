# Spike Q2 — Ebene „ganz unten“ gegen Win+D und Verwandte

**Stand:** 2026-10-12 · Plan 01-14 (Messung abgeschlossen) und Plan 01-15 (S2-Folgemessung, Welle 7) · Grenzfälle teils erledigt, Rest offen · Code: `spikes/WindowHost/Variants/ZOrderGuard.cs` (Wächter S0/S1/S2), `spikes/Driver/Cases/ZOrderCase.cs`, `spikes/Driver/Cases/ZOrderTriggers.cs`, Lauf: `spikes/tools/run-zorder.ps1`

## Frage

Q2 (ROADMAP, Erfolgskriterium 2): Bleibt die Leiste unter `HWND_BOTTOM` über dem Desktop sichtbar, wenn Windows den Desktop zeigt oder die Fenster umordnet, und wenn nicht — kommt sie von selbst zurück, ohne vor andere Fenster zu springen (D-10, D-11)?

Auslöser (Testmatrix D-10), jeweils **ohne Wächter (S0)** und **mit Wächter (S1)**, S2 nur dort, wo S1 scheitert:

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

**Einwilligung und Messbedingungen:** Stephan antwortete am Haltepunkt mit **„los“**, also mit Explorer-Neustart **und** Auflösungswechsel. RocketDock war beendet (Lehre aus Q1). Hauptlauf am 2026-10-12, 14:39–14:51: `run-zorder.ps1 -AllowExplorer -AllowDisplay -Yes`, alle sechs Schritte (S0 und S1 je Grundmatrix, Explorer, Auflösung) mit Exit 0, kein Abbruch durch echte Eingabe. Weil S1 bei Win+D das Kriterium D-11 verfehlte, folgte im selben bestätigten Fenster (14:53) `run-zorder.ps1 -Guards S2 -IncludeS2 -S2Triggers win-d,peek-key -Yes` (Exit 0). Bedingungen: ein Monitor, 1920×1080 bei 59 Hz, 100 % Skalierung, Leiste am oberen Rand. Der Auflösungswechsel ging auf 1600×900 und zurück, dynamisch und nicht gespeichert (Symbolanordnung vorher als `.reg` unter `spikes/results/private/` gesichert, nicht im Repo).

**Wiederherstellung (Plan 01-14, Aufgabe 3, Schritt 6):** Die Zeilen „Session before/after“ des Skripts sind in beiden Läufen gleich: `TaskbarSd=absent explorer.exe=1 primary=1920x1080`. Nachprüfung nach dem Lauf: `reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" /v TaskbarSd` meldet den Wert als nicht vorhanden (wie vorher), `explorer.exe` läuft, der Anzeigemodus ist 1920×1080. Dass der Driver kein Fenster minimiert zurückgelassen hat, ist nicht unabhängig geprüft; die Rücknahme (Win+Umschalt+M und Wiederherstellung der vorher sichtbaren Fenster) lief ohne Fehlermeldung.

**S2-Folgemessung (Plan 01-15, Welle 7):** Im selben freigegebenen Fenster wie der Q3-Lauf (Stephan: „los“, mit Explorer-Neustart und Auflösungswechsel) lief am 2026-10-12 (Ende gegen 17:24) `run-zorder.ps1 -Guards S2 -IncludeS2` mit den Auslösern `win-d,corner,win-m,peek,alt-tab` je 20 Wiederholungen, `explorer` und `display` je 3 (`-AllowExplorer -AllowDisplay`), alle drei Schritte Exit 0. Ein erster Versuch brach nach etwa 5 s wegen echter Eingabe ab (Stephan saß noch an der Tastatur), **bevor etwas gemessen war**; der zweite Versuch mit 90 s Vorlauf blieb ungestört. Die Zeilen „Session before/after“ sind gleich: `TaskbarSd=absent explorer.exe=1 primary=1920x1080`. Ergebnisse unter `spikes/results/q2/s2-followup/`, die Dateien von Lauf 1 unter `spikes/results/q2/` bleiben unverändert. Der Lauf wurde vom Orchestrator aus dem Hauptverzeichnis gestartet (der Sandbox der ausführenden Sitzung ist PowerShell verwehrt); die Dateien sind unverändert übernommen. `peek-key` (Win+,) war nicht Teil dieses Laufs. Der Auflösungswechsel lief in den Protokollzeilen als 1920×1080@60 → 1600×900@60 und zurück (Lauf 1: @59).

**Messmangel, behoben:** Der S2-Folgelauf schrieb in dasselbe Ausgabeverzeichnis, ersetzte `runs.csv`, `events.csv` und `summary.md` und löschte `S0.md`/`S1.md`. Die Dateien von S0/S1 wurden aus einer unmittelbar vorher gezogenen Sicherung byte-gleich wiederhergestellt, die S2-Dateien als `runs-s2.csv`, `events-s2.csv`, `summary-s2.md` abgelegt. Ein Messwert ist dadurch nicht verändert. `run-zorder.ps1` ist korrigiert: Ein Lauf, der nicht zugleich S0 und S1 enthält, schreibt seine zusammengeführten Dateien mit Wächter-Suffix (`runs-s2.csv` usw.) und löscht nur die Einzeltabellen der Wächter, die er selbst gemessen hat. **Die Korrektur ist nur durch Lesen geprüft, nicht ausgeführt** (der Sandbox der ausführenden Sitzung ist PowerShell verwehrt).

**Nachmessen:**

```
powershell -NoProfile -ExecutionPolicy Bypass -File spikes/tools/run-zorder.ps1 -AllowExplorer -AllowDisplay
```

Der echte Lauf sendet Eingaben und fragt vorher nach „GO“. Ohne Explorer-Neustart und Auflösungswechsel: ohne die beiden Schalter. S2 nur für die in `-S2Triggers` genannten Auslöser: `-Guards S2 -IncludeS2 -S2Triggers win-d,corner`. Pipeline-Prüfung ohne Eingaben: `-DryRun` (Ausgabe unter `spikes/results/dry/q2/`, nicht im Repo).

## Messwerte

Quelle: `spikes/results/q2/` — `summary.md` (S0 und S1: größte Lücke, von selbst zurück, größte Zeit vor dem Zeugen), `S0.md`, `S1.md`, Einzelläufe in `runs.csv` (72 Zeilen: 2 Wächter × [6 Auslöser × 5 + Explorer 3 + Auflösung 3]), Ereignisse in `events.csv`; der S2-Folgelauf von Plan 01-14 als `S2.md`, `summary-s2.md`, `runs-s2.csv`, `events-s2.csv`; **die S2-Folgemessung von Plan 01-15** in `spikes/results/q2/s2-followup/` (`summary-s2.md`, `S2.md`, `runs-s2.csv` mit 106 Läufen, `events-s2.csv`). Alle Zahlen unten sind aus `runs.csv`/`runs-s2.csv` nachgerechnet (größte Lücke, Zahl der Wiederholungen mit Rückkehr, größte Zahl der Abtastwerte vor dem Zeugen je Wiederholung).

| Wächter | Auslöser | Läufe | max. Lücke ms | von selbst zurück | vor Zeuge (Abtastwerte je Wdh. / max. ms) | D-11 |
|---|---|---|---|---|---|---|
| S0 | win-d | 5 | 2968 | 0/5 | 0 / 0 | verfehlt |
| S0 | corner | 5 | 2934 | 0/5 | 0 / 0 | verfehlt |
| S0 | win-m | 5 | 0 | 5/5 | 0 / 0 | erfüllt |
| S0 | peek | 5 | 0 | 5/5 | 0 / 0 | erfüllt, aber Auslöser wirkungslos (s. u.) |
| S0 | peek-key | 5 | 2899 | 2/5 | 0 / 0 | verfehlt, Messung uneindeutig (s. u.) |
| S0 | alt-tab | 5 | 0 | 5/5 | 0 / 0 | erfüllt |
| S0 | explorer | 3 | 0 | 3/3 | 0 / 0 | erfüllt |
| S0 | display | 3 | 17 | 3/3 | 0 / 0 | erfüllt |
| S1 | win-d | 5 | 218 | 5/5 | 0, 0, **3**, 1, 1 / 43 | **verfehlt** (Wdh. 3: 3 Abtastwerte, 2 durch die Fensterreihenfolge bestätigt) |
| S1 | corner | 5 | 218 | 5/5 | 0 / 0 | erfüllt |
| S1 | win-m, alt-tab, explorer, display | 5/5/3/3 | 0/0/0/17 | alle zurück | 0 / 0 | erfüllt |
| S1 | peek | 5 | 0 | 5/5 | 0 / 0 | erfüllt, Auslöser wirkungslos |
| S1 | peek-key | 5 | 2868 | 2/5 | 0 / 0 | verfehlt, Messung uneindeutig |
| S2 (Lauf 1) | win-d | 5 | 200 | 5/5 | 0, 0, 0, 0, **1** / 5 | **Grenzfall** (1 Abtastwert, nicht durch die Fensterreihenfolge bestätigt) |
| S2 (Lauf 1) | peek-key | 5 | 2884 | 2/5 | 0 / 0 | verfehlt, identisch zu S0/S1 |
| S2 (Folgemessung) | win-d | 20 | 236 | 20/20 | 0 / 0 | erfüllt |
| S2 (Folgemessung) | corner | 20 | 271 | 20/20 | 0 / 0 | erfüllt |
| S2 (Folgemessung) | win-m | 20 | 0 | 20/20 | 0 / 0 | erfüllt |
| S2 (Folgemessung) | peek | 20 | 0 | 20/20 | 0 / 0 | erfüllt, aber Auslöser wirkungslos (s. u.) |
| S2 (Folgemessung) | alt-tab | 20 | 0 | 20/20 | 0 / 0 | erfüllt |
| S2 (Folgemessung) | explorer | 3 | 0 | 3/3 | 0 / 0 | erfüllt |
| S2 (Folgemessung) | display | 3 | 17 | 3/3 | 0 / 0 | erfüllt |

In Lauf 1 wurde S2 nur für Win+D und Win+, gemessen (die beiden Auslöser, an denen S1 scheiterte). Die **Folgemessung** (Plan 01-15) hat S2 für Win+D, Ecke, Win+M, Aero Peek per Ecke, Alt+Tab, Explorer und Auflösung gemessen (zusammen 106 Läufe); die Tabellenwerte sind aus `runs-s2.csv` nachgerechnet und stimmen mit `summary-s2.md` überein. **Nicht** in der Folgemessung war Win+, (`peek-key`); dafür gilt weiter Lauf 1.

**Beobachtungen**

- **Win+D, Ecke ohne Wächter:** Die Leiste verschwindet 48–85 ms nach dem Auslöser (Ecke, Wdh. 1: 367 ms) und kommt in 5/5 nicht von selbst zurück, solange der Desktop gezeigt wird (Lücke bis zum Ende des 3-s-Fensters). Annahme A11 („auf 26200 genügt `HWND_BOTTOM` ohne Wächter“) ist damit **widerlegt**. Erste Ecken-Läufe (S0 und S1, Wdh. 1) meldeten `corner-inactive` (der erste Klick zeigte den Desktop nicht); die Lücke trat trotzdem auf, ein Neustart des Explorers wurde nicht ausgelöst.
- **S1 bei Win+D:** Der Wächter erkennt „Desktop gezeigt“ 232–257 ms nach dem Auslöser und hebt die Leiste auf topmost; die Lücke ist 185–218 ms lang und die Leiste kommt in 5/5 zurück. Beim Zurückkehren der Fenster sitzt sie aber kurz vor dem Zeugen: 0, 0, 3, 1, 1 Abtastwerte (43, 6, 15 ms). Wdh. 3 liegt mit 3 Abtastwerten (2 durch die Fensterreihenfolge bestätigt) über der Grenzfall-Marke (1–2) und verletzt D-11 eindeutig. Die Ecke besteht mit S1 (0 vor dem Zeugen) — die Ursache des Unterschieds zu Win+D wurde nicht untersucht; die topmost-Anhebung ist der naheliegende Kandidat, denn S2 ohne topmost hat nur einen einzigen Abtastwert.
- **S2 bei Win+D:** Erkennung 185–243 ms nach dem Auslöser, Lücke 150–200 ms, 5/5 zurück, danach `zpos-bottom` innerhalb 1 ms nach `desktop-hidden`. Nur Wdh. 5 hat einen Abtastwert (5 ms) vor dem Zeugen, den die Fensterreihenfolge nicht bestätigt (0 von 1) — nach D-08 ein **Grenzfall**.
- **Welche WinEvents Win+D auf Build 26200 liefert** (Spalte `notes` in `runs.csv`): 2× `0x0003` (`EVENT_SYSTEM_FOREGROUND`) und 4–7× `0x8004` (`EVENT_OBJECT_REORDER`) je Auslösung; die Ecke zusätzlich `0x0008`/`0x0009` (Mausaufnahme Beginn/Ende) und 8–11× `0x8004`. Der Wächter erkennt den Zustand über das Desktop-Host-Fenster, nicht über ein eigenes Verbergen-Ereignis der Leiste.
- **Win+M, Alt+Tab:** Lücke 0 ms, 5/5, ohne Wächter. Hier genügt S0.
- **Aero Peek per Ecke (`peek`):** Lücke 0 in allen 10 Läufen, aber in keinem Schnappschuss der Läufe taucht das DWM-Fenster `LivePreview` auf — der Auslöser hat auf diesem Rechner nichts ausgelöst (Ursache nicht untersucht; die Peek-Vorschau per Maus ist unter Windows 11 möglicherweise nicht vorhanden). „D-11 erfüllt“ ist hier leer und kein Beleg.
- **Aero Peek per Taste (`peek-key`, Win+, 2 s gehalten):** Dasselbe Bild bei S0, S1 und S2: Wiederholungen 1, 3, 5 zeigen ein Fenster `LivePreview` (Besitzer `dwm`, 3840×1080, sichtbar, nicht verdeckt) an oberster Stelle der Fensterreihenfolge von T+0,5 s bis mindestens T+2 s; die Leiste bleibt in der Fensterreihenfolge unverändert unten, es gibt keine Wächter-Übergänge, 0 Abtastwerte vor dem Zeugen. Die Lücke läuft bis zum Ende des 3-s-Fensters (rund 1 s nach dem Loslassen), und der Deckungstest ist `cover-invalid`: die Zeugenfarbe war auch in den 3 s nach der Rücknahme nie zu sehen, die Vorschau stand also mindestens etwa 4 s nach dem Loslassen. Wiederholungen 2 und 4 zeigen **keinen** Peek (kein `LivePreview`, Lücke 0). Das Muster „jede zweite Wiederholung“ spricht für ein Artefakt der gesendeten Tasten (Peek hängt nach dem gesendeten Loslassen und wird erst vom nächsten Tastendruck beendet); bewiesen ist das nicht. Eine Ebenen-Strategie kann daran nichts ändern: Peek blendet alle Fenster aus, auch topmost.
- **Explorer-Neustart:** Die Leiste bleibt in 6/6 Läufen die ganze Zeit sichtbar (Lücke 0, über 1170 Abtastwerte). Windows startet den Explorer nach `taskkill /f` nicht von selbst innerhalb von 5 s; der Driver startet ihn danach (Shell wieder da nach 5,3–6,1 s), `TaskbarCreated` kommt 6,4–6,7 s nach dem Beenden (einmal 9,6 s), der Desktop-Host wird ≈ 10 ms später neu gefunden und die Leiste liegt 1–2 ms danach wieder ganz unten. S0 und S1 verhalten sich gleich.
- **Auflösungswechsel (1920×1080@59 → 1600×900@59 und zurück):** Lücke 17 ms (ein Abtastwert) in 6/6 Läufen; `display-change` 0,29–0,44 s nach dem Auslöser, Desktop-Host neu gefunden nach 17–47 ms, `zpos-bottom` nach weiteren 2–21 ms; Rückweg bei etwa 3,3–3,5 s ebenso. Der größte Abstand zwischen zwei Abtastwerten ist dabei 307–668 ms (der Bildschirm selbst wechselt); die Lückenzahl ist hier nur Richtwert.
- **S2-Folgemessung, Win+D (20 Wiederholungen):** größte Lücke 236 ms, in 20/20 von selbst zurück, **0 Abtastwerte vor dem Zeugen in allen 20** (Lauf 1: 1 von 5 mit einem Abtastwert, jetzt nicht reproduziert). Zehn Wiederholungen ohne messbare Lücke, die übrigen 17–236 ms. Die größte Lücke liegt weit außerhalb des Grenzbereichs von 450–550 ms, also kein Grenzfall nach D-08.
- **S2-Folgemessung, Ecke:** größte Lücke 271 ms, 20/20 zurück, 0 vor dem Zeugen. Wiederholung 1 meldet `corner-inactive` (der erste Klick zeigte den Desktop nicht, der Wiederholungsversuch ist ohne `--allow-explorer` übersprungen); der Wächter hat trotzdem `desktop-shown` (337 ms) und `desktop-hidden` gesehen, der Lauf zählt also. Dasselbe war schon in Lauf 1 aufgefallen.
- **S2-Folgemessung, Win+M und Alt+Tab:** Lücke 0 in je 20/20, 0 vor dem Zeugen. **Aero Peek per Ecke:** Lücke 0 in 20/20, aber ohne Wächter-Übergang und ohne `LivePreview` in `runs-s2.csv` und `events-s2.csv` — der Auslöser ist auch in dieser Messung wirkungslos, „erfüllt“ bleibt leer (Grenzfall 3).
- **S2-Folgemessung, Explorer (3):** Leiste die ganze Zeit sichtbar (Lücke 0, über 1180 Abtastwerte je Lauf); Explorer nach `taskkill /f` nicht von selbst zurück, vom Driver nach 5 s gestartet (Shell wieder da nach 5,26–5,27 s); `TaskbarCreated` 6,0–6,2 s nach dem Beenden, Desktop-Host 4–5 ms danach neu gefunden, Leiste 0–3 ms später wieder ganz unten. **Auflösungswechsel (3):** Lücke 17 ms (ein Abtastwert), `display-change` 61–95 ms nach dem Auslöser, Desktop-Host nach 10–14 ms neu gefunden, `zpos-bottom` 1–2 ms später, Rückweg bei etwa 3,05 s ebenso. Beides wie bei S0/S1 in Lauf 1.
- **Wiederherstellung nach der Folgemessung:** `Session before/after` gleich (`TaskbarSd=absent explorer.exe=1 primary=1920x1080`). Eine eigene Nachprüfung der minimierten Fenster fand nicht statt.

### Handprüfung (D-07)

Handteil H2 und H3 am 2026-10-19 (Stephan, Wächter S2, echte Tasten):

| Punkt | Messung | Von Hand |
|---|---|---|
| H2 Sperren/Entsperren | nicht automatisierbar | Leiste nach dem Entsperren sichtbar und unten — bestanden |
| H3 Win+D | 20/20, größte Lücke 236 ms | bestanden, Aufblitzen stört nicht |
| H3 Ecke „Desktop anzeigen“ | besteht D-11, Lücke bis 271 ms | bestanden, Aufblitzen stört nicht |
| H3 Win+M / Win+Umschalt+M | besteht | bestanden |
| H3 Win+, (Aero Peek per Taste) | Peek hing ≥ 4 s nach gesendetem Loslassen | Leiste kam sofort zurück — die Messung war ein Artefakt des gesendeten Loslassens |
| H3 Aero Peek per Ecke | Auslöser ohne Wirkung | bestanden |
| H3 Alt+Tab | besteht | Leiste blieb unten und steht nicht in der Liste — bestanden |

## Entscheidung

Auslöser für Auslöser nach D-11, dann die einfachste Strategie, die alle besteht (S0 < S1 < S2):

- **S0 scheidet aus:** Win+D und Ecke 0/5 von selbst zurück, Lücke fast 3 s. Der Wächter ist nötig (A11 widerlegt).
- **S1 scheidet aus:** Win+D hat in einer Wiederholung 3 Abtastwerte vor dem Zeugen (2 bestätigt) und insgesamt drei von fünf Wiederholungen mit Abtastwerten davor.
- **S2 besteht** alle in der Folgemessung gemessenen Auslöser nach D-11: Win+D (20/20 zurück, größte Lücke 236 ms, 0 Abtastwerte vor dem Zeugen in allen 20 — der einzelne Abtastwert aus Lauf 1 ist nicht reproduziert), Ecke (271 ms), Win+M, Alt+Tab, Explorer-Neustart, Auflösungswechsel. S2 ist die einzige Strategie ohne klare Verletzung. Der Wächter erkennt den Desktop 185–243 ms nach dem Auslöser (Lauf 1); die Lücke bleibt unter der 500-ms-Grenze und die Leiste springt nicht über topmost vor andere Fenster.
- **Win+, (Aero Peek per Taste) verfehlt D-11 bei allen drei Strategien gleich.** Ob das ein echter Verstoß ist, entscheidet die Messung nicht: Der Auslöser hängt die Peek-Vorschau (`LivePreview` von `dwm`) nach dem gesendeten Loslassen fest, in jeder zweiten Wiederholung wirkt er gar nicht, und der Deckungstest meldet sich selbst als ungültig. Keine Ebenen-Strategie ändert daran etwas. Die Entscheidung dazu fällt am Handteil mit echten Tasten (H3); sie ist unten als Grenzfall 2 benannt. Die Ecken-Peek-Messung ist leer (Grenzfall 3).
- Die in Lauf 1 offene Annahme „S2 ist bei Ecke, Win+M, Alt+Tab, Explorer und Auflösung nicht schlechter als S1“ ist durch die Folgemessung **belegt** (Grenzfall 4 erledigt). Aero Peek per Ecke bleibt leer: der Auslöser wirkt auch in der Folgemessung nicht (Grenzfall 3).

**Entscheidung:** GO — Strategie S2 (Wächter setzt die Leiste direkt über den Desktop-Host, nie topmost); S0 scheitert an Win+D und Ecke, S1 an Win+D; die S2-Folgemessung (Win+D, Ecke, Win+M, Alt+Tab je 20 Wiederholungen, Explorer und Auflösung je 3) erfüllt D-11 überall, 0 Abtastwerte vor dem Zeugen; Handprüfung H2/H3 bestanden (Win+, und Peek per Ecke von Hand ohne Befund; der gemessene Peek-Hänger war ein Artefakt des gesendeten Loslassens).

## Folgen für Phase 2/3

- **Phase 2 — `ZOrderKeeper` ist nötig (A11 widerlegt) und folgt S2:**
  - Immer: jede Z-Änderung der Leiste in `WM_WINDOWPOSCHANGING` auf `HWND_BOTTOM` umschreiben (S0-Grundlage, hält Win+M und Alt+Tab ohne weiteres Zutun).
  - Wächter: WinEvent-Haken `EVENT_SYSTEM_FOREGROUND` plus Takt von 250 ms im Normalbetrieb und 100 ms, solange der Desktop gezeigt wird. Erkennung am Desktop-Host-Fenster (`Progman`, hier gefunden; `WorkerW` mit `SHELLDLL_DefView` als zweite Möglichkeit). Reaktionszeit gemessen 185–243 ms; kürzere Takte könnten die Lücke verkleinern, kosten aber Aufwachvorgänge im Ruhezustand — nicht ohne Not ändern.
  - Liegt der Desktop-Host über der Leiste: Leiste direkt über den Host einfügen (`SetWindowPos` mit dem nächsten nicht-topmost-Fenster darüber bzw. `HWND_NOTOPMOST`); sobald der Host wieder unten ist (`desktop-hidden`), sofort zurück auf `HWND_BOTTOM` (gemessen ≤ 1 ms).
  - Desktop-Host neu nachschlagen bei `TaskbarCreated` (Explorer-Neustart: Host ≈ 10 ms danach da, Leiste bleibt sichtbar) und bei `WM_DISPLAYCHANGE` (Host nach 17–47 ms neu, Leiste 2–21 ms danach unten).
  - Sicherung gegen „hängen geblieben oben“: S2 benutzt kein topmost, die Gefahr ist kleiner als bei S1; trotzdem beim Beenden und beim Verlust des Wächters (Hakenfehler, Ausnahme im Takt) die Leiste auf `HWND_BOTTOM` zurückstellen.
- **Phase 2 — Abnahmetest:** `run-zorder.ps1` mit `-Guards S2` für alle Auslöser (Stephan muss das Fenster freigeben), Kriterium D-11 unverändert. Die Vorabmessung von S2 für alle Auslöser außer Win+, ist mit der Folgemessung (`spikes/results/q2/s2-followup/`) erledigt; sie dient als Vergleichswert.
- **Zusammenspiel mit dem Menü (Q3, `01-SPIKE-menu.md`):** Die Menütechnik M-C aktiviert die Leiste für die Dauer des Menüs (`SetForegroundWindow`) und hebt sie dabei über die Fenster; der Wächter reagiert auf `EVENT_SYSTEM_FOREGROUND`. Dass beide zusammen die Leiste unten halten und der Wächter die Aktivierung nicht als „Desktop gezeigt“ missdeutet, ist nicht gemessen und gehört in den Abnahmetest von Phase 2.
- **Phase 3:** nichts Neues aus Q2; Peek (`LivePreview`) blendet jede Leiste aus, auch eine topmost-Leiste — das ist Windows-Verhalten, kein Teil der Strategie.
- Kein NO-GO: ein Unterabschnitt `Folgespike (D-12)` entfällt. Er wäre nur nötig, wenn Stephan in Grenzfall 2 einen Verstoß feststellt, der sich mit keiner Strategie beheben lässt.

## Grenzfälle für Stephan

Keine. Entschieden am 2026-10-19: Win+, und Peek per Ecke sind durch die Handprüfung erledigt (D-07, das Handergebnis gilt). Offen für Phase 2 bleiben nur die Messbedingungen (125/150 %, zweite Leiste).
