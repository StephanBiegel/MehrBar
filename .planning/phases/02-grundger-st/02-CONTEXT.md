# Phase 2: Grundgerüst - Context

**Gathered:** 2026-10-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Stephan kann MehrBar im Alltag statt RocketDock nutzen: eine Leiste oben mittig, immer ganz unten über dem Desktop und Win+D-fest, befüllbar per Drag & Drop (über „Leiste nach vorn“) und per Dateiauswahl, Einträge starten per Klick, die Konfiguration übersteht jeden Neustart, Tray-Symbol als Notausgang, Log, nur eine Instanz, erster Publish als Einzeldatei. Symbole in Grundgröße, **noch ohne Zoom und ohne Skin** (Zoom: Phase 3, Skins: Phase 4).

Anforderungen: LEIS-01…07, ITEM-01, ITEM-03, DND-01, DND-02, MENU-03, CONF-01, CONF-02, APP-01, APP-05 (siehe `.planning/ROADMAP.md`, Phase 2, Erfolgskriterien 1–5).

</domain>

<decisions>
## Implementation Decisions

### Übernommen aus Phase 1 (`01-SPIKE-ERGEBNISSE.md`, gesperrt)
- **D-01:** Fensterebene über `ZOrderKeeper` nach Strategie S2: jede Z-Änderung in `WM_WINDOWPOSCHANGING` auf `HWND_BOTTOM`, Wächter mit `EVENT_SYSTEM_FOREGROUND` plus Takt 250 ms (100 ms, solange der Desktop gezeigt wird), bei gezeigtem Desktop direkt über den Desktop-Host, nie topmost; Host neu suchen bei `TaskbarCreated` und `WM_DISPLAYCHANGE`.
- **D-02:** Trefferfläche als Alpha-1-Platte unter den Symbolen; `TOOLWINDOW|NOACTIVATE`, `MA_NOACTIVATE`, `HWND_BOTTOM` vor dem ersten Bild. Rückfall H2 (Region).
- **D-03:** Kontextmenüs nativ nach M-C (`TrackPopupMenuEx`, Leiste als Besitzer, `SetForegroundWindow` davor, `WM_NULL` danach); vorheriges Vordergrundfenster ausdrücklich zurückgeben, Leiste danach auf `HWND_BOTTOM`.
- **D-04:** Zeichnen mit R1 (`OnRender`, alles neu) im transparenten WPF-Fenster, Hardware; Core-Layout über `ZoomEngine` mit Zoom-Fortschritt fest 0 (Layoutvertrag für Phase 3).
- **D-05:** IconCache auf 2–4 STA-Arbeitsthreads, `SIIGBF_ICONONLY`, einmal nach `Pbgra32` + `Freeze()`, Zeitlimit 3 s je Eintrag, Platzhalter während des Ladens.
- **D-06:** Tests mit xUnit v3 über VSTest (Rückfall B, seit 2026-10-19).

### Aussehen der Leiste ohne Skin
- **D-07:** Hintergrund in Phase 2 ist der im Konzept vorgesehene **einfache halbtransparente Standard-Hintergrund** (später Rückfall, wenn ein Skin fehlt oder defekt ist).
- **D-08:** Die **leere Leiste** erscheint als kurzer Kasten in der Breite von etwa drei Symbolen, **ohne Hinweistext**.
- **D-09:** Symbole in **Grundgröße 32 px** wie bei RocketDock (Stephans bisherige Leiste).

### Klick auf einen Eintrag mit fehlendem Ziel
- **D-10:** Es erscheint ein **Meldungsfenster** mit dem Fehler und den Knöpfen **„Entfernen“** und **„Behalten“**; „Entfernen“ löscht den Eintrag wie das Kontextmenü (inklusive Backup beim Speichern). Zusätzlich ein Eintrag im Log. Texte deutsch in Ressourcen-Dateien.

### Entfernen und Duplikate
- **D-11:** „Entfernen“ im Eintrags-Kontextmenü löscht **sofort, ohne Rückfrage** (wie RocketDock); das Backup vor jedem Speichern fängt einen Fehlgriff ab.
- **D-12:** **Duplikate sind erlaubt:** Ein Programm, das schon auf der Leiste liegt, wird beim erneuten Hineinziehen oder Hinzufügen ein zweites Mal angehängt.

### Nach der Planung entschieden (2026-10-20)
- **D-13:** Die Einzelinstanz-Sperre (benannter Mutex) kommt in Phase 2 (Teil von APP-02); dass ein zweiter Start die Konfiguration öffnet, bleibt Phase 6 (Stephan).

