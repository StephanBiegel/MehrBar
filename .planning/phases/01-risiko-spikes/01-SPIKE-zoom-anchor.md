# Spike Q5 — Zoom-Verankerung `cursor`

**Stand:** 2026-10-10 · Plan 01-05 · Code: `src/MehrBar.Core/Layout/` (`ZoomKernel`, `ZoomEngine`), Test: `src/MehrBar.Tests/ZoomAnchorTests.cs`, Fixture: `src/MehrBar.Tests/Fixtures/RocketDockReference.cs`

## Frage

Q5: Sagt die Verankerung `cursor` (das Symbol unter der Maus bleibt stehen, die Leiste wächst ungleich) die vier Messreihen aus „Nachtrag zu Q12“ auf ±6 px voraus — und zwar als **Vorhersage** aus unabhängig gemessenen Eingaben, nicht als Anpassung an die Leistenränder? Und kann derselbe Test die Verankerung `center` widerlegen, sonst trennen die Reihen die beiden nicht?

## Aufbau

**Modell** (reine Funktion in Core, achsenneutral, `ZoomEngine.Compute(items, pitch, params, pointerU, anchor)`):

- Abstand `d` = |Zeiger − Ruhemitte des Eintrags| / Raster, **immer aus Ruhepositionen** (kein Rückkoppeln der verschobenen Lage).
- Kernel `f(d) = (cos(π·d/r) + 1)/2` für `d < r`, sonst 0.
- Zusatzplatz eines zoomenden Eintrags: `Länge · (Zoomgröße/Symbolgröße − 1) · f(d)`; bei Symbolen (Länge = 32 = Symbolgröße) ist das `(121 − 32) · f`.
- `cursor`: Der Eintrag unter dem Zeiger verteilt seinen Zusatzplatz nach dem Zeigeranteil `f` im Eintrag (`f · extra` davor, Rest dahinter); alle übrigen Zusatzplätze liegen ganz davor bzw. dahinter. Leiste = `[Ruhe-Anfang − davor, Ruhe-Ende + dahinter]`. `center`: Summe halbiert.

**Eingaben, jede unabhängig von den Leistenrändern gemessen** (`spikes/tools/measure-zoom-fixture.ps1`, liest `samples/reference/` an Ort und Stelle, die Bilder gelangen nie ins Repo):

| Eingabe | Quelle | Messung |
|---|---|---|
| Ruhegeometrie | Bild 1 | Trennlinien bei x = 234 / 442 / 554 → 6p + q = 208 und 3p + q = 112 → **Raster p = 32,00, Trennlinien-Platz q = 16,00**, Ränder 14,5 / 14,5 (16 Symbole in Gruppen 6/6/3/1, 3 Trennlinien) |
| Ausrichtung der Bilder | Hintergrund | Korrelation des Himmels unter der Leiste: dx = 114 / 90 / 114 / 117, dy = 0 (deckt sich mit dem Tabellenrahmen, ohne dass ein Rand eingeht) |
| Zeigeranteil je Reihe | Symbol unter der Maus | Das Symbol hält den Zeigerpunkt fest, seine Mitte wandert um `(0,5 − f) · 89` gegenüber der Ruhemitte. Nur die Pixel dieses einen Symbols gehen ein: **f = 0,489 / 0,067 / 0,933 / 0,657** (±2 px, also ±0,02) |
| Radius | Nachbarn in „Maus mittig“ | Breite von Ring und PDF (direkte Nachbarn: 92 / 94 px) und Würfel und „a“ (übernächste: 35 / 39 px), je auf die Ruhebreite normiert, nach `r` aufgelöst: 2,72 / 2,72 / 2,41 / 2,46 → **r = 2,58** (Mittel, Streuung 2,41 bis 2,72) |
| Trennlinien je Bild | dünne Linien in den Leistenzeilen | je drei Positionen pro Bild, ohne Leistenränder — zweite, unabhängige Prüfgröße |

