# MehrBar

## What This Is

MehrBar ist ein schlanker Programmstarter für Windows im Stil von RocketDock: eine Leiste am Bildschirmrand, deren Symbole beim Überfahren mit der Maus stufenlos vergrößern, mit Beschriftung, Trennlinien, Drag & Drop und RocketDock-Skin-Unterstützung. Version 1 ist für den Eigengebrauch des Entwicklers (Stephan) und ersetzt seine RocketDock-Leiste oben am Bildschirm; der eigentliche Anlass ist eine zweite Leiste am rechten Rand, die nach V1 kommt.

Vollständiges Konzept mit Begründung jeder Entscheidung: `docs/PROJEKT_START.md` (Hauptteil Abschnitte 1–12, Grill-Protokoll Q1–Q20).

## Core Value

**Der Zoom fühlt sich an wie RocketDock mit CrystalXP.net** — flüssig, vertraut, mit demselben Abfall und derselben Verankerung. Ohne das ist MehrBar kein Ersatz; bei Abwägungen geht der Zoom vor.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] Eine Leiste oben mittig; Datenmodell trägt mehrere Leisten, auch mehrere pro Rand
- [ ] Position: Ausrichtung (Anfang/Mitte/Ende), Versatz entlang des Rands, Abstand zum Rand (`edgeOffset`, darf negativ sein)
- [ ] Fensterebene „immer im Hintergrund“: ganz unten über dem Desktop, Klick holt sie nicht nach vorn, kein Fokusdiebstahl, übersteht Win+D
- [ ] Zoom: Grundgröße, Zoomgröße, Zoom-Breite, Zoom-Dauer; Leiste wächst nur in der Breite, Symbol unter der Maus bleibt stehen; 60 fps auch mit 60+ Symbolen; Abfall kalibriert gegen Screenshots des Originals
- [ ] Beschriftung des Symbols unter der Maus (weiß, Kontur, Schatten; ein/aus, Schriftart, Größe)
- [ ] Eintragstypen: Programm/Datei/Verknüpfung, URL, Ordner, Trennlinie (polymorph, `group` später ohne Migration)
- [ ] Erster Start ohne Konfiguration: leere Leiste oben mittig mit Standardwerten
- [ ] Drag & Drop aus dem Explorer (früh: anhängen; später: Einfügemarke, Umsortieren, URLs aus dem Browser)
- [ ] Kontextmenü Eintrag: Starten · Als Administrator starten · Dateipfad öffnen · Eigenschaften · Entfernen
- [ ] Kontextmenü Leiste: Trennlinie einfügen · Konfiguration öffnen · Beenden
- [ ] Eigenschaften-Dialog: Name, Pfad, Argumente, Arbeitsverzeichnis, eigenes Symbol (PNG/ICO)
- [ ] Hochauflösende Symbole (bis 256 px), scharf beim Zoom
- [ ] RocketDock-Skin CrystalXP.net korrekt (9-Slice, Outside-Margins, Trennlinie), Skins aus `Skins\` neben der .exe, Standard-Hintergrund als Rückfall
- [ ] Konfigurationsfenster für die eine Leiste, Änderungen live, automatisch gespeichert, Zurücksetzen auf Standardwerte, Mit Windows starten
- [ ] Tray-Symbol als Notausgang: Konfiguration · Leiste ein-/ausblenden · Beenden
- [ ] Konfiguration als JSON unter `%APPDATA%\MehrBar\`, Backup + atomares Schreiben, unbekannte Felder bleiben erhalten
- [ ] Nur eine Instanz; Per-Monitor-DPI-aware (v2); Start < 1 s, kaum CPU im Ruhezustand

### Out of Scope

- Zweite Leiste rechts, senkrechte Leisten links/rechts — Kernanlass, aber nach V1 (Q6); Architektur muss sie tragen (achsenneutral)
- Mehrere Leisten im UI anlegen/löschen — V1 hat eine Leiste (Q7)
- Gruppen / Unterleisten — später (Q8)
- Autohide — in RocketDock ohnehin aus (Q9)
- Fensterebenen `normal`/`top` — V1 nur „unten“ (Q10)
- Eigene Prozent-Skalierung je Leiste — Grund-/Zoomgröße direkt einstellbar, DPI übernimmt Windows (Q11)
- Import aus RocketDock — Befüllen per Drag & Drop dauert 5 Minuten, spart Code (Q4)
- Mehrere Monitore, AppBar-Platzreservierung, mehrere Skins, Skin-Editor, Mehrsprachigkeit, Installer — Release-Themen für V2+ (Q1)
- Anzeige laufender Programme, Fenster-Vorschau, Taskleisten-Ersatz — ausdrücklich nicht, MehrBar startet nur
- Widgets / Docklets — ausdrücklich nicht

## Context

- Ersetzt RocketDock 1.3.5 (installiert unter `C:\Program Files (x86)\RocketDock`). Referenzwerte aus dessen Registry: IconMin 32, IconMax 121, ZoomWidth 4, ZoomTicks 200 ms, vOffset −5, Beschriftung Tahoma 13.
- Referenz-Skin CrystalXP.net liegt lokal in `samples/skins/` (gitignored, Lizenz ungeklärt): `background.ini` mit Margins 8 und Outside-Margins 20/13/23/10, `separator.ini`, `bg.png`, `sep.png`.
- Fünf vermessene Screenshots des Originals in `samples/reference/` (gitignored): Mitte bleibt beim Zoom bis auf ±40 px fest; direkte Nachbarn ca. 90–95 px, übernächste nahe Grundgröße → RocketDocks `ZoomWidth` meint die Gesamtbreite der Zoomzone (Radius ≈ 2,5 für 4).
- Arbeitsplatz: Windows 11, 100 % Windows-Skalierung, ein Hauptmonitor.
- Der Hintergrund behält beim Zoom seine Höhe; gezoomte Symbole ragen darüber hinaus.

## Constraints

- **Tech stack**: C# / .NET 10 (LTS, SDK 10.0.401 installiert) / WPF — Festlegung im Konzept
- **Plattform**: Windows 10/11 x64, self-contained single-file .exe, `Skins\` als Ordner daneben
- **Architektur**: Alles ohne Oberfläche Testbare (Zoom, Layout, Config, Skin-Parser) in `MehrBar.Core` mit xUnit-Tests; Win32 nur in `App/Interop` — damit Zoom-Mathematik testbar und die rechte Leiste ohne Umbau möglich ist
- **Achsenneutralität**: Zoom und Layout rechnen entlang einer abstrakten Leistenachse — die rechte Leiste ist der eigentliche Anlass
- **Rendering**: eigenes leichtgewichtiges Control (`DrawingVisual`/`OnRender`), keine ItemsControl mit vielen Einzel-Controls — für flüssigen Zoom
- **Performance**: 60 fps beim Zoom mit 60+ Symbolen, Start < 1 s, im Ruhezustand keine Frames
- **Abhängigkeiten**: keine schweren Fremdbibliotheken, nur gepflegte NuGet-Pakete mit kompatibler Lizenz
- **Sprache**: Code, Kommentare, Commits Englisch; UI Deutsch, aber in Ressourcen-Dateien
- **Rechte**: Fremde Skins und Screenshots nie ins Repo

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| V1 nur Eigengebrauch, Architektur releasefähig | Release-Aufwand erst, wenn sinnvoll (Q1) | — Pending |
| Referenz-Skin CrystalXP.net, Skins aus `Skins\` neben der .exe, RocketDock-Ordner nicht lesen | Einfach; Rechte vor Release klären oder eigene Skins (Q2, Q14) | — Pending |
| Beschriftung in V1 | Gehört zum gewohnten Erscheinungsbild (Q3) | — Pending |
| Kein RocketDock-Import, leere Startleiste | 5 Minuten Handarbeit statt Code (Q4) | — Pending |
| Drag & Drop geteilt: Anhängen früh, Rest später | Leiste von Anfang an mit echten Programmen nutzbar (Q5) | — Pending |
| Eine Leiste oben mittig; Modell für mehrere pro Rand mit `edgeOffset` | V1 schlank, rechte Leiste ohne Migration (Q6, Q7) | — Pending |
| Keine Gruppen, kein Autohide in V1 | Nicht genutzt bzw. nicht nötig (Q8, Q9) | — Pending |
| Fensterebene „immer im Hintergrund“ (`layer: bottom`) | So nutzt der Entwickler die Leiste (Q9, Q10) | — Pending |
| Keine Prozent-Skalierung, Per-Monitor-DPI v2 | Doppelter Regler vermieden (Q11) | — Pending |
| Zoom-Verankerung `cursor` (Symbol unter der Maus bleibt stehen), als Parameter; `center` wählbar | Q12 revidiert: Auswertung der Messtabelle je Seite passt nur zu diesem Modell, wie das Original | — Pending |
| Befüllen: Tray „Leiste nach vorn“ (bis Ablegen / 30 s) + „Eintrag hinzufügen…“ | Leiste liegt hinter allen Fenstern, Explorer verdeckt sie (Q17) | — Pending |
| Keine Sonderbehandlung bei Überlauf der Bildschirmbreite | ~20 Symbole, einfachste Lösung (Q18) | — Pending |
| Ablegen auf Programmsymbol = einfügen, nicht „öffnen mit“ | Einheitliches Verhalten (Q20) | — Pending |
| Spike-Phase vor dem Grundgerüst | Win+D, Klick-Durchlass, Zeichenleistung, Menüs auf nicht aktivierbarem Fenster bestimmen die Architektur (Recherche) | — Pending |
| Minimales Tray-Symbol | Notausgang, weil die Leiste im Hintergrund liegt (Q15) | — Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-10-09 after initialization*
