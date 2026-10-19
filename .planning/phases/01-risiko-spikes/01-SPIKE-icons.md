# Spike Q6 — Symbole (256 px, Alpha, Threadmodell)

**Stand:** 2026-10-10 · Plan 01-06 · Code: `spikes/Icons/` (`ShellIcon`, `IconMetrics`, `StoreLink`, `ContactSheet`, `LatencyBench`), Fixtures: `spikes/tools/make-icon-fixtures.ps1`

## Frage

Q6: Liefert `IShellItemImageFactory` (`SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK`, 256 px) für **jeden** Eintragstyp der Leiste ein Bitmap mit sauberem Alpha — oder gibt es eine dokumentierte Ersatzregel? Welches Threadmodell (STA oder MTA) nutzt der IconCache in Phase 2 (Widerspruch `PITFALLS.md` Pitfall 7 gegen `STACK.md`)? Blockiert ein nicht erreichbarer Netzpfad den Aufrufer?

## Aufbau

Eigene Konsolen-App `spikes/Icons` (D-03, `net10.0-windows`, CsWin32, WPF nur für `BitmapSource`). Das Rezept aus `STACK.md` § 1 wurde übernommen und nicht neu geprüft: `GetObject` + `GetDIBits` (top-down 32 bpp), Straight-Alpha → `Pbgra32` **einmal** umwandeln → `Freeze()`. Jeder Aufruf läuft auf einem eigenen Arbeitsthread mit festem Apartment und hartem Zeitlimit (der Thread wird bei Überschreitung aufgegeben, nie dauerhaft gejoint).

Fixtures erzeugt das Skript (nichts Fremdes im Repo, Ordner `spikes/fixtures/` ist ignoriert): `Only48.exe` und `Only256.exe` (nur ein 48-px- bzw. 256-px-PNG-Frame), `.lnk` auf notepad, `.url` mit und ohne `IconFile`, Mini-PDF, Ordner, Store-App Rechner (`shell:AppsFolder\<AUMID>`) und ein `.lnk` auf sie (`IShellLinkW.SetIDList`). Netz: nur **eine** Symbolabfrage auf die Laufwerkswurzeln `L:` und `M:` (kein Auflisten, kein Schreiben) sowie ein nicht erreichbarer Pfad in `192.0.2.1` mit 3 s Zeitlimit. Pfade der Netzfälle stehen nur in der privaten CSV.

**Alpha-Orakel** je Bitmap: `all_opaque`, `corner_alpha_max` (vier 4×4-Ecken), `straight_ratio` (Anteil Pixel mit Farbkanal > Alpha, beweist Straight-Alpha), `fringe` (Randsaum: nach der Umwandlung über Weiß und Schwarz zusammengesetzt, Abweichung von 255 − Alpha), `rawfringe` (dasselbe auf den Rohdaten, die fälschlich als premultipliziert behandelt werden) und `edge` (Anteil weicher Randpixel an allen sichtbaren, hoch = unscharf). Ehrliche Einschränkung: `fringe` misst die Konsistenz der Umwandlung (Clipping), nicht den künstlerischen Randsaum eines Symbols; das Auge entscheidet am Kontaktbogen (H7).

Latenz: kalt = 5 frische Kindprozesse je Eintrag und Apartment (erster Aufruf nach Prozessstart), warm = 20 Wiederholungen nach einem Vorlauf, parallel = 8 verschiedene Einträge gleichzeitig gegen nacheinander, Zeitlimit = 5 Läufe mit gleichzeitiger Anfrage eines zweiten Eintrags. Zeiten sind die Zeit **im** Arbeitsthread (COM-Init, Abfrage, Pixel lesen), ohne Thread-Anlage.

Nachmessen: `powershell -File spikes/tools/make-icon-fixtures.ps1`, dann `dotnet build spikes/Icons -c Release -warnaserror`, `Icons.exe --all --out spikes/results/q6-icons.csv --sheet` und `Icons.exe --latency --out spikes/results/q6-latency.csv`.

