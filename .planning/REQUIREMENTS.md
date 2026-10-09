# Requirements: MehrBar

**Defined:** 2026-10-09
**Core Value:** Der Zoom fühlt sich an wie RocketDock mit CrystalXP.net — flüssig, vertraut, mit demselben Abfall und derselben Verankerung.

Quellen: `docs/PROJEKT_START.md` (Hauptteil + Grill Q1–Q20), `.planning/research/SUMMARY.md`.

## v1 Requirements

### Leiste und Fenster

- [ ] **LEIS-01**: Beim ersten Start ohne Konfiguration erscheint eine leere Leiste oben mittig mit den Standardwerten (Q2, Q4)
- [ ] **LEIS-02**: Die Leiste lässt sich am Rand ausrichten (Anfang/Mitte/Ende), entlang des Rands versetzen und mit `edgeOffset` vom Rand abrücken, auch negativ (Q7)
- [ ] **LEIS-03**: Die Leiste liegt immer ganz unten in der Fensterreihenfolge, direkt über dem Desktop; jedes geöffnete Fenster überdeckt sie (Q10)
- [ ] **LEIS-04**: Ein Klick auf die Leiste holt sie nicht nach vorn und nimmt dem aktiven Fenster nicht den Fokus (Q10)
- [ ] **LEIS-05**: Die Leiste ist nach Win+D / „Desktop anzeigen“, nach einem Explorer-Neustart und nach einem Auflösungswechsel weiter sichtbar und an ihrem Platz
- [ ] **LEIS-06**: Klicks neben die sichtbare Leiste (auch in den Bereich, in den gezoomte Symbole ragen könnten) erreichen den Desktop bzw. das Fenster darunter
- [ ] **LEIS-07**: Über das Tray-Menü „Leiste nach vorn“ steht die Leiste vorn, bis etwas abgelegt wurde oder 30 s vergangen sind (Q17)

### Zoom

- [ ] **ZOOM-01**: Beim Überfahren wachsen die Symbole unter der Maus stufenlos; Grundgröße, Zoomgröße und Zoom-Breite sind einstellbar
- [ ] **ZOOM-02**: Mit den Standardwerten entspricht der Abfall dem Original: Symbol unter der Maus 121 px, direkte Nachbarn ca. 90–95 px, übernächste nahe Grundgröße
- [ ] **ZOOM-03**: Die Leiste wächst beim Zoom nur in der Breite; das Symbol unter der Maus bleibt stehen, jede Seite weicht um ihren eigenen Mehrplatz aus (`cursor`); „Mitte fest“ (`center`) ist wählbar (Q12)
- [ ] **ZOOM-04**: Ein- und Auszoomen sind animiert mit einstellbarer Dauer (Standard 200 ms); beim Verlassen zoomen alle Symbole auf Grundgröße zurück
- [ ] **ZOOM-05**: Symbole wachsen vom Bildschirmrand weg; der Skin-Hintergrund behält seine Höhe und folgt nur der Breite
- [ ] **ZOOM-06**: Der Zoom läuft flüssig mit 60 fps, auch mit 60+ Symbolen; im Ruhezustand verbraucht die Leiste praktisch keine CPU
- [ ] **ZOOM-07**: Beim Überfahren von Lücken zwischen gezoomten Symbolen und am Leistenrand flackert der Zoom nicht
- [ ] **ZOOM-08**: Symbole sind auch gezoomt scharf (Quelle bis 256 px)

### Beschriftung

- [ ] **LABL-01**: Das Symbol unter der Maus zeigt seinen Namen auf der vom Rand abgewandten Seite — weiße Schrift mit Kontur und leichtem Schatten, nie über den Monitorrand hinaus (Q3)
- [ ] **LABL-02**: Beschriftung ein/aus, Schriftart und Schriftgröße sind einstellbar (Standard Tahoma, 13 px)

### Einträge und Starten

- [ ] **ITEM-01**: Ein Klick auf ein Programm, eine Datei oder Verknüpfung startet sie; bei `.lnk` wird der Pfad der Verknüpfung gespeichert (Store-Apps bleiben funktionsfähig)
- [ ] **ITEM-02**: Ein URL-Eintrag öffnet die Adresse im Standardbrowser und zeigt ein Standardsymbol
- [ ] **ITEM-03**: Ein Ordner-Eintrag öffnet den Ordner im Explorer; Umgebungsvariablen in Pfaden werden aufgelöst
- [ ] **ITEM-04**: Ein schneller Doppelklick startet ein Programm nur einmal, und der Klick gibt eine sichtbare Rückmeldung
- [ ] **ITEM-05**: Schlägt ein Start fehl, erscheint eine kurze Meldung an der Leiste (kein Dialogfenster) und ein Eintrag im Log
- [ ] **ITEM-06**: Einträge mit fehlendem Ziel zeigen ein Ersatzsymbol statt zu verschwinden
- [ ] **ITEM-07**: Neue Einträge bekommen einen lesbaren Namen (Name der Verknüpfung bzw. Dateibeschreibung der .exe statt `programm.exe`)
- [ ] **ITEM-08**: Trennlinien lassen sich an der Mausposition einfügen, verschieben und löschen; sie zoomen nicht mit