**Der Rand −3** (links bei mittig, Publisher, Excel) wird laut Plan als Grenze geprüft („wahrer Rand liegt bei −3 oder weiter links“); nur die rechten Ränder und beide Ränder beim 2. Symbol sind Sollwerte. Zusätzlich zeigt die Verschiebung der ersten Trennlinie (jeweils −112 gegenüber Ruhe, aus den Bildern gemessen), dass der Rand wirklich bei −3 liegt und kein Bildbeschnitt ist.

**Gegenprobe `center`:** dieselben Eingaben, Verankerung `center`; der Test `Center_anchor_misses_reference_rows` verlangt mindestens einen unbeschnittenen Rand mit mehr als 6 px Abweichung.

**Ablauf ehrlich dokumentiert:** Der erste Lauf folgte dem Plan wörtlich (Trennlinien-Platz fest 16 px, `Zooms = false`). Das verfehlt den rechten Rand der Publisher-Reihe um 44 px und schiebt den linken Rand der Excel-Reihe 29 px rechts von seiner Grenze. Ursache stand in den Glyphen von Bild 4: zwischen Publisher und Excel liegen 63 px, im Ruhebild 18 px; die Lücke wächst um `16 · (121/32 − 1) · f(d) ≈ 45 px`, also **der Trennlinien-Platz skaliert wie ein Symbol-Platz** (die Linie selbst nicht). Die Regel hat keinen freien Parameter und gilt für alle Reihen; sie wurde aus der Lücke in Bild 4 gefunden, nicht aus den Leistenrändern. Der wörtliche Plan-Ansatz bleibt als Test (`Fixed_separators_miss_the_rows_next_to_a_separator`) erhalten.

## Messwerte

Vollständig: `spikes/results/q5-zoom-residuals.csv` (je Reihe, Verankerung, Rand/Trennlinie, Soll, Vorhersage, Rest, Variante). Rohprofile und Glyph-Kästen nur unter `spikes/results/private/` (nicht im Repo). Tabellenrahmen = Bild-1-Pixel + 89; alle Werte in px.

`cursor`, Basisvariante (r = 2,58, f gemessen):

| Reihe | Rand | Soll | Vorhersage | Rest |
|---|---|---|---|---|
| mittig (P-touch) | links (Grenze) | ≤ −3 | −6,0 | erfüllt |
| | rechts | 812 | 813,1 | +1,1 |
| 2. Symbol | links | 27 | 24,6 | −2,4 |
| | rechts | 812 | 812,9 | +0,9 |
| Publisher | links (Grenze) | ≤ −3 | −5,9 | erfüllt |
| | rechts | 804 | 802,9 | −1,1 |
| Excel | links (Grenze) | ≤ −3 | −6,5 | erfüllt |
| | rechts | 733 | 728,2 | −4,8 |

Trennlinien (12 Werte, unabhängig von den Rändern): Rest −5,1 bis +3,0 px, zehn der zwölf innerhalb ±3,5 px; der größte Wert ist Excel, Linie 3: −5,1 px. Gesamtmaximum unbeschnittener Rest: **5,1 px** (Ränder allein: 4,8 px).

`center`, dieselben Eingaben: 2. Symbol −17,6 / −14,4 px, Excel +37,9 px am rechten Rand und +39,1 px links der Grenze; mittig und Publisher passen zufällig (Ränder ≤ 4 px). Die Reihen trennen `cursor` und `center` also klar, und zwar mit **2. Symbol** und **Excel**.

Empfindlichkeit (`q5-zoom-residuals.csv`, Spalte `variant`), größter Rest bei `cursor`:

| Variante | größter Rest | innerhalb ±6? |
|---|---|---|
| Basis (r 2,58) | 5,1 | ja |
| f − 0,02 | 4,6 | ja |
| f + 0,02 | 6,6 | nein |
| r = 2,41 / 2,72 (gemessene Streuung) | 7,0 / 9,5 | nein |
| r − 0,25 / + 0,25 | 10,8 / 14,4 | nein |
| Zoomgröße 117 / 125 | 6,1 / 8,7 | nein |
| Trennlinien fest (Plan-Ansatz) | 44,0 | nein |

`center` verfehlt in jeder Variante um 38 px oder mehr; `cursor` bleibt in jeder Variante außer „Trennlinien fest“ bei höchstens 14,4 px.

