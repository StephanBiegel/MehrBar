# Phase 1: Risiko-Spikes - Context

**Gathered:** 2026-10-09
**Status:** Ready for planning

<domain>
## Phase Boundary

Wegwerf-Prototypen messen auf diesem Rechner (Windows 11 Pro, Build 26200, Intel UHD Graphics 770, Desktop ohne Akku, 100 % Skalierung) die architekturbestimmenden Unbekannten, jede mit einer Go/No-go-Notiz:

1. Trefferfläche (Alpha 1) und Klick-Durchlass 1 px daneben, Erscheinen ganz unten ohne Aufblitzen, kein Fokusdiebstahl bei aktivem Notepad
2. Fensterebene `bottom` gegen Win+D und Verwandte, mit und ohne Wächter (WinEvent-Hook plus 250-ms-Abfrage); Strategie gewählt
3. Menütechnik am nicht aktivierbaren Fenster: WPF-ContextMenu gegen TrackPopupMenu
4. Zeichenkosten des transparenten Fensters (~1900×250 px) bei 60 fps, Hardware gegen SoftwareOnly
5. Zoom-Verankerung `cursor` als Core-Test gegen die vier Messreihen (±6 px)
6. Symbole: 256 px mit sauberem Alpha für .exe (nur 48 px und 256 px), .lnk samt Store-App, Ordner, .url, PDF, Netzpfad
7. Werkzeuge: CsWin32-Build und Testerkennung (MTP) in VS Code

Phase 1 trägt keine v1-Anforderung; sie senkt das Risiko für Phase 2 und 3. Ergebnisse können den Umfang von Phase 2 ändern (Wächter, Menütechnik, Zeichentechnik). Phase 2 wird erst nach den Notizen geplant.

</domain>

<decisions>
## Implementation Decisions

### Spike-Code: Ort, Schnitt, Zeitlimit
- **D-01:** Spike-Code liegt in `spikes/` im Repo, außerhalb der Solution, und wird committet (nachmessen bleibt möglich). Nach Phase 2 löschen oder eingefroren lassen.
- **D-02:** Die Zoom-Mathematik (Verankerung `cursor`/`center`) und ihr Test entstehen gleich im **echten** `src/MehrBar.Core` + `src/MehrBar.Tests`. Phase 1 legt dafür das Minimalgerüst an: Solution, `global.json` (SDK 10.0.401, MTP-Runner), `Directory.Packages.props`, Core (`net10.0`, ohne Pakete), Tests (`net10.0`, `OutputType Exe`, xunit.v3.mtp-v2 4.0.1). Die Testerkennung in VS Code (Frage 7) wird an diesem echten Projekt geklärt. Phase 3 baut darauf auf, statt neu zu schreiben. — **Reversibility:** costly — Phase 2 und 3 setzen auf diese Projektstruktur und das Testgerüst auf.
- **D-03:** Schnitt: **eine** WPF-Wegwerf-App für Fragen 1–4 (gleiches Fenster; Varianten per Schalter, Kommandozeile oder Tasten umschaltbar), ein eigener kleiner Symbol-Spike für Frage 6, Zoom als Core-Test (Frage 5). CsWin32 wird im Fenster-Spike gebaut (Frage 7).
- **D-04:** Zeitlimit **pro Spike**, etwa eine Sitzung je Frage. Ist das Budget ohne klares Ergebnis aufgebraucht, wird „No-go“ oder „unklar, Ausweichweg X“ notiert, statt weiterzubohren. Ausnahme: siehe D-11.