### Drag & Drop und Hinzufügen

- [ ] **DND-01**: Dateien, Verknüpfungen und Ordner aus dem Explorer auf die Leiste ziehen hängt sie am Ende an; die Quelle wird weder verschoben noch kopiert (Q5)
- [ ] **DND-02**: „Eintrag hinzufügen…“ im Leisten-Kontextmenü fügt über eine Dateiauswahl einen Eintrag hinzu (Q17)
- [ ] **DND-03**: Beim Ziehen über die Leiste zeigt eine Einfügemarke die Zielposition, und der Zoom bleibt aktiv; Ablegen fügt dort ein
- [ ] **DND-04**: Einträge lassen sich per Ziehen umsortieren; ein normaler Klick startet weiterhin (Mindestbewegung)
- [ ] **DND-05**: Eine URL aus dem Browser auf die Leiste ziehen erzeugt einen URL-Eintrag
- [ ] **DND-06**: Ablegen auf ein vorhandenes Symbol fügt neben ihm ein (kein „Öffnen mit“) (Q20)

### Menüs und Eigenschaften

- [ ] **MENU-01**: Rechtsklick auf einen Eintrag: Starten · Als Administrator starten · Dateipfad öffnen · Eigenschaften · Entfernen
- [ ] **MENU-02**: Rechtsklick auf die Leiste: Trennlinie einfügen · Eintrag hinzufügen… · Konfiguration öffnen · Beenden
- [ ] **MENU-03**: Kontextmenüs schließen sich zuverlässig beim Klick daneben oder mit Escape, obwohl die Leiste nicht aktivierbar ist
- [ ] **MENU-04**: Der Eigenschaften-Dialog ändert Name, Pfad, Argumente, Arbeitsverzeichnis und eigenes Symbol (PNG/ICO)

### Skin

