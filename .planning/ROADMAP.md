# Roadmap: MehrBar

## Overview

Der Weg beginnt mit zeitlich begrenzten Spikes, die die riskantesten Fragen des Fensters auf diesem Rechner klären (Trefferfläche, Klick-Durchlass, Ebene ganz unten und Win+D auf Build 26200, Menüs am nicht aktivierbaren Fenster, Zeichenkosten, Symbole, Zoom-Verankerung). Darauf folgt ein Grundgerüst, das RocketDock im Alltag schon ersetzt: eine Leiste im Hintergrund, befüllbar, startfähig, sicher gespeichert, mit Tray als Notausgang. Dann kommt der Kern des Projekts — der Zoom, der sich wie RocketDock anfühlt, samt Beschriftung — und danach der Skin CrystalXP.net, die bequeme Pflege der Einträge, das Konfigurationsfenster und zuletzt die robuste Release-Datei. Nach jeder Phase baut und startet die App, und das bisher Erreichte ist nutzbar (Konzept, Abschnitt 10).

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Risiko-Spikes** - Wegwerf-Prototypen klären Fenster, Win+D, Menüs, Zeichenkosten, Symbole und Zoom-Verankerung mit Go/No-go-Notizen
- [ ] **Phase 2: Grundgerüst** - Leere Leiste oben mittig, ganz unten und Win+D-fest, per Drag & Drop befüllbar, Klick startet, Konfiguration sicher gespeichert, Tray
- [ ] **Phase 3: Zoom und Beschriftung** - Zoom wie RocketDock (Abfall, Verankerung `cursor`, 60 fps, scharf) und Beschriftung des Symbols unter der Maus
- [ ] **Phase 4: Skin CrystalXP.net** - 9-Slice-Hintergrund mit Outside-Margins, Trennlinienbild, stiller Standard-Hintergrund als Rückfall
- [ ] **Phase 5: Einfügen, Umsortieren und Menüs** - Einfügemarke, Umsortieren, URLs aus dem Browser, Trennlinien, vollständige Kontextmenüs, Eigenschaften-Dialog
- [ ] **Phase 6: Konfigurationsfenster und Autostart** - Alle Leisten-Einstellungen live im Fenster, Skin-Auswahl, Zurücksetzen, Mit Windows starten
- [ ] **Phase 7: Robustheit und Release** - Unaufdringliche Startfehler, Ersatzsymbole, DPI-Stichprobe, Release-Datei zeigt die Leiste in unter 1 s

## Phase Details

### Phase 1: Risiko-Spikes

**Goal**: Die architekturbestimmenden Unbekannten sind auf diesem Rechner (Windows 11, Build 26200) mit Wegwerf-Prototypen gemessen und je mit einer Go/No-go-Notiz entschieden, bevor das Grundgerüst gebaut wird
**Mode:** mvp
**Depends on**: Nothing (first phase)
**Requirements**: None (Spike ohne eigene Anforderungen; senkt das Risiko für Phase 2 und 3)
**Success Criteria** (what must be TRUE):
  1. Ein Wegwerf-Fenster (ca. 1900×250 px, transparent, Trefferfläche mit Alpha 1) fängt Klicks und Mausbewegung über der Leistenfläche, lässt Klicks 1 px daneben zum Desktop durch, erscheint ohne Aufblitzen ganz unten (HWND_BOTTOM, TOOLWINDOW|NOACTIVATE) und nimmt einem aktiven Notepad beim Klick nicht den Fokus
  2. Für Win+D, „Desktop anzeigen“ in der Taskleisten-Ecke, Win+M, Explorer-Neustart und Sperren/Entsperren ist protokolliert, ob das Fenster mit und ohne Wächter (WinEvent-Hook plus 250-ms-Abfrage) sichtbar bleibt; die Strategie für die Fensterebene ist gewählt
  3. WPF-ContextMenu und TrackPopupMenu sind am nicht aktivierbaren Fenster verglichen (schließt bei Klick daneben und mit Escape, Pfeiltasten und Enter wirken, gestartete Programme kommen nach vorn); die Menütechnik ist entschieden
  4. Mit 64 Symbolen bei 60 fps sind Frame-Zeit und CPU des transparenten Fensters gemessen (Ziel ≤ 16 ms je Frame, ≤ 15 % CPU; Hardware gegen SoftwareOnly), und die Zeichentechnik ist mit Go oder Ausweichweg entschieden
  5. Ein Core-Test bildet die vier Messreihen des Originals mit Verankerung `cursor` auf ±6 px ab; der Symbol-Spike liefert 256-px-Symbole mit sauberem Alpha für .exe (nur 48 px und 256 px), .lnk samt Store-App, Ordner, .url, PDF und Netzpfad; CsWin32-Build und Testerkennung in VS Code sind geklärt