### Prüfung und Entscheidung
- **D-05:** Tests laufen **möglichst automatisiert**: Ein Skript bzw. die Spike-App löst die Fälle selbst aus (SendInput für Win+D/Win+M/Alt+Tab, Ecke „Desktop anzeigen“ per Maus, Explorer beenden und neu starten, Auflösungswechsel per `ChangeDisplaySettingsEx`, Notepad starten) und prüft per Win32 (Sichtbarkeit, `IsWindowVisible`/Cloaking, Z-Order, Vordergrundfenster, Fokus), mit Protokoll und Zeitstempeln.
- **D-06:** Was sich nicht verlässlich automatisieren lässt (Sperren/Entsperren, „ohne Aufblitzen“, Menüs mit Tastatur, gestartetes Programm kommt nach vorn), kommt in einen **kurzen Handteil**, eine Checkliste für Stephan in einem Durchgang am Phasenende (passt zu `human_verify_mode: end-of-phase`).
- **D-07:** **Der echte Weg gewinnt:** Jeder automatisierte Fall wird einmal von Hand mit echtem Tastendruck gegengeprüft. Weicht das Ergebnis ab, gilt das Handergebnis, und der Fall wandert in den Handteil.
- **D-08:** Go/No-go: **Klare Kriterien entscheiden selbst.** Trifft ein Messwert die Schwelle eindeutig, ist die Notiz endgültig; nur Grenzfälle gehen an Stephan.
- **D-09:** Notizformat: **eine Datei je Spike** im Phasenordner (z. B. `01-SPIKE-window-hit.md`, `01-SPIKE-zorder-wind.md`, `01-SPIKE-menu.md`, `01-SPIKE-render-cost.md`, `01-SPIKE-zoom-anchor.md`, `01-SPIKE-icons.md`, `01-SPIKE-tooling.md`) mit Frage, Aufbau, Messwerten, Entscheidung und Folgen für Phase 2/3. Dazu eine Sammelnotiz mit allen Ergebnissen und den Änderungen am Umfang von Phase 2.
- **D-10:** Testmatrix Ebene: Win+D, Ecke „Desktop anzeigen“, Win+M, Explorer-Neustart, Sperren/Entsperren **plus** Aero Peek, Alt+Tab, Auflösungswechsel; jeweils **mit und ohne** Wächter. Ruhezustand und RDP gehören nicht dazu.

### Rückfallgrenzen
- **D-11:** Win+D: Kurzes Verschwinden **bis etwa 0,5 s** ist hinnehmbar, wenn die Leiste **von selbst** zurückkommt und dabei **nicht vor andere Fenster springt**. Längeres oder dauerhaftes Verschwinden ist No-go.
- **D-12:** Ebene `bottom` ist nicht verhandelbar. Geht „ganz unten“ auf Build 26200 grundsätzlich nicht zuverlässig, gibt es **keinen** Ausweichweg über eine andere Ebene oder Autohide; stattdessen wird ein zweiter, gezielter Spike angesetzt (die harten Wege der Recherche: Einhängen in das Desktop-Fenster, Explorer-Hook). — **Reversibility:** one-way — eine andere Ebene würde Q10 und Stephans Arbeitsweise umstoßen.
- **D-13:** Zeichenkosten: Das **Go für Phase 2** hängt an der realen Leiste (~20 Symbole, Q18). Verfehlen 64 Symbole die Ziele, ist das **kein** No-go, sondern ein offener Punkt mit notiertem Ausweichweg (z. B. kleineres Fenster/Dirty-Rect, anderer Zeichenweg), der **vor Phase 3** gelöst sein muss, weil ZOOM-06 dort 60 fps mit 60+ Symbolen verlangt.
- **D-14:** Trefferfläche: Der Spike misst die Ausweichwege **gleich mit**: Alpha-1-Fläche, Hit-Region per `SetWindowRgn` und dynamisch umgeschaltetes `WS_EX_TRANSPARENT` (Konzept § 6). Das beste Verfahren gewinnt.
- **D-15:** Menüs: Schaffen weder WPF-ContextMenu noch TrackPopupMenu alle Punkte, darf sich die Leiste **nur für die Dauer des Menüs aktivieren** (SetForegroundWindow-Trick) und gibt den Fokus danach zurück. Pflicht bleiben: Schließen bei Klick daneben und mit Escape, Pfeiltasten/Enter, gestartetes Programm kommt nach vorn.

### Messung der Zeichenkosten
- **D-16:** Werkzeug: **eigene Messung plus PresentMon.** Die Spike-App misst die Render-Zeit je Frame selbst (Stopwatch an `CompositionTarget.Rendering`) und die CPU über die Prozesszeit; PresentMon (MIT, Intel, eine einzelne .exe) misst unabhängig, was der DWM tatsächlich darstellt. PresentMon wird in `spikes/` heruntergeladen, aber **nicht** committet (gitignore).
- **D-17:** Ablauf: Ein Skript fährt die Maus per SendInput reproduzierbar über die Leiste, mit fester Dauer (etwa 30 s je Lauf). Matrix: **20 und 64 Symbole × Hardware und SoftwareOnly**. Ausgewertet werden Median, 95. und 99. Perzentil der Frame-Zeit sowie das CPU-Mittel.
- **D-18:** Schwelle Frame-Zeit: **95. Perzentil ≤ 16,7 ms**, höchstens ~1 % ausgefallene Frames (laut PresentMon).
- **D-19:** Schwelle CPU: **≤ 15 % Anteil an der Gesamt-CPU** wie in der Task-Manager-Spalte; der Mehraufwand von `dwm.exe` während des Zooms wird mit protokolliert und in der Notiz genannt.