## Messwerte

Rohdaten: `spikes/results/q6-icons.csv` (je Fall und Apartment, Spalten laut Kopfzeile, Netzfälle nur als Fallname) und `spikes/results/q6-latency.csv`. Kontaktbogen: nur lokal unter `spikes/results/private/`. Die Latenzen wurden gemessen, während der Entwickler den Rechner benutzte und andere Phase-1-Bauläufe möglich waren; sie sind **nur informativ**, es gibt keine Schwelle.

**Größe und Alpha** (Anforderung 256 px, STA; MTA gleiche Größe und gleiche Pixel außer bei `.url`):

| Fall | Größe | Ecken-Alpha | Straight-Alpha | `fringe` / `rawfringe` | Befund |
|---|---|---|---|---|---|
| `exe-notepad` | 256×256 | 0 | ja (17 %) | 0,0 / 17,4 | sauber |
| `exe-only256` | 256×256 | 0 | ja (30 %) | 0,0 / 34,1 | sauber |
| `exe-only48` | 256×256 | **66** | ja | 0,0 / 111,0 | **Symbol klein im Rahmen**, nicht hochskaliert |
| `lnk-file` | 256×256 | 0 | ja | 0,0 / 17,4 | kein Pfeil, identisch zu notepad |
| `lnk-store` | 256×256 | 0 | ja | 0,0 / 20,5 | kein Pfeil, identisch zu `appsfolder-store` |
| `appsfolder-store` | 256×256 | 0 | ja | 0,0 / 20,5 | Rechner, füllt das Bild |
| `folder` | 256×256 | 0 | ja | 0,0 / 4,8 | sauber |
| `url-plain` | 256×256 | 0 | ja | 0,0 / 15,1 | STA: Seitensymbol mit Standard-Browser-Zeichen; **MTA: leere Seite** |
| `url-iconfile` | 256×256 | 0 | ja | 0,0 / 2,7 | STA: `IconFile` wird übernommen; **MTA: leere Seite, `IconFile` ignoriert** |
| `pdf` | 256×256 | 0 | ja | 0,0 / 14,9 | Acrobat-Symbol statt Vorschaubild (`ICONONLY`) |
| `net-root-L`, `net-root-M` | 256×256 | 0 | ja | 0,0 / 12,1 | Netzlaufwerk-Symbol, 6–11 ms, nichts aufgelistet |
| `net-unreachable` | — | — | — | — | `timeout` nach 3001 ms |

Alle Bitmaps haben Straight-Alpha, `all_opaque` ist nie wahr, `fringe` nach der Umwandlung ist überall 0,0. Der Kontrast `rawfringe` (bis 111) zeigt, was ohne die Umwandlung passieren würde.

**Quelle begrenzt Schärfe** (`exe-only48`, STA): Die Shell skaliert ein 48-px-Symbol nicht hoch. Bei Anforderung 48 kommt ein sauberes 48×48 (Ecken 0); ab 64 px zeichnet sie das 48-px-Symbol in Originalgröße in die Mitte eines Rahmens (Ecken-Alpha 51 bei 64, 65 bei 128, 66 bei 256).

**Latenz** (ms, Median / p95; vollständig in `q6-latency.csv`):

| Eintrag | kalt STA | kalt MTA | warm STA | warm MTA |
|---|---|---|---|---|
| `exe-notepad` | 33,5 / 40,2 | 35,5 / 36,4 | 3,9 / 5,6 | 2,5 / 3,9 |
| `folder` | 30,1 / 39,6 | 32,2 / 33,9 | 4,0 / 7,2 | 4,0 / 6,2 |
| `lnk-store` | 48,8 / 55,7 | 52,4 / 53,3 | 5,4 / 8,1 | 6,4 / 9,0 |
| `url-plain` | — | — | 5,0 / 10,3 | 5,4 / 13,2 |