**Plans:** 8/18 plans executed (10 waves)

Plans:
**Wave 1**
- [x] 01-01-PLAN.md — Produktgerüst (global.json, Directory.Packages.props, MehrBar.sln, Core, Tests) + .gitignore + check-notes.ps1 (Welle 1)
- [x] 01-02-PLAN.md — Spike-Skelett mit CsWin32 (eigene spikes/Directory.Packages.props, WindowHost, Driver, RepoPaths, Stats, README) (Welle 1)

**Wave 2** *(blocked on Wave 1 completion)*
- [x] 01-03-PLAN.md — Messmaschine Host-Seite: Leiste unten (H1), Zeuge, Variantenregister (Welle 2)
- [x] 01-04-PLAN.md — Messmaschine Sicherheitsnetz und Eingabe (Notabbruch, Echteingabe-Abbruch, Wiederherstellung) (Welle 2)
- [x] 01-05-PLAN.md — Q5 Zoom-Verankerung als Core-Test (Vorhersage aus gemessenen Eingaben, center als Gegenprobe) + Notiz (Welle 2)
- [x] 01-06-PLAN.md — Q6 Symbol-Spike (alle Eintragstypen, Alpha, Latenz, STA/MTA, Netz) + Notiz (Welle 2)

**Wave 3** *(blocked on Wave 2 completion)*
- [x] 01-07-PLAN.md — Messmaschine Treiber-Seite: Orakel, HostProcess, `smoke` ohne Eingaben (Welle 3)
- [x] 01-08-PLAN.md — Q7 Werkzeuge (CsWin32 mit mehreren Listen, MTP-CLI) + Notiz (Welle 3)

**Wave 4** *(blocked on Wave 3 completion)*
- [ ] 01-09-PLAN.md — Q1-Instrument: Trefferflächen H2/H3, Fälle hit/focus/firstframe (Welle 4)
- [ ] 01-10-PLAN.md — Q2-Instrument: Wächter S0/S1/S2, alle Auslöser der Testmatrix (Welle 4)
- [ ] 01-11-PLAN.md — Q3-Instrument: Menüvarianten M-A..M-D und Prüfliste (Welle 4)
- [ ] 01-12-PLAN.md — Q4-Instrument: Zeichenvarianten R1/R2, Renderlauf, Matrix-Skript, PresentMon mit Hash-Prüfung (Welle 4)

**Wave 5** *(blocked on Wave 4 completion)*
- [ ] 01-13-PLAN.md — Q1 messen (Haltepunkt) + Notiz Trefferfläche, ggf. Folgespike D-12 (Welle 5)

**Wave 6** *(blocked on Wave 5 completion)*
- [ ] 01-14-PLAN.md — Q2 messen (Zustimmung, Haltepunkt) + Notiz Ebene/Win+D, ggf. Folgespike D-12 (Welle 6)

**Wave 7** *(blocked on Wave 6 completion)*
- [ ] 01-15-PLAN.md — Q3 messen (Haltepunkt) + Notiz Menütechnik (Welle 7)

**Wave 8** *(blocked on Wave 7 completion)*
- [ ] 01-16-PLAN.md — Q4 messen (ruhiger Rechner, UAC für PresentMon) + Notiz Zeichenkosten (Welle 8)

**Wave 9** *(blocked on Wave 8 completion)*
- [ ] 01-17-PLAN.md — Handteil H1–H7 in einem Durchgang, Notizen final (Welle 9)

**Wave 10** *(blocked on Wave 9 completion)*
- [ ] 01-18-PLAN.md — Sammelnotiz 01-SPIKE-ERGEBNISSE.md, ggf. VSTest-Rückfall, Nachmessen-Befehle (Welle 10)

**Hinweise**: Zeitlich begrenzt; jede Frage endet mit einer Go/No-go-Notiz im Phasenordner. Ergebnisse können den Umfang von Phase 2 ändern (Wächter für Win+D, Menütechnik, Zeichentechnik).

### Phase 2: Grundgerüst

