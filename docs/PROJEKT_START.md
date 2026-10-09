# Projekt: MehrBar

Ein schlanker Programmstarter für Windows im Stil von RocketDock: Leisten mit Zoom-Effekt, Beschriftung, Trennlinien, Drag & Drop, RocketDock-Skin-Unterstützung und eine kleine Konfigurationsoberfläche.

> **Stand:** Konzept nach der Grill-Session vom 2026-10-09 bereinigt. Die Begründung jeder Entscheidung steht im Protokoll am Ende dieser Datei (Q1–Q15); Verweise wie „(Q7)“ zeigen dorthin.

---

## 1. Ziel

- Programme platzsparend in Leisten am Bildschirmrand unterbringen.
- Der **Zoom-Effekt beim Überfahren mit der Maus** ist zentral: Symbole sind im Ruhezustand klein und werden unter dem Mauszeiger groß — Verhalten und Optik wie RocketDock mit dem Skin CrystalXP.net.
- Die App startet **nur** Programme, Dateien, Ordner und Links. Keine Anzeige laufender Programme, keine Taskleisten-Funktion.
- **Version 1 ist für den Eigengebrauch** des Entwicklers (Q1). Die Architektur wird trotzdem releasefähig gebaut (Core/App-Trennung, Ressourcen-Dateien, versionierte Konfiguration).
- **Der eigentliche Anlass für MehrBar ist eine zweite Leiste am rechten Rand** (Q6). Sie kommt nach V1 — die Architektur muss sie aber von Anfang an tragen: Zoom und Layout sind **achsenneutral**, die Ausrichtung ist ein Parameter.

---

## 2. Umfang Version 1

### Muss
1. **Eine Leiste, oben mittig** (Q7). Das Datenmodell trägt aber mehrere Leisten, auch mehrere pro Rand.
2. **Position:** Ausrichtung am Rand (Anfang, Mitte, Ende), Versatz entlang des Rands (`offset`) und Abstand zum Bildschirmrand (`edgeOffset`, darf negativ sein) (Q7).
3. **Fensterebene „immer im Hintergrund“** (Q9, Q10): Die Leiste liegt ganz unten in der Fensterreihenfolge, direkt über dem Desktop; jedes Fenster überdeckt sie. Ein Klick auf die Leiste holt sie nicht nach vorn und stiehlt keinen Fokus. Sie übersteht Win+D.
4. **Zoom-Effekt** (Abschnitt 6): Grundgröße, Zoomgröße, Zoom-Breite, Zoom-Dauer; Leiste wächst nur in der Breite, Mitte bleibt fest (Q12); flüssig (60 fps, keine Ruckler bei 60+ Symbolen).
5. **Beschriftung** (Q3): Das Symbol unter der Maus zeigt seinen Namen auf der vom Rand abgewandten Seite — weiße Schrift mit Kontur und leichtem Schatten. Ein/aus, Schriftart, Schriftgröße einstellbar.
6. **Eintragstypen:** Programm / Datei / Verknüpfung (.exe, .lnk, beliebige Datei), URL, Ordner (öffnet im Explorer), **Trennlinie**. Typen polymorph, damit `group` später ohne Migration dazukommt (Q8).
7. **Erster Start:** Ohne `config.json` legt MehrBar eine **leere Leiste oben mittig** mit den Standardwerten an (Q4). Befüllt wird per Drag & Drop. Kein Import aus RocketDock.
8. **Drag & Drop:**
   - Meilenstein 1: Dateien, Verknüpfungen, Ordner aus dem Explorer auf die Leiste ziehen → wird am Ende angehängt (Q5).
   - Meilenstein 4: Einfügemarke an der Drop-Position, Umsortieren innerhalb der Leiste, URLs aus dem Browser.