Kein Lauf scheiterte, kein STA/MTA-Paar lieferte eine andere Größe; die Pixel waren in allen Paaren gleich außer bei `url-plain` und `url-iconfile` (Hashspalte). Parallel: 8 verschiedene Einträge in 32 ms (MTA 32,1 / STA 32,8) gegen 68 ms bzw. 62 ms nacheinander. Zeitlimit: der nicht erreichbare Pfad endet in 3006 ms (max 3014); die gleichzeitige Anfrage von `exe-notepad` brauchte dabei 5,8 ms (MTA) bzw. 9,0 ms (STA), war also nicht blockiert.

### Handprüfung (D-07)

Handteil H7 am 2026-10-19 (Stephan):

| Punkt | Messung | Von Hand |
|---|---|---|
| Kontaktbogen auf hell, dunkel, Schachbrett (32 und 121 px) | 12/13 Fälle 256 px, Straight-Alpha umgerechnet | sauber, ohne dunklen Saum — bestanden |
| Echte Netzdatei auf einer Firmenfreigabe | nur Laufwerkswurzel geprüft | übersprungen |

## Entscheidung

**Entscheidung:** GO — zwölf von dreizehn Fällen liefern ein 256-px-Bitmap (der dreizehnte ist der absichtlich nicht erreichbare Pfad) mit gültigem Straight-Alpha, zwei Fälle brauchen eine Ersatzregel (Quelle nur 48 px: bei Rahmen erneut mit 48 px anfordern; `.url`: nur STA), und der Zeitlimit-Fall blockiert nichts.

## Folgen für Phase 2/3

- **Threadmodell: STA.** Der IconCache nutzt wenige dedizierte STA-Arbeitsthreads (Richtwert 2–4; acht gleichzeitige STA-Aufrufe liefen sauber und in 33 ms). MTA liefert für alles außer `.url` dieselben Pixel und ist nicht schneller (warm 2–6 ms in beiden Modellen), scheitert aber lautlos bei `.url`: leere Seite statt Symbol, `IconFile` ignoriert. Pitfall 7 trifft damit zu, soweit ein Icon-Handler beteiligt ist (Ursache vermutlich Apartment-Threading der Handler, nicht weiter untersucht); die Probe aus `STACK.md` betraf Dateien ohne solchen Handler.
- **Umwandlung:** jedes Bitmap hat Straight-Alpha → einmal nach `Pbgra32`, `Freeze()`, dann in Phase 3 nur noch zeichnen.
- **Zeitlimit je Eintrag: 3 s**, Arbeitsthread aufgeben statt warten; lokale Einträge brauchen warm 2–10 ms, kalt 30–55 ms. Bei 60 Symbolen kalt sind das 2–3 s nacheinander (parallel etwa die Hälfte) — also beim Start im Hintergrund laden und Platzhalter zeigen.
- **Ersatzregeln:** (1) Rahmen-Erkennung nach dem Laden: `corner_alpha_max > 0` bei 256 px → erneut mit 48 px anfordern und in WPF skalieren (Schärfe begrenzt die Quelle; Heuristik, ein vollflächiges Symbol könnte fälschlich anschlagen — in Phase 2 mit echten Symbolen prüfen). (2) `.url`: nur STA. (3) Netzlaufwerke bei Fehler oder Zeitlimit: Platzhalter-Symbol behalten, nicht wiederholen.
- **Während des Ladens** zeigt die Leiste ein neutrales Platzhaltersymbol (Phase 3 entscheidet das Bild); der Eintrag bleibt klickbar.
- `.lnk` und Store-Apps brauchen **keine** Pfeil-Entfernung und keine Auflösung des Ziels: die Shell liefert das Zielsymbol ohne Pfeil; `shell:AppsFolder\<AUMID>` und das `.lnk` auf die Store-App liefern dasselbe Bild.

## Grenzfälle für Stephan

Keine zur Entscheidung. Offen für Phase 2 vermerkt (2026-10-19): eine echte Netzdatei (H7 übersprungen), Symbole mit nativen 64- oder 128-px-Rahmen, 125/150 % Skalierung und der eigene Bestand an `.url`-Einträgen.