**Goal**: Stephan kann MehrBar im Alltag statt RocketDock nutzen — eine Leiste oben mittig, immer ganz unten über dem Desktop, befüllbar trotz verdeckender Fenster, Einträge starten per Klick, die Konfiguration übersteht jeden Neustart; Symbole noch ohne Zoom in Grundgröße
**Mode:** mvp
**Depends on**: Phase 1
**Requirements**: LEIS-01, LEIS-02, LEIS-03, LEIS-04, LEIS-05, LEIS-06, LEIS-07, ITEM-01, ITEM-03, DND-01, DND-02, MENU-03, CONF-01, CONF-02, APP-01, APP-05
**Success Criteria** (what must be TRUE):
  1. Beim ersten Start ohne `%APPDATA%\MehrBar\config.json` erscheint eine leere Leiste oben mittig mit den Standardwerten (leicht über den Rand geschoben, `edgeOffset` −5); in der config.json geänderte Werte für Ausrichtung, Versatz und `edgeOffset` (auch negativ) wirken nach einem Neustart
  2. Die Leiste liegt hinter jedem geöffneten Fenster; ein Klick auf sie holt sie nicht nach vorn und lässt den Fokus beim aktiven Fenster; nach Win+D, „Desktop anzeigen“, Explorer-Neustart und Auflösungswechsel ist sie weiter sichtbar und an ihrem Platz; Klicks neben die Leiste, auch in den Bereich künftiger Zoom-Symbole, erreichen den Desktop bzw. das Fenster darunter
  3. Mit Tray „Leiste nach vorn“ steht die Leiste vor dem Explorer, bis etwas abgelegt wurde oder 30 s vergangen sind; aus dem Explorer gezogene Programme, Dateien, Verknüpfungen und Ordner werden am Ende angehängt, ohne dass die Quelle verschoben oder kopiert wird; „Eintrag hinzufügen…“ im Leisten-Kontextmenü fügt per Dateiauswahl hinzu, „Entfernen“ im Eintrags-Kontextmenü löscht; beide Menüs schließen bei Klick daneben oder mit Escape
  4. Ein Klick startet Programm, Datei oder Verknüpfung (gespeichert wird der .lnk-Pfad, Store-App-Verknüpfungen funktionieren) bzw. öffnet einen Ordner im Explorer, auch mit `%USERPROFILE%` im Pfad; die Symbole kommen aus hochauflösenden Shell-Symbolen
  5. Die Konfiguration übersteht Beenden der App und Windows-Neustart; vor jedem Speichern entsteht ein Backup, unbekannte Felder und Eintragstypen bleiben erhalten; das Tray-Symbol bietet Konfiguration (bis Phase 6: öffnet die config.json im Standard-Editor) · Leiste ein-/ausblenden · Leiste nach vorn · Beenden, übersteht einen Explorer-Neustart, und Fehler stehen im Log unter `%APPDATA%\MehrBar\logs\`

**Plans**: TBD
**Hinweise**: Konzept-Meilenstein 1, erweitert laut Recherche: Fensterebene samt Win+D-Robustheit, Trefferfläche mit Alpha 1, ConfigStore und Core-Layoutvertrag (Zoom-Fortschritt fest auf 0) gehören hierher, damit Phase 3 rein additiv ist. Nur eine Instanz (Mutex) läuft ab hier; ein erster Publish als Einzeldatei misst die Startzeit früh.

### Phase 3: Zoom und Beschriftung

**Goal**: Der Zoom fühlt sich an wie RocketDock mit CrystalXP.net — Symbole wachsen unter der Maus stufenlos mit dem gemessenen Abfall, das Symbol unter der Maus bleibt stehen, alles läuft flüssig, und das Symbol unter der Maus zeigt seinen lesbaren Namen
**Mode:** mvp
**Depends on**: Phase 2
**Requirements**: ZOOM-01, ZOOM-02, ZOOM-03, ZOOM-04, ZOOM-05, ZOOM-06, ZOOM-07, ZOOM-08, LABL-01, LABL-02, ITEM-04, ITEM-07, APP-06
**Success Criteria** (what must be TRUE):
  1. Mit den Standardwerten misst das Symbol unter der Maus 121 px, die direkten Nachbarn ca. 90–95 px, die übernächsten liegen nahe 32 px; das Symbol unter der Maus bleibt stehen, jede Seite weicht um ihren eigenen Mehrplatz aus, die Leiste wächst nur in der Breite, Symbole wachsen vom Rand weg, der Hintergrund behält seine Höhe; die vier Messreihen des Originals bestehen als Core-Test (±6 px), und `center` ist per Konfiguration wählbar
  2. Ein- und Auszoomen sind animiert (Standard 200 ms), beim Verlassen kehren alle Symbole auf Grundgröße zurück, und über Lücken zwischen gezoomten Symbolen und am Leistenrand flackert nichts; Grundgröße, Zoomgröße, Zoom-Breite und Zoom-Dauer wirken aus der Konfiguration
  3. Mit 60+ Symbolen (kleinere Grundgröße) läuft der Zoom mit 60 fps, gezoomte Symbole sind scharf, und im Ruhezustand zeichnet die Leiste keine Frames und braucht praktisch keine CPU
  4. Das Symbol unter der Maus zeigt seinen Namen auf der vom Rand abgewandten Seite (weiß, Kontur, leichter Schatten, Tahoma 13 px), nie über den Monitorrand hinaus; Beschriftung ein/aus, Schriftart und Schriftgröße wirken aus der Konfiguration; neue Einträge heißen wie die Verknüpfung bzw. wie die Dateibeschreibung der .exe statt `programm.exe`
  5. Ein Klick trifft das Symbol, das sichtbar unter dem Zeiger gezeichnet ist; ein schneller Doppelklick startet nur einmal, und der Klick gibt eine sichtbare Rückmeldung; ein Core-Test belegt Layout und Zoom achsenneutral über alle vier Ränder und mit mehreren Leisten pro Rand

**Plans**: TBD
**Hinweise**: Core Value lebt hier — bei Abwägungen geht der Zoom vor. Vor Beginn die Referenz-Screenshots als Vollbild neu aufnehmen (linker Rand −3 kann Bildbeschnitt sein), dazu eine Reihe mit Zoom-Breite 2/6/8 zur Bestimmung des Radius.

### Phase 4: Skin CrystalXP.net

**Goal**: Die Leiste sieht aus wie das Original — CrystalXP.net mit 9-Slice-Hintergrund und Outside-Margins folgt beim Zoom der Breite, Trennlinien tragen das Bild des Skins, und ein fehlender oder defekter Skin führt still zum Standard-Hintergrund
**Mode:** mvp
**Depends on**: Phase 3
**Requirements**: SKIN-01, SKIN-02, SKIN-04
**Success Criteria** (what must be TRUE):
  1. Mit CrystalXP.net in `Skins\` neben der .exe sieht die Leiste neben einem Screenshot des Originals gleich aus: 9-Slice-Hintergrund mit Rändern und Outside-Margins aus der INI, Ecken unverzerrt, Bedeutung der Felder vorher in `docs/skin-format.md` am echten Skin belegt
  2. Beim Zoom folgt der Skin-Hintergrund der Leistenbreite und behält seine Höhe; gezoomte Symbole ragen über ihn hinaus
  3. Trennlinien-Einträge (bis Phase 5 in der config.json eingetragen) werden mit dem Trennlinienbild des Skins gezeichnet und zoomen nicht mit; ohne Trennlinienbild erscheint eine einfache Linie
  4. Fehlt der Skin, ist die INI defekt oder ein Bild unlesbar, startet die Leiste ohne Meldung mit einem einfachen halbtransparenten Standard-Hintergrund und vermerkt das im Log

**Plans**: TBD
**Hinweise**: `docs/skin-format.md` ist Tor vor dem Code (Outside-*Margin und Strecken der Trennlinie sind unbelegt). Tests nutzen einen synthetischen Skin im Repo, nie `samples/skins/`.

### Phase 5: Einfügen, Umsortieren und Menüs

**Goal**: Die Leiste lässt sich bequem pflegen — Ablegen landet an der Einfügemarke, Einträge und Trennlinien lassen sich umsortieren, URLs kommen aus dem Browser, und die vollständigen Kontextmenüs samt Eigenschaften-Dialog sind da
**Mode:** mvp
**Depends on**: Phase 4
**Requirements**: DND-03, DND-04, DND-05, DND-06, ITEM-02, ITEM-08, MENU-01, MENU-02, MENU-04
**Success Criteria** (what must be TRUE):
  1. Beim Ziehen über die Leiste bleibt der Zoom aktiv und eine Einfügemarke zeigt die Zielposition; Ablegen fügt genau dort ein, auch beim Ablegen auf ein vorhandenes Symbol (daneben, kein „Öffnen mit“)
  2. Einträge und Trennlinien lassen sich per Ziehen umsortieren, und ein normaler Klick startet weiterhin (Mindestbewegung)
  3. Eine aus dem Browser gezogene URL wird zu einem URL-Eintrag mit Standardsymbol, der die Adresse im Standardbrowser öffnet
  4. Rechtsklick auf einen Eintrag bietet Starten · Als Administrator starten · Dateipfad öffnen · Eigenschaften · Entfernen; Rechtsklick auf die Leiste bietet Trennlinie einfügen (an der Mausposition) · Eintrag hinzufügen… · Konfiguration öffnen · Beenden; Trennlinien lassen sich so einfügen und löschen
  5. Der Eigenschaften-Dialog ändert Name, Pfad, Argumente, Arbeitsverzeichnis und ein eigenes Symbol (PNG/ICO), und die Leiste zeigt die Änderung sofort

**Plans**: TBD
**UI hint**: yes
**Hinweise**: Das Format-Raster für URL-Drops (Firefox, Edge, Chrome; UniformResourceLocatorW, Text, IDList) ist ein Testdurchlauf, keine Recherche. „Konfiguration öffnen“ öffnet bis Phase 6 die config.json.

### Phase 6: Konfigurationsfenster und Autostart

**Goal**: Alle Einstellungen der einen Leiste lassen sich in einem Fenster live ändern, ohne die config.json anzufassen, und MehrBar startet auf Wunsch mit Windows
**Mode:** mvp
**Depends on**: Phase 5
**Requirements**: CONF-03, CONF-04, CONF-05, APP-02, SKIN-03
**Success Criteria** (what must be TRUE):
  1. Das Konfigurationsfenster ändert Position (Ausrichtung, Versatz, `edgeOffset`), Grund- und Zoomgröße, Zoom-Breite und -Dauer, Abstand und Beschriftung; jede Änderung wirkt sofort an der Leiste und ist ohne Speichern-Knopf nach einem Neustart noch da
  2. Die Skin-Auswahl listet die Skins aus `Skins\` neben `MehrBar.exe`, und ein Wechsel wirkt sofort
  3. „Zurücksetzen auf Standardwerte“ stellt die Werte aus Q2 wieder her (oben/Mitte, `edgeOffset` −5, 32/121 px, Zoom-Breite 4, 200 ms, Tahoma 13, CrystalXP.net)
  4. „Mit Windows starten“ lässt sich ein- und ausschalten, und nach einem Windows-Neustart läuft MehrBar entsprechend bzw. eben nicht
  5. Tray „Konfiguration“, Doppelklick auf das Tray-Symbol, „Konfiguration öffnen“ im Leisten-Menü und ein zweiter Start von MehrBar öffnen dieses Fenster der laufenden Instanz; eine zweite Leiste entsteht nie

**Plans**: TBD
**UI hint**: yes

### Phase 7: Robustheit und Release

**Goal**: MehrBar ist als Release-Datei bereit für den Dauerbetrieb — Startfehler stören nicht, fehlende Ziele bleiben sichtbar, Windows-Skalierung wird sauber umgesetzt, und die einzelne .exe zeigt die Leiste in unter einer Sekunde
**Mode:** mvp
**Depends on**: Phase 6
**Requirements**: ITEM-05, ITEM-06, APP-03, APP-04
**Success Criteria** (what must be TRUE):
  1. Schlägt ein Start fehl (Ziel fehlt, Zugriff verweigert), erscheint eine kurze Meldung an der Leiste, die von selbst verschwindet, kein Dialogfenster, plus ein Eintrag im Log; ein abgebrochener UAC-Dialog bleibt still
  2. Einträge mit fehlendem Ziel (Programm deinstalliert, Netzpfad nicht erreichbar) zeigen ein Ersatzsymbol statt zu verschwinden, und die Leiste hängt beim Laden nicht
  3. Bei 100 % Windows-Skalierung ist alles scharf; bei 125 % und 150 % sehen Symbole, Skin und Beschriftung sauber aus, auch nach einem Wechsel der Skalierung im laufenden Betrieb
  4. Die Release-Ausgabe ist eine einzelne `MehrBar.exe` plus `Skins\`, läuft ohne installierte .NET-Runtime und zeigt die Leiste nach dem Start gemessen in unter 1 Sekunde
  5. Die Abnahmekriterien aus Abschnitt 11 des Konzepts bestehen mit der Release-Datei, einschließlich Win+D, Explorer-Neustart und Zoom mit 100+ Symbolen

**Plans**: TBD
**Hinweise**: ReadyToRun nur nach Messung; Symbol-Cache auf Platte mit Zeitlimit je Eintrag; Schutz gegen hängengebliebenes „vorn“ nach „Leiste nach vorn“.

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5 → 6 → 7

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Risiko-Spikes | 8/18 | In Progress | - |
| 2. Grundgerüst | 0/TBD | Not started | - |
| 3. Zoom und Beschriftung | 0/TBD | Not started | - |
| 4. Skin CrystalXP.net | 0/TBD | Not started | - |
| 5. Einfügen, Umsortieren und Menüs | 0/TBD | Not started | - |
| 6. Konfigurationsfenster und Autostart | 0/TBD | Not started | - |
| 7. Robustheit und Release | 0/TBD | Not started | - |