9. **Kontextmenü pro Eintrag** (Q13): Starten · Als Administrator starten · Dateipfad öffnen · Eigenschaften · Entfernen.
10. **Kontextmenü der Leiste** (Q13): Trennlinie einfügen · Konfiguration öffnen · Beenden.
11. **Eigenschaften-Dialog:** Name, Pfad, Argumente, Arbeitsverzeichnis, eigenes Symbol (PNG/ICO).
12. **Symbole** aus der Datei in hoher Auflösung (bis 256 px), damit der Zoom scharf bleibt; eigenes PNG/ICO pro Eintrag möglich.
13. **Skin:** RocketDock-Skin einlesen, Referenz ist **CrystalXP.net** (Q2). Skin-Ordner ist `Skins\` neben `MehrBar.exe` (Q14). Der Loader ist allgemein geschrieben.
14. **Konfigurationsfenster** für die eine Leiste (Q14): Position, Symbole, Beschriftung, Skin, Mit Windows starten. Jede Änderung wirkt **sofort live** und wird automatisch gespeichert; „Zurücksetzen auf Standardwerte“.
15. **Tray-Symbol** als Notausgang (Q15): Konfiguration · Leiste ein-/ausblenden · Beenden; Doppelklick öffnet die Konfiguration.
16. **Konfiguration als JSON** im Benutzerprofil, Änderungen sofort übernommen und gespeichert.
17. **Nur eine Instanz** gleichzeitig.
18. **Per-Monitor-DPI-Awareness (v2)** statt eigener Prozent-Skalierung (Q11).

### Standardwerte einer neuen Leiste (Q2)
Übernommen aus der RocketDock-Einstellung des Entwicklers, im Konfigurationsfenster änderbar:

| Wert | Standard |
|---|---|
| Rand / Ausrichtung | oben / Mitte |
| `edgeOffset` | −5 px |
| Grundgröße | 32 px |
| Zoomgröße | 121 px |
| Zoom-Breite | 4 (Gesamtbreite der Zoomzone in Symbolen, RocketDock-Bedeutung — siehe Abschnitt 6) |
| Zoom-Dauer | 200 ms |
| Beschriftung | an, Tahoma, 13 px |
| Skin | CrystalXP.net |

### Später (V2+)
Nach Priorität:
1. **Zweite Leiste am rechten Rand** — der eigentliche Anlass für die App (Q6). Mit ihr: senkrechte Leisten links/rechts, gedrehter bzw. senkrechter Skin, Beschriftung seitlich.
2. Mehrere Leisten im Konfigurationsfenster anlegen/löschen/duplizieren; „Neue Leiste“ / „Leiste löschen“ im Kontextmenü (Q7).
3. **Gruppen / Unterleisten**, verschachtelt, Drag & Drop hinein, Auto-Aufklappen beim Darüberziehen (Q8).
4. Einträge zwischen Leisten verschieben.
5. **Autohide** mit Verzögerung (Q9).
6. Fensterebenen `normal` und `top` (Q10).
7. Kontur-/Schattenfarbe der Beschriftung, Start-Effekt (Hüpfen) (Q3).
8. Mehrere Monitore (Monitor-ID steht schon im Datenmodell); Bildschirmplatz reservieren (AppBar).
9. Import der RocketDock-Einstellungen (Q4).
10. Mehrere Skins, Skin-Editor, eigener Standard-Skin für ein Release (Q2).
11. Mehrsprachigkeit, Tastenkürzel zum Einblenden, portabler Modus, Installer.

### Ausdrücklich nicht
- Anzeige laufender Programme, Fenster-Vorschau, Taskleisten-Ersatz
- Widgets / Docklets

---

## 3. Technik

| Thema | Entscheidung |
|---|---|
| Sprache / Framework | C# mit **.NET 10 (LTS)**, **WPF** |
| Zielsystem | Windows 10 und 11, x64; Arbeitsplatz des Entwicklers mit 100 % Windows-Skalierung |
| Ausgabe | Einzelne .exe (self-contained, single-file Publish); `Skins\` als normaler Ordner daneben |
| Konfiguration | `System.Text.Json`, Datei unter `%APPDATA%\MehrBar\config.json` |
| Log | `%APPDATA%\MehrBar\logs\`, einfache Rotation |
| Symbole | Shell-API (`IShellItemImageFactory` bzw. `SHGetImageList` mit Jumbo-Größe) für hochauflösende Symbole, Cache im Speicher |
| Rendering der Leiste | Eigenes leichtgewichtiges Control, das Symbole selbst zeichnet (`DrawingVisual` / `OnRender`), **keine** ItemsControl mit vielen Einzel-Controls |
| Fenster | Rahmenlos, transparent (`AllowsTransparency`), nicht in Taskleiste/Alt+Tab (`WS_EX_TOOLWINDOW`), nicht aktivierbar (`WS_EX_NOACTIVATE`), Z-Reihenfolge „unten“ wird erzwungen |
| Lizenz für späteres Release | Vorschlag: MIT |

### Entwicklungsumgebung
- .NET 10 SDK (installiert: 10.0.401)
- VS Code mit **C# Dev Kit**
- Bauen: `dotnet build`, Starten: `dotnet run --project src/MehrBar.App`
- Release: `dotnet publish src/MehrBar.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`
- Der Build kopiert `samples/skins/` (sofern vorhanden) nach `Skins\` im Ausgabeordner.

---

## 4. Projektstruktur

```
MehrBar/
├─ MehrBar.sln
├─ CLAUDE.md
├─ README.md
├─ LICENSE
├─ docs/
│  ├─ PROJEKT_START.md           ← diese Datei
│  └─ skin-format.md             ← entsteht in Meilenstein 3 aus dem echten Skin
├─ samples/                      ← nur lokal, gitignored
│  ├─ skins/CrystalXP.net/       ← Referenz-Skin, Testdaten für den Parser
│  └─ reference/                 ← Screenshots des Originals (Zoom-Kalibrierung)
├─ src/
│  ├─ MehrBar.Core/              ← reine Logik, keine WPF-Abhängigkeit
│  │  ├─ Model/                  ← DockConfig, BarConfig, DockItem (+ Untertypen)
│  │  ├─ Config/                 ← Laden/Speichern, Versionierung, Migration
│  │  ├─ Skins/                  ← ISkin, RocketDockSkinLoader, SkinInfo
│  │  ├─ Layout/                 ← Zoom- und Positionsberechnung, achsenneutral (testbar!)
│  │  └─ Launch/                 ← ILauncher, Start von Programmen/URLs/Ordnern
│  ├─ MehrBar.App/               ← WPF-Anwendung
│  │  ├─ Bars/                   ← BarWindow, BarRenderer
│  │  ├─ Settings/               ← Konfigurationsfenster (MVVM)
│  │  ├─ Interop/                ← Win32-Aufrufe (Shell-Symbole, Fensterstile, Z-Reihenfolge, Monitore, DPI)
│  │  ├─ Tray/
│  │  └─ App.xaml(.cs)
│  └─ MehrBar.Tests/             ← xUnit-Tests für Core (Zoom, Layout, Config, Skin-Parser)
└─ .editorconfig
```

**Grundregel:** Alles, was ohne Windows-Oberfläche testbar ist (Zoom-Mathematik, Layout, Konfiguration, Skin-Parser), liegt in `MehrBar.Core`. Die App-Schicht zeichnet nur und leitet Eingaben weiter. Win32-Aufrufe ausschließlich in `App/Interop`.

---

## 5. Datenmodell und Konfiguration

Beispiel `config.json`:

```json
{
  "version": 1,
  "general": {
    "startWithWindows": true,
    "skinFolder": "Skins"
  },
  "bars": [
    {
      "id": "b1",
      "name": "Oben",
      "monitor": "primary",
      "edge": "top",
      "align": "center",
      "offset": 0,
      "edgeOffset": -5,
      "layer": "bottom",
      "iconSize": 32,
      "zoomSize": 121,
      "zoomWidth": 4,
      "zoomDurationMs": 200,
      "zoomAnchor": "center",
      "spacing": 4,
      "label": { "show": true, "fontFamily": "Tahoma", "fontSize": 13 },
      "skin": "CrystalXP.net",
      "items": [
        { "type": "app", "name": "Firefox", "path": "C:\\Program Files\\Mozilla Firefox\\firefox.exe" },
        { "type": "app", "name": "Notizen", "path": "C:\\Tools\\notes.lnk", "icon": "C:\\Icons\\notes.png" },
        { "type": "separator" },
        { "type": "url", "name": "Wetter", "url": "https://www.dwd.de" },
        { "type": "folder", "name": "Downloads", "path": "%USERPROFILE%\\Downloads" }
      ]
    }
  ]
}
```

Anforderungen:
- Feld `version` für spätere Migrationen.
- `skinFolder` relativ ist relativ zum Programmordner (Q14).
- `edge` kennt `top`, `bottom`, `left`, `right` — V1 nutzt nur `top`. Mehrere Leisten pro Rand sind erlaubt (Q7).
- `layer` kennt in V1 nur `bottom` (Q10); `zoomAnchor` kennt `center` (V1) und `cursor` (Q12).
- Umgebungsvariablen in Pfaden (`%USERPROFILE%` usw.) werden aufgelöst.
- Unbekannte Felder beim Laden nicht verwerfen (Vorwärtskompatibilität).
- Vor jedem Speichern Backup `config.bak.json`; Schreiben über temporäre Datei + Umbenennen, damit die Datei nie halb geschrieben ist.
- Eintragstypen polymorph über `type` (System.Text.Json Polymorphie); neue Typen wie `group` ohne Umbau ergänzbar.

---

## 6. Zoom-Effekt (Kernfunktion)

### Verhalten
- Ohne Maus: alle Symbole in Grundgröße.
- Maus über der Leiste: Jedes Symbol bekommt eine Größe abhängig vom Abstand seiner Mitte zum Mauszeiger entlang der Leistenachse. Das Symbol unter der Maus ist am größten, Nachbarn werden stufenlos kleiner.
- Beim Verlassen der Leiste zoomen alle Symbole animiert auf Grundgröße zurück.
- Symbole wachsen **vom Bildschirmrand weg** (Leiste oben → nach unten). Der **Hintergrund behält seine Höhe**; gezoomte Symbole ragen über ihn hinaus (Q2).
- **Verankerung `center`** (Q12): Die Leiste wächst nur in der Breite, um genau den Mehrplatz der gezoomten Symbole, und zwar **zu beiden Seiten gleich — die Leistenmitte bleibt fest**. Die übrigen Symbole behalten ihre Grundgröße und werden nur verschoben. Am Original gemessen (Protokoll, Nachtrag zu Q12): Mitte bleibt bis auf ±40 px fest.
- Trennlinien zoomen nicht mit, nur ihr Abstand wird mitgeschoben.
- Ein Klick trifft das Symbol, das **sichtbar** unter dem Zeiger gezeichnet ist.

### Berechnung (in `Core/Layout`, mit Tests, achsenneutral)
- Abstand `d` = |Mausposition − Symbolmitte **im Ruhezustand**| in Einheiten von Symbolbreiten (aus den Ruhepositionen, sonst schaukelt sich die Verschiebung auf).
- Radius `r` aus der Zoom-Breite abgeleitet: `zoomWidth` meint wie bei RocketDock die **Gesamtbreite** der Zoomzone. Ausgangspunkt `r ≈ 2,5` für `zoomWidth = 4`.
- Wenn `d < r`: `f = (cos(π · d / r) + 1) / 2`, sonst `f = 0`. Größe = `iconSize + (zoomSize − iconSize) · f`.
- **Kalibrierung in Meilenstein 2** gegen `samples/reference/`: gezoomt 121 px, direkte Nachbarn ca. 90–95 px, übernächste nahe Grundgröße.
- Verankerung als Parameter (`center` jetzt, `cursor` = Symbol unter dem Zeiger bleibt fest), damit nach dem Ausprobieren ohne Umbau gewechselt werden kann.

### Technische Umsetzung
- Das Leistenfenster ist so groß wie die **maximal gezoomte** Leiste samt Beschriftung, der Rest ist transparent.
- **Transparente Bereiche müssen Klicks durchlassen.** Zu prüfen: Layered Windows lassen Klicks auf vollständig transparente Pixel von selbst durch; sonst Hit-Region oder `WS_EX_TRANSPARENT` dynamisch umschalten.
- Animation über `CompositionTarget.Rendering`, Größen pro Frame interpolieren; im Ruhezustand keine Frames.
- Symbole einmal in der höchsten benötigten Auflösung laden und nur skaliert zeichnen (hochwertiges Bitmap-Scaling).

---

## 7. RocketDock-Skin

Ein RocketDock-Skin ist ein Ordner mit PNG-Grafiken und INI-Dateien. CrystalXP.net enthält:
- `background.ini` → `[Background]` mit `Image = bg.png`, `LeftMargin/TopMargin/RightMargin/BottomMargin = 8` und `Outside-LeftMargin = 20`, `Outside-TopMargin = 13`, `Outside-RightMargin = 23`, `Outside-BottomMargin = 10`
- `separator.ini` → `[Separator]` mit `Image = sep.png`, `TopMargin = 0`, `BottomMargin = 0`

**Vorgehen:**
1. Bedeutung der Felder (insbesondere `Outside-*Margin`) am echten Skin klären und in `docs/skin-format.md` dokumentieren. Nicht auf Annahmen bauen.
2. Hintergrund per **9-Slice-Skalierung** (Ecken fest, Kanten und Mitte gestreckt), Ränder aus der INI.
3. Hintergrund folgt beim Zoom der aktuellen Leistenbreite, nicht der Höhe.
4. Fehlt ein Skin oder ist er defekt: einfacher halbtransparenter Standard-Hintergrund, keine Fehlermeldung beim Start, Hinweis im Log.

**Rechte:** Fremde Skins werden nicht mitgeliefert und nicht ins Repo eingecheckt. Vor einem Release werden die Rechte geklärt oder eigene, ähnlich aussehende Skins erstellt (Q2).

---

## 8. Drag & Drop und Trennlinien

- `.lnk`-Dateien: Ziel, Argumente, Arbeitsverzeichnis und Symbol auslesen, aber den Pfad der .lnk speichern (so bleiben Verknüpfungen von Store-Apps funktionsfähig).
- Meilenstein 1: Ablegen aus dem Explorer hängt am Ende an.
- Meilenstein 4: Einfügemarke zeigt die Zielposition, der Zoom bleibt beim Ziehen aktiv. Gezogen werden können Dateien, Verknüpfungen, Ordner, URLs aus dem Browser. Umsortieren per Ziehen mit der linken Maustaste (mit kleiner Mindestbewegung, damit ein normaler Klick weiter startet).
- Trennlinie über das Leisten-Kontextmenü an der Mausposition einfügen; Trennlinien lassen sich wie Symbole verschieben und löschen.

---

## 9. DPI und Monitor

- Die App ist **Per-Monitor-DPI-aware (v2)**; Windows-Skalierung wirkt auf Symbole, Abstände und Skin. Keine eigene Prozent-Skalierung (Q11).
- V1: Hauptmonitor. Die Monitorauswahl ist im Datenmodell pro Leiste vorhanden.
- Reagieren auf Änderung der Auflösung oder Monitoranordnung: Leiste neu positionieren.

---

## 10. Meilensteine

1. **Grundgerüst:** Solution und Projekte, Konfiguration laden/speichern (Backup, atomares Schreiben), nur eine Instanz, Tray-Symbol, Log. Erster Start legt eine leere Leiste oben mittig an. Leiste mit Fensterebene „unten“, nicht aktivierbar; Symbole in Grundgröße mit hochauflösenden Shell-Symbolen; Klick startet; Hineinziehen aus dem Explorer (anhängen) und „Entfernen“ im Kontextmenü.
2. **Zoom:** Zoom-Berechnung in Core (achsenneutral, Verankerung `center`) mit Unit-Tests, Kalibrierung gegen die Referenz-Screenshots, flüssige Animation, Beschriftung, Klick-Durchlass für transparente Bereiche.
3. **Skin:** `docs/skin-format.md`, RocketDock-Skin-Loader, 9-Slice-Hintergrund mit Outside-Margins, Trennlinien (einfügen, zeichnen, löschen), Standard-Hintergrund als Rückfall, Kopie von `samples/skins/` in den Ausgabeordner.
4. **Drag & Drop und Menüs:** Einfügemarke, Umsortieren, URLs aus dem Browser, Trennlinien verschieben, vollständige Kontextmenüs, Eigenschaften-Dialog, eigene Symbole.
5. **Konfigurationsfenster:** alle Leisten-Einstellungen live, Zurücksetzen auf Standardwerte, Mit Windows starten.
6. **Feinschliff:** Performance mit 100+ Symbolen, Robustheit der Fensterebene (Win+D, Explorer-Neustart, Auflösungswechsel), Fehlerbehandlung und unaufdringliche Startfehler-Meldung, Log-Rotation, Release-Build.

Nach jedem Meilenstein: App muss bauen, starten und das bisher Erreichte nutzbar sein.

---

## 11. Abnahmekriterien Version 1

- [ ] Eine Leiste oben mittig; das Datenmodell trägt mehrere Leisten pro Rand
- [ ] Erster Start ohne Konfiguration zeigt eine leere Leiste; Datei aus dem Explorer daraufziehen erzeugt einen funktionierenden Eintrag
- [ ] Zoom läuft flüssig mit 60+ Symbolen auf einer Leiste
- [ ] Beim Zoom wächst die Leiste nur in der Breite, zu beiden Seiten gleich; die Mitte bleibt fest; nichts ruckelt oder flackert
- [ ] Zoom-Abfall entspricht sichtbar dem Original (Referenz-Screenshots)
- [ ] Das Symbol unter der Maus zeigt seine Beschriftung
- [ ] Die Leiste liegt immer hinter allen Fenstern, kommt beim Klick nicht nach vorn, stiehlt keinen Fokus und übersteht Win+D
- [ ] Klicks neben die sichtbare Leiste erreichen den Desktop bzw. die darunterliegenden Fenster
- [ ] CrystalXP.net wird korrekt dargestellt
- [ ] Trennlinien einfügen, verschieben, löschen
- [ ] Symbole sind beim Zoom scharf (100 %; Stichprobe 125/150 % Windows-Skalierung)
- [ ] Konfigurationsfenster ändert die Leiste live
- [ ] Konfiguration übersteht Neustart der App und von Windows
- [ ] App startet in unter 1 Sekunde und braucht im Ruhezustand kaum CPU

---

## 12. Hinweise für die Umsetzung

- Kleine, nachvollziehbare Schritte; nach jedem Schritt bauen und starten.
- Win32-Aufrufe ausschließlich in `App/Interop` kapseln.
- Keine schweren Fremdbibliotheken; falls nötig, nur gut gepflegte NuGet-Pakete mit kompatibler Lizenz.
- Fehler beim Starten eines Eintrags (Datei fehlt usw.) als kurze, unaufdringliche Meldung anzeigen, nicht abstürzen.
- Code, Kommentare und Commit-Nachrichten auf Englisch; Oberfläche zunächst Deutsch, Texte aber von Anfang an in Ressourcen-Dateien.

---

# Protokoll

## Grill Session — 2026-10-09

### Q1: Zielgruppe Version 1
**Question:** Ist Version 1 für den Eigengebrauch oder schon für ein öffentliches Release gedacht?
**Recommended:** Eigengebrauch; Architektur trotzdem releasefähig (Core/App-Trennung, Ressourcen-Dateien, Config-Version). Release-Themen (eigener Standard-Skin, Installer, englische UI, Mehrmonitor-UI) nach V2.
**Answer:** V1 nur für mich.
**Follow-ups:** Release-Themen wandern nach V2.

### Q2: Referenz-Skin und Skin-Quelle
**Question:** Welcher Skin ist der Referenz-Skin für V1, und liest MehrBar zusätzlich den RocketDock-Skin-Ordner?
**Recommended:** CrystalXP.net als Referenz; Skin-Ordner `%APPDATA%\MehrBar\Skins`, zusätzlich RocketDock-Ordner als zweite Quelle; Kopie in `samples/skins/` (gitignored) als Testdaten.
**Answer:** CrystalXP.net ist richtig. Den RocketDock-Ordner **nicht** lesen. Rechte werden vor einem Release geklärt — oder es entstehen eigene, ähnlich aussehende Skin-Dateien. Die übernommenen Referenzwerte sind nur Standardwerte und später in den Einstellungen bearbeitbar.
**Follow-ups:**
- Referenz-Einstellung aus RocketDock (Registry, Screenshot 2026-10-09): `IconMin=32`, `IconMax=121`, `ZoomWidth=4`, `ZoomTicks=200` ms, `vOffset=-5` (Leiste leicht über den oberen Rand geschoben), Beschriftung des gezoomten Symbols sichtbar (Tahoma, weiß mit Kontur/Schatten).
- Skin-Format CrystalXP.net: `background.ini` mit `LeftMargin/TopMargin/RightMargin/BottomMargin = 8` und `Outside-*Margin` (20/13/23/10), `separator.ini` mit `Image`, `TopMargin`, `BottomMargin`.
- Beobachtung: Der Hintergrund behält beim Zoom seine Höhe; gezoomte Symbole ragen nach unten über ihn hinaus.

### Q3: Beschriftung unter dem gezoomten Symbol
**Question:** Kommt die Beschriftung (Name unter dem gezoomten Symbol, wie im RocketDock-Screenshot) in V1? Im ursprünglichen Konzept fehlt sie.
**Recommended:** Ja, Muss in V1. Nur das Symbol unter der Maus, auf der vom Rand abgewandten Seite, weiße Schrift mit Kontur und leichtem Schatten; je Leiste ein/aus, Schriftart, Schriftgröße (Standard Tahoma 13 px). Kontur-/Schattenfarbe und Start-Effekt (Hüpfen) nach V2.
**Answer:** Ja — immer das Symbol, auf dem die Maus sitzt, zeigt die Schrift. Rest wie empfohlen.

### Q4: Import der RocketDock-Einträge
**Question:** Sollen die 19 RocketDock-Einträge (Registry `HKCU\Software\RocketDock\Icons`) in V1 beim ersten Start importiert werden?
**Recommended:** Ja, einmaliger Import in Meilenstein 1 (ca. ½ Tag), echte Testdaten ab Tag 1.
**Answer:** Nein. Kein Import. MehrBar legt beim ersten Start oben eine leere Leiste an; die benötigten Symbole werden per Drag & Drop aus dem Explorer daraufgezogen (ca. 5 Minuten Handarbeit, spart unnötigen Code). Import bleibt unter „Kann (später)“.
**Follow-ups:** Erste-Start-Verhalten: leere Leiste am oberen Rand mit den Referenzwerten aus Q2. → Drag & Drop wird damit früher gebraucht als in Meilenstein 4 (siehe Q5).

### Q5: Zeitpunkt für Drag & Drop aus dem Explorer
**Question:** Ohne Import ist die Leiste anfangs leer — ab wann muss Hineinziehen aus dem Explorer funktionieren (laut Konzept erst Meilenstein 4)?
**Recommended:** Aufteilen. Meilenstein 1: Dateien/Verknüpfungen/Ordner aus dem Explorer auf die Leiste ziehen (anhängen am Ende) + „Entfernen“ im Kontextmenü. Meilenstein 4 wie geplant: Einfügemarke, Umsortieren, Verschieben zwischen Leisten/in Gruppen, URLs aus dem Browser, Eigenschaften-Dialog.
**Answer:** Ja, passt so.
**Follow-ups:** Meilenstein 1 in Abschnitt 11 wird um „einfaches Hineinziehen + Entfernen“ erweitert.

### Q6: Ränder und senkrechte Leisten
**Question:** Welche Leisten werden wirklich genutzt, und braucht V1 schon Leisten links/rechts (gedrehter Skin, eigene Beschriftung, seitliches Aufklappen)?
**Recommended:** V1 oben und unten; links/rechts im Datenmodell vorgesehen, Umsetzung V2.
**Answer:** V1 nur oben. Eine zweite Leiste **rechts** ist der eigentliche Grund für die App — kommt aber später.
**Follow-ups:**
- Weil die rechte Leiste das Kernziel ist, muss die Architektur von Anfang an achsenneutral sein: Zoom- und Layout-Berechnung in Core rechnen entlang einer abstrakten Leistenachse, der Renderer kennt die Ausrichtung als Parameter. Kein „horizontal“ fest im Code.
- Abnahmekriterium „3 Leisten an verschiedenen Rändern“ entfällt für V1 (siehe Q7).

### Q7: Anzahl der Leisten in V1
**Question:** „Nur oben“ — eine Leiste oben oder mehrere oben nebeneinander?
**Recommended:** Eine Leiste in V1; Datenmodell bleibt eine Liste; Meilenstein 6 schrumpft auf das Konfigurationsfenster für diese Leiste; rechte Leiste als erster Punkt nach V1.
**Answer:** V1: **eine Leiste, oben mittig**. Das Datenmodell muss aber vorsehen, dass pro Rand mehrere Leisten positioniert werden können — auch je eine oben und unten, jeweils mit Abstand zum Bildschirmrand.
**Follow-ups:**
- Je Leiste zwei getrennte Versätze: `offset` entlang des Rands (zur Ausrichtung Anfang/Mitte/Ende) und **`edgeOffset` senkrecht zum Rand** (Abstand zum Bildschirmrand, darf negativ sein — entspricht RocketDocks `vOffset = -5`).
- Abnahmekriterium „mindestens 3 Leisten an verschiedenen Rändern“ entfällt für V1; ersetzt durch „eine Leiste oben mittig; Datenmodell trägt mehrere Leisten pro Rand“.
- Meilenstein 6 wird „Konfigurationsfenster für die eine Leiste“; Anlegen/Löschen/Duplizieren von Leisten und „Neue Leiste“/„Leiste löschen“ im Kontextmenü nach V2.

### Q8: Gruppen (Unterleisten) in V1
**Question:** Gehören Gruppen/Unterleisten (verschachtelt, aufklappend, Drag & Drop hinein) in V1, oder reicht zunächst eine flache Leiste?
**Recommended:** Schlank in V1: Klick öffnet Unterleiste, Verschachtelung rekursiv, Ablegen auf dem Gruppensymbol hängt an; Auto-Aufklappen beim Ziehen nach V2.
**Answer:** Nein, nicht in V1 — kommt später.
**Follow-ups:**
- Meilenstein 5 (Gruppen) entfällt für V1; Abnahmekriterium „Gruppen mit 2 Ebenen“ entfällt.
- Kontextmenü „Gruppe einfügen“ entfällt in V1.
- Die polymorphe Typ-Struktur der Einträge (`type`) bleibt so angelegt, dass `group` später ohne Migration ergänzt werden kann.

### Q9: Autohide in V1
**Question:** Autohide ist im Konzept Muss, in RocketDock aber ausgeschaltet (`AutoHide = 0`). Gehört es in V1?
**Recommended:** Nach V2; V1 dauerhaft sichtbar, wahlweise immer im Vordergrund.
**Answer:** Nicht in V1. Die Leiste muss **immer im Hintergrund** stehen — hinter allen geöffneten Fenstern.
**Follow-ups:** „Immer im Vordergrund“ (`topmost`) ist damit nicht der V1-Fall, sondern das Gegenteil: Fensterebene „unten“. Genaue Bedeutung siehe Q10.

### Q10: Bedeutung von „immer im Hintergrund“
**Question:** Was genau heißt „immer im Hintergrund“?
**Recommended:** Leiste ganz unten in der Fensterreihenfolge, direkt über dem Desktop; jedes Fenster überdeckt sie. Klick auf die Leiste holt sie nicht nach vorn und stiehlt keinen Fokus. Erreichbar über den freigelegten Desktop (Win+D o. ä.), kein Hervorhol-Kürzel in V1. Datenmodell: Feld `layer` (`bottom` Standard und einziger V1-Wert; später `normal`, `top`); `topmost` entfällt.
**Answer:** Ja, genau so.
**Follow-ups:**
- Technisch: Leiste bleibt auch nach Klicks, Aktivierungen anderer Fenster und Win+D unten (Fensterstil ohne Aktivierung; Z-Reihenfolge wird bei jeder Positionsänderung erzwungen). Muss Win+D überleben — eigenes Abnahmekriterium.
- Konfigurationsfenster: Option „Immer im Vordergrund“ entfällt in V1.

### Q11: Skalierung in Prozent je Leiste
**Question:** Braucht V1 `scalePercent` je Leiste, obwohl Grund- und Zoomgröße ohnehin direkt einstellbar sind?
**Recommended:** `scalePercent` entfällt in V1. Pflicht bleibt Per-Monitor-DPI-Awareness (v2): Windows-Skalierung wirkt automatisch auf Symbole, Abstände und Skin, Symbole werden in passender Auflösung geladen. Abnahmekriterium „75/100/150 %“ bezieht sich auf die Windows-Skalierung.
**Answer:** Wie empfohlen. Arbeitsplatz läuft mit **100 %** Windows-Skalierung (Hauptfall für Tests; 125/150 % nur Stichprobe). Ergänzend zum Zoom: Im Original wächst die Leiste beim Zoom **nur in der Breite** — um genau den Mehrplatz, den die 3–5 gestuft vergrößerten Symbole unter der Maus brauchen. Die übrigen Symbole links und rechts behalten ihre Grundgröße und bleiben in der Reihe; die Höhe des Hintergrunds ändert sich nicht.
**Follow-ups:** Wohin genau die Leiste beim Wachsen ausweicht (Verankerung), siehe Q12.

### Q12: Verankerung beim Zoom
**Question:** Die Leiste steht oben mittig und wird beim Zoom breiter — wer weicht aus? A: Leistenmitte bleibt fest, beide Ränder wandern gleich weit nach außen. B: Symbol unter der Maus bleibt fest, die Leiste wächst ungleich je nach Mausposition.
**Recommended:** B (macOS-/RocketDock-Verhalten, Abnahmekriterium „Symbol unter dem Mauszeiger bleibt stabil“).
**Answer:** **A** — Leistenmitte bleibt fest, die Leiste wächst zu beiden Seiten gleich.
**Follow-ups:**
- Abnahmekriterium „Symbol unter dem Mauszeiger bleibt stabil unter dem Zeiger“ wird ersetzt durch: „Leistenmitte bleibt beim Zoom fest; die Leiste wächst nur in der Breite, zu beiden Seiten gleich; das Zoomen ruckelt und flackert nicht.“
- Die Zoom-Stärke je Symbol wird aus den **Ruhepositionen** berechnet (wie in Abschnitt 6), nicht aus den gezoomten — sonst schaukelt sich die Verschiebung auf.
- Risiko: Nahe den Leistenenden liegt das größte Symbol sichtbar etwas neben dem Zeiger. Treffer beim Klick = das Symbol, das **sichtbar** unter dem Zeiger gezeichnet ist.
- Die Verankerung wird in Core als Parameter gebaut (`center` jetzt, `cursor` = Variante B möglich), damit nach dem ersten Ausprobieren ohne Umbau gewechselt werden kann.

#### Nachtrag zu Q12: Messung am Original (2026-10-09)
Fünf Screenshots der RocketDock-Leiste (Ruhe; Maus auf mittigem Symbol; links; rechts; ganz rechts), über das identische Hintergrundbild pixelgenau zueinander ausgerichtet. Ablage lokal unter `samples/reference/` (gitignored).

| Zustand | linker Rand | rechter Rand | Breite | Mitte |
|---|---|---|---|---|
| Ruhe | 109 | 698 | 589 | 404 |
| Maus mittig | −3 | 812 | 815 | 404 |
| Maus links (2. Symbol) | 27 | 812 | 785 | 420 |
| Maus rechts (Publisher) | −3 | 804 | 807 | 400 |
| Maus ganz rechts (Excel) | −3 | 733 | 736 | 365 |

- **Bestätigt Variante A:** Die Mitte bleibt im Wesentlichen fest (±40 px), die Leiste wächst zu beiden Seiten.
- **Zoom-Abfall ist steiler als die Konzeptformel mit `zoomWidth = 4`:** Gezoomt 121 px, direkte Nachbarn ca. 90–95 px, übernächste fast Grundgröße (~40 px). Die cos-Formel aus Abschnitt 6 trifft das mit einem Radius von etwa 2,5 Symbolen. RocketDocks `ZoomWidth` meint offenbar die **Gesamtbreite** der Zoomzone, nicht den Radius. In Meilenstein 2 gegen diese Screenshots kalibrieren.
- Trennlinien behalten beim Zoom ihre Größe und werden nur verschoben (bestätigt Abschnitt 6).

### Q13: Kontextmenüs und Eigenschaften in V1
**Question:** Reicht dieser Umfang? Symbol: Starten · Als Administrator starten · Dateipfad öffnen · Eigenschaften · Entfernen. Leiste: Trennlinie einfügen · Konfiguration öffnen · Beenden. Eigenschaften-Dialog: Name, Pfad, Argumente, Arbeitsverzeichnis, eigenes Symbol (PNG/ICO).
**Recommended:** Alles in V1; Trennlinien werden laut Screenshot genutzt.
**Answer:** Ja, passt so.

### Q14: Umfang des Konfigurationsfensters in V1
**Question:** Reicht für die eine Leiste: Position (Ausrichtung, Versatz, `edgeOffset`), Symbole (Grund-/Zoomgröße, Zoom-Breite, Zoom-Dauer, Abstand), Beschriftung (ein/aus, Schriftart, -größe), Skin-Auswahl, Mit Windows starten?
**Recommended:** Schlichtes Fenster mit diesen fünf Gruppen; jede Änderung wirkt sofort live und wird automatisch gespeichert; „Zurücksetzen auf Standardwerte“ (Werte aus Q2).
**Answer:** Passt. Der Skin kommt fürs Erste einfach in den **Programmordner**, und dieser wird als Skin-Ordner verwendet.
**Follow-ups:**
- Skin-Ordner V1: `Skins\` neben `MehrBar.exe` (nicht `%APPDATA%`). Bei Single-File-Publish liegt `Skins\` als normaler Ordner neben der .exe, nicht eingebettet.
- Entwicklung: Der Build kopiert `samples/skins/` nach `Skins\` im Ausgabeordner, sofern vorhanden (der Ordner ist gitignored, fehlt also in fremden Klonen → Standard-Hintergrund).
- `general.skinFolder` bleibt im Datenmodell, Standardwert relativ zum Programmordner.

### Q15: Tray-Symbol in V1
**Question:** Braucht V1 ein Tray-Symbol, obwohl Konfiguration und Beenden über das Leisten-Kontextmenü erreichbar sind?
**Recommended:** Ja, minimal als Notausgang (Leiste verdeckt, aus dem Bild geschoben, Skin defekt): Konfiguration · Leiste ein-/ausblenden · Beenden; Doppelklick öffnet die Konfiguration.
**Answer:** Ja, klar.

### Q16: Abschluss und Bereinigung
**Question:** Grill abschließen, den Hauptteil des Konzepts an die Entscheidungen anpassen und einen ersten Commit machen, bevor `/gsd-new-project` läuft?
**Recommended:** Ja — sonst liest GSD widersprüchliche Stände.
**Answer:** Ja, passt.
**Follow-ups:** Hauptteil (Abschnitte 1–12) am 2026-10-09 bereinigt; Meilensteine sind jetzt Abschnitt 10 (Verweise auf „Abschnitt 11“ in Q5 meinen diesen). Offene technische Punkte für die Phasen-Recherche: Klick-Durchlass, Win+D-Robustheit, Zoom-Kalibrierung, Form der Startfehler-Meldung.