### Claude's Discretion
- Farbe, Deckkraft und Eckenradius des Standard-Hintergrunds; Abstand der Symbole; Wortlaut der Meldung (Phase 4 ersetzt den Hintergrund durch Skins).
- Ob das Meldungsfenster ein WPF-Fenster oder eine Windows-Meldung ist — Hauptsache, es nimmt der Leiste nicht dauerhaft die Ebene „ganz unten“.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Konzept und Anforderungen
- `docs/PROJEKT_START.md` — Konzept (Abschnitte 1–12, Grill-Protokoll Q1–Q20); Erststart Q4, Tray Q15, Meilenstein 1 in Abschnitt 10, Standard-Hintergrund Abschnitt 8 Punkt 4
- `.planning/ROADMAP.md` — Phase 2, Ziel und Erfolgskriterien 1–5
- `.planning/REQUIREMENTS.md` — LEIS-01…07, ITEM-01, ITEM-03, DND-01, DND-02, MENU-03, CONF-01, CONF-02, APP-01, APP-05
- `.planning/research/ARCHITECTURE.md`, `.planning/research/STACK.md`, `.planning/research/PITFALLS.md` — Architektur, Pakete, Fallstricke

### Ergebnisse aus Phase 1 (gesperrte Technik)
- `.planning/phases/01-risiko-spikes/01-SPIKE-ERGEBNISSE.md` — Zusammenfassung, Änderungen am Umfang von Phase 2
- `.planning/phases/01-risiko-spikes/01-SPIKE-window-hit.md` — Trefferfläche, Fenstererzeugung
- `.planning/phases/01-risiko-spikes/01-SPIKE-zorder-wind.md` — ZOrderKeeper S2, Abnahme mit `run-zorder.ps1 -Guards S2`
- `.planning/phases/01-risiko-spikes/01-SPIKE-menu.md` — Menütechnik M-C, offene Abnahmepunkte (Wächter-Zusammenspiel, `EndMenu`)
- `.planning/phases/01-risiko-spikes/01-SPIKE-render-cost.md`, `.planning/phases/01-risiko-spikes/01-SPIKE-frame-pacing.md` — R1/Hardware, `ComputeSmooth`
- `.planning/phases/01-risiko-spikes/01-SPIKE-icons.md` — IconCache, Ersatzregeln
- `.planning/phases/01-risiko-spikes/01-SPIKE-tooling.md` — CsWin32-Pin, VSTest, Filter-Syntax
- `spikes/README.md` — Nachmess-Befehle; Bildschirmläufe nur vom Desktop, nicht aus einer Hivemo-Sitzung

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/MehrBar.Core/Layout/ZoomEngine.cs`, `ZoomKernel.cs`: Layoutrechnung achsenneutral; Phase 2 nutzt sie mit Zoom-Fortschritt 0.
- `spikes/WindowHost/HostWindow.cs`, `spikes/WindowHost/Variants/PlateH1.cs`, `ZOrderGuard.cs`, `MenuNative.cs`: belegte Vorlagen für BarWindow, Platte, Wächter S2 und natives Menü (Wegwerfcode, als Muster lesen, nicht verlinken).
- `spikes/Icons/`: belegte Shell-Symbol-Extraktion (STA, Pbgra32).

### Established Patterns
- Win32 nur in `App/Interop` über CsWin32 0.3.360 (`allowMarshaling: false`, mehrere `NativeMethods.txt` nach Thema).
- Alles ohne Oberfläche Testbare in `MehrBar.Core` mit xUnit (VSTest).
- Code, Kommentare, Commits Englisch; UI-Texte deutsch in Ressourcen-Dateien.

### Integration Points
- Neues Projekt `src/MehrBar.App` (net10.0-windows, WPF) referenziert `MehrBar.Core`; CsWin32-Pin wandert ins Wurzel-`Directory.Packages.props`.
- `%APPDATA%\MehrBar\config.json` (Backup, atomares Schreiben, unbekannte Felder und Typen erhalten), Log unter `%APPDATA%\MehrBar\logs\`.

</code_context>

<specifics>
## Specific Ideas

- Vorbild ist Stephans RocketDock-Leiste oben am Bildschirm: Grundgröße 32 px, Entfernen ohne Rückfrage, Duplikate möglich.
- Die leere Leiste soll unaufdringlich sein — kein Text, nur ein kleiner Kasten als Ziel zum Hineinziehen.

</specifics>

<deferred>
## Deferred Ideas

- Unaufdringliche Startfehler-Meldung als Feinschliff bleibt Phase 6; in Phase 2 gilt das Meldungsfenster aus D-10.
- Offen aus Phase 1 und hier zu prüfen, aber keine neue Fähigkeit: echte Netzdatei für Symbole, 125/150 % Skalierung, Symbole mit 64/128-px-Rahmen.

</deferred>

---

*Phase: 02-grundgerüst*
*Context gathered: 2026-10-19*