Zwei Nebenbefunde: Die sichtbaren Glyphen der Symbole unter der Maus messen 110 bis 115 px, der Platz im Layout wächst aber auf 121 (Bildmaterial hat Rand; die Leistenbreite verlangt 121). Der Abfall ist steiler als ein reiner Kosinus: direkte Nachbarn ergeben `r` ≈ 2,72, übernächste ≈ 2,43.

## Entscheidung

**Entscheidung:** GO — Verankerung `cursor` sagt alle unbeschnittenen Ränder auf ±6 px voraus (max. Restfehler 4,8 px an den Rändern, 5,1 px an den Trennlinien; r = 2,58 gemessen); `center` verfehlt 17,6 px (2. Symbol) und 37,9 px (Excel, rechter Rand). Trennlinien-Platz skaliert dabei mit dem Zoom (gemessen).

Die Verankerung ist damit entschieden. Die Genauigkeit auf ±6 px trägt nur innerhalb von r ± 0,1; siehe Grenzfälle.

## Folgen für Phase 2/3

- **Phase 2** nutzt nur Zoom-Fortschritt 0: `RestItem`-Liste mit Raster 32, Trennlinien-Platz 16 (Linie 1 px mittig), Ränder 14,5 und achsenneutrale Koordinaten. `ZoomEngine` ist ohne WPF und Windows-Assemblies in `MehrBar.Core.Layout` und steht dafür bereit.
- **Phase 3** erweitert `ZoomEngine` (RestLayout/FrameLayout nach `ARCHITECTURE.md`, allokationsfreie Variante, Animation); die Namen `ZoomKernel`, `ZoomEngine`, `ZoomAnchorMode`, `ZoomParams` bleiben. `center` bleibt wählbar. **Trennlinien-Platz muss mitzoomen** (`RestItem.Zooms = true` für Trennlinien); gezeichnet wird die Linie unverändert.
- Kalibrierung dort gegen die Vollbild-Neuaufnahmen und die Reihe Zoom-Breite 2/6/8 (offener Punkt in STATE.md); `ZoomCheck` und `spikes/tools/measure-zoom-fixture.ps1` messen neue Bilder mit denselben Schritten. Der Kernel ist dabei zu prüfen: ein steilerer Verlauf als Kosinus würde die Abweichung zwischen direkten und übernächsten Nachbarn (r 2,72 gegen 2,43) erklären.
- Der linke Rand −3 ist nach der Trennlinien-Messung kein Beschnitt; die Neuaufnahme wird trotzdem gebraucht, weil sie `r` und die Zoomgröße schärfer festlegt.

## Grenzfälle für Stephan

- **Dünner Rand der Genauigkeit.** Der Befund ist ein Punktwert: r = 2,58 trifft alle Reihen auf 5,1 px; liegt `r` 0,1 daneben (gemessene Streuung der Nachbarn 2,41 bis 2,72), kippt es über ±6 px (7,0 bzw. 9,5 px), ebenso f + 0,02 (6,6 px) oder Zoomgröße ±4. Die Verankerung selbst bleibt in allen Varianten eindeutig `cursor` (≤ 14,4 px gegen ≥ 38 px). Empfehlung: GO stehen lassen, Zahl in Phase 3 neu kalibrieren.
- **Werte bei 5–6 px:** Excel, rechter Rand (−4,8 px) und Excel, Trennlinie 3 (−5,1 px).
- **Trennlinien-Regel nachträglich gefunden.** Anlass war der Fehlschlag des rechten Rands der Publisher-Reihe mit festem Platz; die Regel selbst stammt aus der Glyph-Lücke in Bild 4 (kein freier Wert, nicht aus den Rändern angepasst). Die übrigen Reihen, Ränder und Linienpositionen gingen nicht ein und treffen trotzdem. Wer das nicht gelten lassen will, behandelt Q5 als UNKLAR für die Ränder neben einer Trennlinie.
- **Zoomgröße:** 121 aus dem Nachtrag (Platz im Layout), sichtbare Glyphen nur 110–115 px.