- [ ] **SKIN-01**: Der Skin CrystalXP.net wird wie im Original dargestellt (9-Slice-Hintergrund mit Rändern und Outside-Margins aus der INI)
- [ ] **SKIN-02**: Trennlinien werden mit dem Trennlinienbild des Skins gezeichnet
- [ ] **SKIN-03**: Skins werden aus `Skins\` neben `MehrBar.exe` geladen und sind im Konfigurationsfenster wählbar (Q14)
- [ ] **SKIN-04**: Fehlt der Skin oder ist er defekt, erscheint ein einfacher halbtransparenter Standard-Hintergrund ohne Fehlermeldung, mit Hinweis im Log

### Konfiguration

- [ ] **CONF-01**: Die Konfiguration liegt als JSON unter `%APPDATA%\MehrBar\` und übersteht Neustart der App und von Windows
- [ ] **CONF-02**: Vor jedem Speichern entsteht ein Backup, geschrieben wird atomar; unbekannte Felder und Eintragstypen bleiben beim Speichern erhalten
- [ ] **CONF-03**: Das Konfigurationsfenster ändert Position, Symbolgrößen, Zoom, Abstand, Beschriftung und Skin der Leiste — jede Änderung wirkt sofort live und wird automatisch gespeichert
- [ ] **CONF-04**: „Zurücksetzen auf Standardwerte“ stellt die Werte aus Q2 wieder her
- [ ] **CONF-05**: „Mit Windows starten“ lässt sich ein- und ausschalten und funktioniert nach einem Windows-Neustart

### App

- [ ] **APP-01**: Das Tray-Symbol bietet Konfiguration · Leiste ein-/ausblenden · Leiste nach vorn · Beenden; Doppelklick öffnet die Konfiguration; es übersteht einen Explorer-Neustart (Q15)
- [ ] **APP-02**: Es läuft nur eine Instanz; ein zweiter Start öffnet stattdessen die Konfiguration der laufenden
- [ ] **APP-03**: Die App ist eine einzelne .exe (plus `Skins\`) und zeigt die Leiste in unter 1 Sekunde
- [ ] **APP-04**: Die App ist Per-Monitor-DPI-aware; bei 100 % ist alles scharf, Stichprobe bei 125/150 % sieht sauber aus (Q11)
- [ ] **APP-05**: Ein Log unter `%APPDATA%\MehrBar\logs\` mit einfacher Rotation hält Fehler fest
- [ ] **APP-06**: Datenmodell und Layout tragen mehrere Leisten, auch mehrere pro Rand, an allen vier Rändern; Zoom und Layout sind achsenneutral (per Test über alle vier Ränder belegt), damit die rechte Leiste ohne Umbau kommt (Q6, Q7)

## v2 Requirements

### Leisten

- **BARS-01**: Zweite Leiste am rechten Rand — der eigentliche Anlass (Q6)
- **BARS-02**: Senkrechte Leisten links/rechts mit gedrehtem bzw. senkrechtem Skin und seitlicher Beschriftung
- **BARS-03**: Leisten im Konfigurationsfenster anlegen, löschen, duplizieren; „Neue Leiste“/„Leiste löschen“ im Kontextmenü (Q7)
- **BARS-04**: Einträge zwischen Leisten verschieben
- **BARS-05**: Tastenkürzel zum Hervorholen (RocketDock: Strg+Alt+R)

### Weiteres

- **GRUP-01**: Gruppen / Unterleisten, verschachtelt, mit Drag & Drop hinein (Q8)
- **HIDE-01**: Autohide mit Verzögerung (Q9)
- **LAYR-01**: Fensterebenen `normal` und `top` (Q10)
- **LABL-03**: Kontur-/Schattenfarbe der Beschriftung, Start-Effekt (Hüpfen) (Q3)
- **MULT-01**: Mehrere Monitore, Bildschirmplatz reservieren (AppBar)
- **OVFL-01**: Leisten breiter als der Bildschirm sinnvoll behandeln (Q18)
- **IMPT-01**: Import der RocketDock-Einstellungen (Q4)
- **REL-01**: Eigener Standard-Skin, mehrere Skins, Skin-Editor, Mehrsprachigkeit, Installer (Q1, Q2)

## Out of Scope

| Feature | Reason |
|---------|--------|
| Anzeige laufender Programme, Fenster-Vorschau, Taskleisten-Ersatz | MehrBar startet nur |
| Widgets / Docklets | Ausdrücklich nicht |
| Eigene Prozent-Skalierung je Leiste | Grund-/Zoomgröße direkt einstellbar, DPI übernimmt Windows (Q11) |
| Eintrag durch Herausziehen löschen | Kein Rückgängig, versehentliches Löschen; Entfernen per Kontextmenü |
| Ablegen auf Ordner-Eintrag kopiert hinein | Überraschende Dateioperation |
| Favicons von URLs laden | Netzzugriff ohne Nutzen; Standardsymbol reicht |
| Modale Fehlerdialoge | Stören; Meldung an der Leiste + Log |
| Virtuelle Desktops | Werden nicht genutzt (Q19) |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| LEIS-01 | Phase 2 | Pending |
| LEIS-02 | Phase 2 | Pending |
| LEIS-03 | Phase 2 | Pending |
| LEIS-04 | Phase 2 | Pending |
| LEIS-05 | Phase 2 | Pending |
| LEIS-06 | Phase 2 | Pending |
| LEIS-07 | Phase 2 | Pending |
| ZOOM-01 | Phase 3 | Pending |
| ZOOM-02 | Phase 3 | Pending |
| ZOOM-03 | Phase 3 | Pending |
| ZOOM-04 | Phase 3 | Pending |
| ZOOM-05 | Phase 3 | Pending |
| ZOOM-06 | Phase 3 | Pending |
| ZOOM-07 | Phase 3 | Pending |
| ZOOM-08 | Phase 3 | Pending |
| LABL-01 | Phase 3 | Pending |
| LABL-02 | Phase 3 | Pending |
| ITEM-01 | Phase 2 | Pending |
| ITEM-02 | Phase 5 | Pending |
| ITEM-03 | Phase 2 | Pending |
| ITEM-04 | Phase 3 | Pending |
| ITEM-05 | Phase 7 | Pending |
| ITEM-06 | Phase 7 | Pending |
| ITEM-07 | Phase 3 | Pending |
| ITEM-08 | Phase 5 | Pending |
| DND-01 | Phase 2 | Pending |
| DND-02 | Phase 2 | Pending |
| DND-03 | Phase 5 | Pending |
| DND-04 | Phase 5 | Pending |
| DND-05 | Phase 5 | Pending |
| DND-06 | Phase 5 | Pending |
| MENU-01 | Phase 5 | Pending |
| MENU-02 | Phase 5 | Pending |
| MENU-03 | Phase 2 | Pending |
| MENU-04 | Phase 5 | Pending |
| SKIN-01 | Phase 4 | Pending |
| SKIN-02 | Phase 4 | Pending |
| SKIN-03 | Phase 6 | Pending |
| SKIN-04 | Phase 4 | Pending |
| CONF-01 | Phase 2 | Pending |
| CONF-02 | Phase 2 | Pending |
| CONF-03 | Phase 6 | Pending |
| CONF-04 | Phase 6 | Pending |
| CONF-05 | Phase 6 | Pending |
| APP-01 | Phase 2 | Pending |
| APP-02 | Phase 6 | Pending |
| APP-03 | Phase 7 | Pending |
| APP-04 | Phase 7 | Pending |
| APP-05 | Phase 2 | Pending |
| APP-06 | Phase 3 | Pending |

**Coverage:**
- v1 requirements: 50 total
- Mapped to phases: 50
- Unmapped: 0 ✓
- Phase 1 (Risiko-Spikes) trägt bewusst keine Anforderung; sie senkt das Risiko für Phase 2 und 3

---
*Requirements defined: 2026-10-09*
*Last updated: 2026-10-09 after roadmap creation (traceability)*