### Claude's Discretion
- Konkretes Budget je Spike innerhalb von „etwa einer Sitzung“, Reihenfolge der Spikes, Dateinamen der Notizen
- Wie die Varianten im Fenster-Spike umgeschaltet werden (Kommandozeile, Tasten, Konfigurationsdatei)
- Wie die Zeichenvarianten im Spike konkret aussehen (z. B. `DrawingVisual`/`OnRender` gegen `WriteableBitmap`), solange Hardware gegen SoftwareOnly gemessen wird
- Umfang des Symbol-Spikes jenseits der Roadmap-Liste (Zeitlimit für Netzpfade, Kalt-/Warm-Latenz messen, wie in der Recherche skizziert)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Konzept und Planung
- `docs/PROJEKT_START.md` — Gesamtkonzept; § 6 Zoom (Berechnung, technische Umsetzung, Klick-Durchlass), Q10 Fensterebene, Q12 samt **Nachtrag zu Q12 (Messtabelle der vier Messreihen)**, Q17, Q18
- `.planning/ROADMAP.md` — Phase 1: Ziel und fünf Erfolgskriterien
- `.planning/PROJECT.md` — Core Value, Constraints, Key Decisions
- `.planning/STATE.md` — offene Punkte (Phase 2 erst nach Go/No-go planen)

### Recherche
- `.planning/research/SUMMARY.md` — „Phase 0: Spikes“: Lieferumfang je Spike, Testfälle für Win+D, ZOrderKeeper-Ansatz
- `.planning/research/PITFALLS.md` — Win+D (HIGH), Hit-Testing, Zeichenkosten, Menüs am NOACTIVATE-Fenster
- `.planning/research/STACK.md` — Paketversionen (CsWin32 0.3.360, xunit.v3.mtp-v2 4.0.1), Symbol-Extraktion (`SIIGBF_ICONONLY`, Straight Alpha → `Pbgra32`), CsWin32-Fallstricke, VSTest-Ausweichweg
- `.planning/research/ARCHITECTURE.md` — Core/App-Schnitt, AxisFrame/Layout-Begriffe für die Zoom-Mathematik
- `.claude/CLAUDE.md` — Technology Stack inkl. „What NOT to Use“

### Referenzdaten
- `samples/reference/rocketdock-zoom-1..5.png` — Screenshots des Originals (gitignored, nur lokal); Grundlage der Messtabelle
- `samples/skins/CrystalXP.net/` — echter Skin (nicht ins Repo, nicht in Tests)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- Keine: Das Repo enthält noch keinen Code (kein `src/`). Phase 1 legt das Minimalgerüst für Core und Tests an (D-02).

### Established Patterns
- Aus Konzept und Recherche vorgegeben: Core ist `net10.0` ohne Windows-Assemblies und ohne Pakete; Win32 nur in `App/Interop` via CsWin32 mit `NativeMethods.txt`; Zoom/Layout rechnen achsenneutral.
- CsWin32: mehrdeutige Namen qualifizieren, benutzte Typen (`BITMAP`) mit auflisten, `EmitCompilerGeneratedFiles` nie in den Projektordner.

### Integration Points
- `src/MehrBar.Core` (Zoom-Mathematik) und `src/MehrBar.Tests` bleiben über Phase 1 hinaus stehen; `spikes/` hängt nicht in der Solution.
- Die Go/No-go-Notizen speisen die Planung von Phase 2 (Wächter, Menütechnik, Zeichentechnik, Trefferfläche).

</code_context>

<specifics>
## Specific Ideas

- Messtabelle (Nachtrag zu Q12) ist der Prüfstein des Zoom-Tests: Ruhe 109/698 (Breite 589, Mitte 404); Maus mittig −3/812; Maus links (2. Symbol) 27/812; Maus rechts (Publisher) −3/804; Toleranz ±6 px. Der linke Rand −3 kann Bildbeschnitt sein (offener Punkt vor Phase 3: Screenshots als Vollbild neu aufnehmen).
- Testfenster ca. 1900×250 px, transparent; Notepad als aktives Fenster für den Fokustest.
- Rechner: Intel UHD Graphics 770 (nur integrierte Grafik), kein Akku; PresentMon war nicht installiert.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 01-risiko-spikes*
*Context gathered: 2026-10-09*
