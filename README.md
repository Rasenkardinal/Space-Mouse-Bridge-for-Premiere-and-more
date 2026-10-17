# Space Mouse Bridge for Premiere and more

Eine Windows-App, die eine 3Dconnexion SpaceMouse als konfigurierbares
Bediengeraet fuer **Adobe Premiere Pro** und **Google Chrome** nutzbar macht.
Die passende Belegung wird automatisch anhand der Vordergrundanwendung
ausgewaehlt. Der Schwerpunkt liegt auf feiner Timeline-Steuerung in Premiere,
frei belegbaren Achsen und Tasten sowie einer einheitlichen dunklen Oberflaeche.

## Funktionen im Ueberblick

- **Premiere Pro:** Jog / Abspielkopfsteuerung, horizontales und vertikales
  Timeline-Scrollen, Timeline-Zoom, Wiedergabe/Stopp und eigene Tastenkuerzel.
- **Einstellbare Kennlinien:** Totzone, Richtung, Geschwindigkeit,
  Kurvenexponent, Wiederholungsverhalten und Einzelbildbereich mit
  grafischer Vorschau.
- **Freie Belegung:** alle sechs Bewegungsachsen und beide unterstuetzten
  SpaceMouse-Tasten sowie ein konfigurierbares Bridge-Kontextmenue.
- **Chrome:** einzelne Browseraktionen wie Zurueck/Vorwaerts ueber die
  Bridge; kontinuierliches Scrollen und Zoom ueber das Chrome-Profil des
  Originaltreibers.
- **Profilverwaltung:** getrennte Einstellungen, automatische Profilwahl,
  globaler Ausgabeschalter und lokale Sicherung/Ruecknahme beim Speichern.
- **Alltag und Diagnose:** Windows-Autostart, Tray-Menue und aufklappbare
  HID-Diagnose mit Ereignisprotokoll.

## Voraussetzungen und Funktionsweise

Windows x64 und .NET Framework 4.x werden benoetigt. Fuer die
treiberbasierten Chrome-Funktionen muss 3Dconnexion 3DxWare installiert sein.
Die Premiere-Aktionen fuehrt diese App ueber Tastatur-/Mausausgabe aus;
dafuer muessen die passenden Shortcuts und der Fokus in Premiere stimmen.
Die App muss laufen und die Bridge-Ausgabe aktiviert sein.

Bei Auswahl von Chrome-Treiberfunktionen legt **Uebernehmen** die
anwendungsspezifische Benutzer-XML an bzw. passt sie an, mit vorheriger
Sicherung. Ein blosser App-Start installiert keine persoenliche
Treiberkonfiguration. Premiere-Einstellungen veraendern keine Treiber-XML.
Treiberfunktionen koennen auch bei pausierter oder beendeter Bridge wirken.

## Hardware und Projektstatus

Lokales Windows-Programm fuer die SpaceMouse Wireless ohne Bluetooth,
getestet ueber den 3Dconnexion-USB-Funkempfaenger (VID 256F / PID C652).
Kein eigener Treiber, keine externen Bibliotheken und keine Administratorrechte.
CAD-/globale Treiberprofile und normales Mausrad werden nicht veraendert.
Andere Geraete und Treiberversionen sind nicht pauschal verifiziert.
Das Projekt ist eine unabhaengige Anwendung und kein offizielles Produkt
von 3Dconnexion, Adobe oder Google.

## Bauen und starten

Windows x64 und .NET Framework 4.x mit enthaltenem C#-Compiler erforderlich.
Ein .NET-SDK ist nicht notwendig.

```powershell
.\build.ps1 -Test -Run
```

Alternativ die VS-Code-Aufgabe **SpaceMouse: Diagnose starten** oder die
gebaute `bin\SpaceMouseBridge.exe` verwenden. Vor erneutem Bauen die Bridge
ueber **Tray-Menue > Beenden** einschliesslich geoeffneter Mapping-Dialoge beenden.

Die Geraeteauswahl (Navigation/Zusatzcollection), HID-Pfad, Achsenrohwerte,
Tastenreports und der letzte HID-Report befinden sich unter
**Erweiterte Diagnose anzeigen**. Dieser Bereich ist beim Start geschlossen.
Profilwahl, Ereignisprotokoll und Aktionen bleiben direkt erreichbar.
Die Aktionsbuttons stehen oben links und rechts, Autostart mittig dazwischen
und Bridge-Ausgabe darunter. Ein zusaetzlicher Erfolgsstatus entfaellt; Fehler und fehlender
Raw Input werden weiterhin in der Protokollueberschrift angezeigt.
Der Ausgabebereich ist in der Standardansicht halb so hoch wie zuvor;
das Hauptfenster ist entsprechend kompakter. Beim Oeffnen der erweiterten
Diagnose waechst das Fenster wieder auf die bisherige Hoehe.
Das Auf- und Zuklappen aendert weder die Eingabeverarbeitung noch die
Belegungen; Zusatzcollections werden im Hintergrund weiterhin verarbeitet.

## Autostart

Im Hauptfenster **Bei Windows-Anmeldung automatisch starten** aktivieren.
Die Einstellung gilt sofort fuer den aktuellen Windows-Benutzer, ohne
Administratorrechte: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`,
Wert `SpaceMouseBridge`. Gespeichert wird der vollstaendige EXE-Pfad in
Anfuehrungszeichen; Deaktivieren entfernt nur diesen eigenen Wert.
Kein Start als Systemdienst vor der Anmeldung. Autostart verwendet `--tray`:
die Bridge startet verborgen im Windows-Infobereich neben der Uhr, mit
automatischer Profilwahl. Manuelles Oeffnen ohne Argument zeigt das Fenster.
Das Orbit-Cap-Tray-Icon bietet **Einstellungen öffnen**, **Bridge pausieren**
(im pausierten Zustand **Bridge fortsetzen**) und **Beenden**;
Doppelklick oeffnet das Fenster ebenfalls.
Das X am Hauptfenster blendet es nur aus, die Bridge arbeitet weiter.
Windows-Abmeldung und Herunterfahren werden nicht blockiert. Windows kann
das Icon im aufklappbaren Bereich der ausgeblendeten Symbole einsortieren.
Ein weiterer manueller Start oeffnet die vorhandene Instanz statt eine
zweite Bridge zu starten; `--tray` laesst eine vorhandene Instanz unveraendert.

Standardmaessig wird Autostart nicht eingerichtet. Beim Oeffnen liest der
Schalter den vorhandenen Eintrag. Ein bereits aktivierter Eintrag fuer
dieselbe EXE ohne `--tray` wird auf den Tray-Start aktualisiert; deaktivierter
Autostart wird nicht automatisch eingeschaltet. Nach Verschieben
der EXE am neuen Ort erneut aktivieren; eine alte Kopie muss vorher
geschlossen werden. Windows kann Autostart zusaetzlich unter
**Einstellungen > Apps > Autostart** bzw. im Task-Manager deaktivieren;
diese Windows-Sperre muss dort wieder aufgehoben werden.
Der App-Schalter zeigt die Registrierung fuer diese EXE, nicht diese
zusaetzliche Windows-Freigabe. Zugriffs-/Schreibfehler werden angezeigt und
protokolliert; der Schalter zeigt dann unbekannten Status und bleibt bis
zum Neustart gesperrt. Diagnosemodus und Tests aktivieren keinen echten
Autostart; Registrierungstests verwenden einen separaten temporaeren
Benutzerschluessel.

## App-Icon: Orbit Cap

Eigenstaendiges Logo: graphitfarbene Navigationskappe mit offener blauer
Umlaufbahn, ohne Herstellerlogo oder Schrift im Symbol.
`assets\orbit-cap.svg` ist die skalierbare Vorlage,
`assets\orbit-cap.png` die transparente 512-px-Vorschau.
`assets\orbit-cap.ico` enthaelt 16, 20, 24, 32, 40, 48, 64, 128 und 256 px;
kleine Varianten verwenden staerkere Konturen und weniger Details.
Das Icon ist in die EXE und alle eigenen App-Fenster eingebettet; keine
externen Bilddateien werden zum Start benoetigt.
Mit `powershell -NoProfile -File .\assets\build-icon.ps1` lassen sich PNG/ICO
ohne Zusatzpakete neu erzeugen. SVG und Renderer bei Designaenderungen
gemeinsam aktualisieren.

## VS-Code-Farbschema und UI

Die App setzt die mittlere Variante des UI-Sample-Sets um:
Arbeitsflaeche `#101112`, Karten `#191A1B`, Gruppenflaechen `#151718`,
Eingaben `#141617`, angehobene Flaechen `#202224`, Text `#D2D5D8` und
Primaeraktionen samt aktivierten Checkboxen `#297AA0`. Primaer- und
Sekundaeraktionen, Eingaben, Tabellen, Kurven, Statusmeldungen und eigene
Dialoge verwenden dieselben Farben, Konturen und mittleren Rundungen.
Checkboxen werden samt Beschriftung gemeinsam gezeichnet und haben einen
DPI-skalierten Abstand von acht Pixeln zwischen Feld und Text.
Ausgewaehlte Tabellenzeilen bleiben neutral abgesetzt.
Das interaktive [UI-Sample-Set](./ui-samples.html) zeigt alle
Bedienelemente und vergleicht die drei Dichtevarianten.

Unter Windows 11 werden runde Fensterecken explizit beim Desktop Window
Manager angefordert. Maximierte/angedockte Fenster koennen systembedingt
eckig bleiben; native Titelleiste, Schatten und Groessenaenderung bleiben
erhalten. Profilkopf, Diagnosefelder, Tabellen, Ereignisausgabe,
Aktionsbereich und Kurvenvorschau sitzen in abgerundeten Bereichen mit
8-px-Radius (DPI-skaliert), dezenten Raendern und einheitlichem
Innenabstand.
Im Windows-Kontrastmodus sind die Bereiche wieder rechteckig.
Alle App-Buttons, Zahlenfelder und Auswahlselektoren verwenden ebenfalls
DPI-skalierte 8-px-Rundungen, abgestimmte Konturen und einheitliche
Bedienhoehen. Hover-, Druck-, Fokus- und Tastaturverhalten bleiben bei
den nativen WinForms-Steuerelementen; im Kontrastmodus gilt die
Standarddarstellung.
Auch die aufgeklappten nativen Auswahllisten verwenden einen blauen
Rahmen, 9 px Rundung und gerundete Auswahlflaechen (DPI-skaliert).
Fensterhoehe, Inhaltsbereich, Scrollposition und Scrollbalken bleiben
vollstaendig unter Windows-Kontrolle. Es gibt keine kuenstlichen
Innenabstaende oder nachtraeglichen Scrollkorrekturen; die Rahmenzeichnung
spart den tatsaechlichen Inhaltsbereich aus, damit keine Eintraege
uebermalt werden.
Auswahl,
Tastaturbedienung und Scrollbalken bleiben nativ; der Kontrastmodus
behaelt die Windows-Darstellung.
Bei Zahlenfeldern lassen Text- und Pfeilbereiche den runden Aussenrahmen
frei; die Werte sind im Textbereich zentriert. Der abgesetzte Pfeilbereich
und der blaue Fokusrahmen folgen der gemeinsamen Palette. Deaktivierte
Felder verwenden gedaempfte Farben.
Auf-/Ab-Pfeile besitzen dieselbe mittlere, DPI-skalierte Groesse,
unabhaengig von geraden oder ungeraden Buttonhoehen.
Mapping-Tabellen verwenden angehobene Header, passende Zeilenfarben und
dieselbe Akzentfarbe fuer Checkboxen wie Primaerbuttons. Checkboxen behalten
native Bedienung, Fokus und deaktivierte Beschriftungen; im
Windows-Kontrastmodus gilt die systemseitige Darstellung.
Auswahlzellen zeichnen Hintergrund, Beschriftung und Pfeil gemeinsam, damit
keine hellen nativen Kacheln oder doppelten Pfeile sichtbar bleiben.
Geschlossene Auswahlfelder einschliesslich der aktiven Tabelleneditoren
zeichnen die gesamte Feldflaeche und den Rahmen, nicht nur den Pfeilknopf.
Dadurch bleiben auch die schmalen Uebergaenge zwischen Text und Pfeil dunkel.
Auswahl- und Zahlenfelder verwenden denselben blauen Tastaturfokusrahmen.

Auch die Schrift entspricht der Windows-Workbench: **Segoe UI, 13 px**
(9,75 pt bei 96 DPI), einschliesslich Bold. Die Monospace-Schrift des
Code-Editors wird bewusst nicht fuer normale Bedienelemente verwendet.
Die zuvor verwendete Inter Tight ist nicht mehr eingebettet/aktiv;
vorhandene Fontquelldateien und ihre Lizenz bleiben im Projekt erhalten.

Dies ist eine lokale Design-Uebernahme, keine laufende Synchronisierung
mit VS-Code-Einstellungen. Windows-Hellmodus aendert das feste Theme
nicht; **Windows-Kontrastmodus hat weiterhin Vorrang**.

Windows-Einstellungs- und Theme-Nachrichten aktualisieren bereits offene
Fenster ohne Neustart. Entwuerfe, Mapping-Werte und aktive Belegungen
werden dadurch nicht veraendert. Im Windows-Kontrastmodus haben die
System-Kontrastfarben Vorrang.

Unter Windows 11 sind auch Titelleisten, Fensterrahmen und native
Scrollbalken dunkel. Dropdown-Pfeile (einschliesslich Mapping-Tabellen),
Auswahllisten und Zahlenfeld-Pfeile verwenden passende dunkle Flaechen,
dezente Raender und lesbare Symbole statt heller Windows-Standardkacheln.
Die native Bedienung per Tastatur, Mausklick und Zahlenfeld-Wiederholung
bleibt erhalten. Hell- und Kontrastmodus schalten wieder auf die jeweilige
Standarddarstellung um; auch neu erzeugte Steuerelement-Handles werden
nachgefuehrt. Windows-eigene Dateidialoge folgen weiterhin dem Betriebssystem.

Das eingebettete Manifest aktiviert aktuelle Windows-Steuerelemente und die
korrekte Windows-Versionserkennung. Die nativen Darkmode-Schnittstellen
werden ab Windows 10 1903 dynamisch aufgeloest; fehlende Unterstuetzung wird
im Trace gemeldet. Fuer Windows 11 werden zusaetzlich DWM-Farben gesetzt.

Theme-Selbsttests pruefen beide Paletten, Textkontraste, Tabellenfarben,
unveraenderte Eingabewerte, die Windows-Einstellungsnachricht und Abbrechen
im Bestaetigungsdialog. Native Tests pruefen gerenderte Pfeilflaechen,
Titelleisten-/Scrollbalkenflaechen, Handle-Neuerstellung und unveraenderte Bedienung nach
Hell-/Dunkel-/Kontrastwechseln. Beide Mapping-Editoren werden visuell geprueft;
die laufende Bridge laedt weiterhin die unveraenderten Profile.

## Anwendungsprofile

Die Bridge waehlt das passende Profil anhand des Prozesses im Vordergrund,
nicht anhand von Fenstertiteln. Aktuell sind zwei Profile vorbereitet:

| Profil | Erkennung | Stand |
|---|---|---|
| Google Chrome | `chrome.exe`, Fensterklasse `Chrome_WidgetWin_1` | Bisherige Belegungen und Mapping-Editor |
| Adobe Premiere Pro | `Adobe Premiere Pro.exe` | Alle sechs Achsen und beide Tasten frei belegbar |

Im Hauptfenster **Profil bearbeiten** auswaehlen und **Mappings bearbeiten**
oeffnen. Diese Auswahl bestimmt nur den Editor, nicht die aktive Anwendung.
**Vordergrundprofil** zeigt die tatsaechlich erkannte Anwendung und ihren
Ausgabestatus. Profilwechsel erscheinen auch im Ereignisprotokoll, damit
die Erkennung nach der Rueckkehr ins Bridge-Fenster nachvollziehbar bleibt.
Im Bridge-Fenster selbst und in anderen Anwendungen bleibt
die Ausgabe aus. Auch ein anderes Chrome-Fenster setzt die Gestenerkennung
zurueck; vorgemerkte Aktionen werden niemals in ein neues Fenster uebertragen.

Die Checkbox **Bridge-Ausgabe (alle Profile)** pausiert die gesamte Bridge.
Zusaetzlich gilt der gespeicherte Aktivierungsschalter jedes Profils. Ein
gesperrtes oder extern veraendertes Profil gibt keine Eingaben aus; andere
fehlerfreie Profile bleiben verfuegbar. Waehrend ein Editor offen ist, ist
die Bridge-Ausgabe fuer alle Anwendungen pausiert.

### Premiere: alle Eingaben konfigurieren

**Profil bearbeiten: Adobe Premiere Pro > Mappings bearbeiten** oeffnet
den neuen Editor fuer **X, Y, Z, Rx, Ry, Rz und Taste 1/2**. Eine Zeile
auswaehlen und darunter Funktion und Parameter einstellen:

| Funktion | Ausgabe / Bedienung |
|---|---|
| Keine Bridge-Aktion | Keine synthetische Ausgabe; die Originaltreiberbelegung bleibt unveraendert |
| Jog / Abspielkopf | Negative Richtung: Pfeil links; positive Richtung: Pfeil rechts |
| Timeline horizontal scrollen | Mausrad ohne Modifier, passend zur Premiere-Praeferenz **Horizontal** |
| Spuren vertikal scrollen | Strg + Mausrad, in der bestaetigten Premiere-Konfiguration bei derselben Horizontal-Praeferenz |
| Timeline-Zoom | Minus / Gleich-Taste der Premiere-Standardbelegung, **kein** Browser-Strg-Zoom |
| Wiedergabe / Stopp | Ein kompletter Leertasten-Tipp |
| Eigenes Tastenkuerzel | Pro Achsenrichtung ein separates aufgezeichnetes Kuerzel; pro Taste ein Kuerzel |
| Bridge-Menue oeffnen (nur Tasten) | Eigenes Premiere-Listenmenue am Mauszeiger |

Tastenkuerzel ueber die Aufzeichnen-Buttons aufnehmen: normale Taste,
optional mit Strg/Umschalt/Alt; Escape bricht die Aufzeichnung ab. Die
Aufzeichnung sendet nichts an Premiere. Eigene Premiere-Shortcuts koennen
von den genannten Standardbefehlen abweichen; dann **Eigenes Tastenkuerzel**
verwenden. Keine Mauszeigersteuerung oder Panel-Fokuserkennung.

#### Eigenes Premiere-Kontextmenue

Im Premiere-Mapping-Editor **Kontextmenue bearbeiten** oeffnen.
Das Menue startet **leer**, ohne automatische Befehle. Zeilen hinzufuegen,
frei benennen und jeweils ein Tastenkuerzel aufzeichnen. Entfernen und
Nach oben/unten aendern die Reihenfolge. Leere Namen oder fehlende Kuerzel
werden mit Fehlerhinweis abgewiesen. **Entwurf uebernehmen** uebergibt die
Liste nur an den Mapping-Entwurf; erst **Sichern und uebernehmen** speichert
sie mit der vorhandenen Sicherung/Ruecknahme. Abbrechen schreibt nichts.

Anschliessend fuer **Taste 1 oder Taste 2** die Funktion **Bridge-Menue
oeffnen (nur Tasten)** zuweisen und speichern. Vorhandene Belegungen werden
nicht automatisch ersetzt. Beim Loslassen der Taste erscheint das eigene
dunkle Listenmenue am Mauszeiger, innerhalb des Bildschirm-Arbeitsbereichs.
Eintrag mit normaler Maus anklicken; Escape oder Fokusverlust/Klick ausserhalb
schliesst ohne Befehl. Ein leeres Menue zeigt einen Einrichtungshinweis.
Lange Listen sind scrollbar. Es wird kein Premiere-Rechtsklick simuliert.

Waehrend das Menue offen ist, verarbeitet die Bridge keine Achsen-/Tasten-
Aktionen. Danach ist erneut Neutralstellung bzw. Loslassen zum Freischalten
noetig. Ein gewaehltes Kuerzel geht nur an das vorherige Premiere-Fenster:
Fenster/Prozess, Fokus, Profil-/Quellenwechsel, Pause und Modifier werden
erneut geprueft. Wechselt der Fokus zu einer anderen App, wird nichts
gesendet und kein Fokus erzwungen. Fehlgeschlagene Fokuswiederherstellung
wird protokolliert. Die vorhandene SendInput-Teilfehlerfreigabe bleibt aktiv.
Das Menue erkennt keine Clips, Panels oder Textfelder; konfigurierte Kuerzel
wirken auf den zuvor fokussierten Premiere-Bereich. Andere Anwendungen
bekommen dadurch kein Menue und keine neuen Belegungen.
Selbsttests oeffnen das echte Popup vor einem isolierten Testfenster und
pruefen Fokus-Rueckgabe, genau ein Strg+F8 samt Freigabe sowie Abbruch/Reset
ohne Ausgabe. Weitere Tests pruefen Reihenfolge, leere Menues, Validierung,
Profilisolation, Speicherung und bytegenaue Ruecknahme.

**Kontinuierliche Achsenfunktionen** (Jog, Scrollen, Zoom) besitzen jeweils
eigene Richtung, Totzone, Minimum/Maximum, Exponent, Wiederholungsverzoegerung,
optionalen Einzelbereich und Endtempo. Die Live-Vorschau und die Ausgabe
verwenden dieselbe Kurve. Endtempo gleich Maximum schaltet die
Zusatzbeschleunigung aus; andernfalls beginnt sie oberhalb 60 % bzw.
oberhalb des Einzelbereichs. Vor der ersten Ausgabe nach Fokuswechsel,
Pause, Quellenwechsel oder 250 ms ohne frische Daten muss die jeweilige
Achse neutral sein. Fremde Achsen-/Tastenreports erneuern diese Lease nicht.
Modifier sperren die Ausgabe. Mehrere Achsen koennen unabhaengig arbeiten.

**Wiedergabe/Kuerzel auf Achsen** werden beim Ueberschreiten der
konfigurierten Einzelaktionsschwelle vorgemerkt und genau einmal bei
Rueckkehr in die Totzone ausgefuehrt. Halten wiederholt nicht.
Fokus-/Quellenwechsel, Pause und Datenabbruch verwerfen die Vormerkung.
**Tasten** loesen ihre Funktion genau einmal beim Loslassen aus, auch nach
laengerem Halten. Nach Reset muss zuerst ein ungedrueckter Zustand gesehen
werden. Die Richtung gilt fuer einzelne Jog-/Scroll-/Zoomschritte; bei
Kuerzeln verwenden Tasten ihr einziges aufgezeichnetes Kuerzel.

Der Editor uebernimmt bestehende **Rz-Jog- und X-Scroll-Werte samt 96er
Endtempo unveraendert** als Ausgangspunkt. Andere Eingaben bleiben leer.
Oeffnen/Vorschau/Abbrechen schreiben nichts. Erst bestaetigtes Speichern
legt die neue `PremiereInputs`-Belegung mit Sicherung ab; diese ist danach
fuer Premiere massgeblich. Die alten Jog-/Scroll-Felder bleiben fuer eine
verlustfreie Ruecknahme erhalten, steuern aber nicht parallel weitere
Ausgaben. Alte Dateien ohne `PremiereInputs` laufen unveraendert weiter.
Ruecknahme stellt den vorherigen Zustand bytegenau wieder her.
Chrome, CAD und alle Treiberdateien bleiben unveraendert. Bereits im
Originaltreiber belegte Achsen/Tasten koennen weiterhin Doppelaktionen
ausloesen; bei Bedarf deren Premiere-Belegung separat pruefen.

Selbsttests decken alle acht Eingaben, Richtungen, Standardausgaben,
Kuerzel/Teilfehlerfreigabe, Lease/Neutralstellung, Tasten-Zusatzcollections,
Editorwerte, Abbrechen, Speicherung, Profilisolation und Legacy-Undo ab.

### Premiere: bisherige Jog-/Scroll-Parameter

Die folgenden Abschnitte beschreiben die bisherigen Einstellungen und die
uebernommene Startbelegung. Die entsprechenden Werte sind jetzt pro
Eingabe im gemeinsamen Premiere-Editor einstellbar. Leerprofile bleiben
inaktiv, bis die Premiere-Bridge ausdruecklich aktiviert und gespeichert wird.

- **Links drehen (Rz negativ):** bildweise zurueck.
- **Rechts drehen (Rz positiv):** bildweise vor.
- **Staerker drehen:** mehr Schritte pro Sekunde, mit einstellbarer Kennlinie.
- **Neutral stellen:** keine weiteren Schritte; es wird keine Taste gehalten.
- Bisheriger Parametersatz (bleibt beim Laden erhalten): Totzone **12 %**, nahe der Totzone
  **0,5 Bilder/s** (ein Bild je zwei Sekunden), Maximum
  **120 Bilder/s**. Totzone (5 bis 40 %), Maximum (2 bis 120 Bilder/s) und
  Drehrichtung sind im Editor einstellbar. Der bisherige Exponent **2**
  verteilt die Geschwindigkeit quadratisch: bei einem Viertel des
  Arbeitsbereichs ca. 8, bei der Haelfte 30,4 und bei drei Vierteln 67,7
  Bilder/s; am Anschlag weiterhin 120. Auch im feinsten Bereich wird bei anhaltender Auslenkung
  wiederholt; der erste Schritt erfolgt sofort, Loslassen beendet die
  Wiederholung. Fuer genau ein Bild daher nur leicht drehen und loslassen.
  Die 25-ms-Zeitschleife fasst bei Bedarf mehrere vollstaendige Pfeiltasten-
  Tipps zusammen (maximal sechs pro Takt), damit nicht bei 40 Bildern/s
  Schluss ist. Premiere-Auslastung kann die praktisch erreichbare Frequenz
  begrenzen; es handelt sich nicht um eine garantierte Wiedergabegeschwindigkeit.

#### Kennlinie und Verhalten konfigurieren

Im Jog-Editor sind jetzt unabhaengig voneinander einstellbar:

| Einstellung | Bedeutung |
|---|---|
| Minimum (0,1 bis Maximum Bilder/s) | Wiederholungsrate direkt oberhalb des kontinuierlichen Bereichs; 0,5 bedeutet ein Bild alle zwei Sekunden |
| Maximum (2 bis 120 Bilder/s) | Wiederholungsrate bei voller Drehung |
| Kurvenexponent (0,25 bis 5) | 1 = linear, 2 = quadratisch, 3 = kubisch; hoeher = feinere Dosierung unten, unter 1 = schnellere Zunahme unten |
| Wartezeit (0 bis 3000 ms) | Erstes Bild immer sofort; erste Wiederholung fruehestens nach dieser Zeit und dem natuerlichen Schrittintervall |
| Kontinuierliches Jog | Auch kleine Auslenkungen wiederholen, wie bisher |
| Einzelbildbereich + kontinuierliches Jog | In einem zusaetzlichen Bereich oberhalb der Totzone ein Bild beim Auslenken oder Richtungswechsel, aber keine Wiederholung beim Halten |
| Einzelbildbereich bis Drehung (%) | Absolute Rz-Auslenkung, nicht Anteil des Restwegs; muss ueber der Totzone und hoechstens bei 95 % liegen |

Im optionalen Einzelbildbereich ermoeglicht Neutralstellen ein weiteres Bild.
Wird staerker ueber die Bereichsgrenze gedreht, beginnt kontinuierliches Jog;
eine konfigurierte Wartezeit seit dem ersten Bild bleibt dabei wirksam.
Bei Rueckkehr in den Einzelbildbereich stoppen Wiederholungen ohne ein
zusaetzliches Bild. Direkter Richtungswechsel gibt sofort ein Bild in die
neue Richtung und startet die Wartezeit neu.

Die Vorschau und die Laufzeit verwenden dieselbe Formel:
`Rate = Minimum + (Maximum - Minimum) * t^Exponent`.
`t` ist der normierte Restweg vom Beginn des kontinuierlichen Bereichs bis
zum Anschlag. Der Beginn liegt normalerweise an der Totzone, im
Einzelbildmodus an dessen aeusserer Grenze. Grau zeigt die Totzone, Gelb den
optionalen Einzelbildbereich; die blaue Kurve zeigt die Wiederholungsrate,
nicht den sofortigen ersten Schritt oder die Wartezeit. Zahlenwerte fuer
25/50/75/100 % des nutzbaren kontinuierlichen Bereichs stehen ueber der Kurve.

Die Vorschau sendet keine Eingaben und aendert keine Dateien. **Abbrechen**
verwirft den Entwurf. **Sichern und uebernehmen** speichert nach Bestaetigung
mit Sicherung; die vorhandene Ruecknahme gilt auch fuer diese Einstellungen.
Alte `FineControl`-Profile werden mit ihrem bisherigen Minimum und Exponenten
geladen (aktiv: 0,5 / 2; inaktiv: 2 / 1), ohne automatisch umgeschrieben zu
werden. Ohne neue Verhaltensfelder bleiben kontinuierliches Jog und 0 ms
Wartezeit aktiv.

**Vor Benutzung Wiedergabe stoppen und einmal in die Timeline klicken.**
Die Bridge sendet die Premiere-Standardbefehle **Pfeil links/rechts**
(ein Bild zurueck/vor). Eigene Premiere-Tastenbelegungen ggf. pruefen.
Es gibt noch keine Timeline-/Textfeld-Fokuserkennung innerhalb von Premiere:
im falschen Panel bewegen Pfeiltasten eventuell etwas anderes. Auch
Premiere-eigene Dialoge gehoeren zum selben Prozess. Dort Jog pausieren.
Jog erzwingt keinen Timeline-Fokus und sendet keine J/K/L-/Space-Befehle.
Die separat zuweisbare Funktion Wiedergabe/Stopp verwendet hingegen Space.
Jog ist keine Wiedergabe und kein Shuttle; neutrale Kappe stoppt nur die
Bridge-Schritte, nicht eine zuvor separat gestartete Wiedergabe.

Nach Fenster-/Anwendungswechsel, Bridge-Pause, gehaltenen Zusatztasten oder
Datenabbruch erst neutral stellen, bevor Jog wieder reagiert. Shift, Strg,
Alt und Windows-Tasten sperren Jog. Ohne frische, erfolgreich dekodierte
Rz-Daten fuer 250 ms stoppt die Ausgabe und verlangt erneut Neutralstellung.
Tasten- oder andere Achsenreports verlaengern diesen Zeitraum nicht.
Bei einem UI-Stau von mehr als 50 ms hinter dem faelligen Schritt wird der
Rueckstand verworfen, statt eine lange Aufholsalve zu senden.

#### Timeline-Ansicht seitlich verschieben (X)

Im gemeinsamen Premiere-Editor die Zeile **X** und die Funktion
**Timeline horizontal scrollen** auswaehlen und speichern.
Seitliches Schieben nach links/rechts scrollt zu frueheren/spaeteren Zeitpunkten;
Drehen bleibt unabhaengig davon Jog. Es werden nur Mausradimpulse ohne
Zusatztasten gesendet, keine Clip- oder Abspielkopf-Befehle.
In Premiere muss **Timeline mouse scrolling** auf **Horizontal** stehen
(wie im aktuellen Benutzer-Setup). Der Mauszeiger muss ueber der Timeline
stehen; andere Panels koennen die Mausradimpulse anders auswerten.
Die Bridge erzwingt keine Panelwahl und versetzt den Mauszeiger nicht.

Totzone 12 %, danach quadratisch von 0,5 bis standardmaessig 24
Mausradimpulsen/s. **Grundtempo** (2 bis 120), **Endtempo** (Grundtempo bis
120) und Richtung sind separat vom Jog einstellbar. Oberhalb von **60 %
X-Auslenkung** kommt eine quadratische Zusatzbeschleunigung hinzu:
`Rate = bisherige Rate + (Endtempo - Grundtempo) * max(0, (abs(X) - 0.6) / 0.4)^2`.
Bis einschliesslich 60 % bleibt die feine Grundkurve exakt erhalten; bei voller
Auslenkung gilt das Endtempo. Aktuell ist Grundtempo **24**, Endtempo **96**
aktiv (vierfaches Tempo am Anschlag). Alte Profile ohne Endtempo behalten
ihre bisherige Kurve; gleiches Grund-/Endtempo deaktiviert den Zusatz.
Ein Impuls entspricht 120 Windows-Mausradeinheiten; der sichtbare
Weg haengt von Premiere und Timeline-Zoom ab. Neutralstellung, Fokuswechsel,
Pause, Zusatztasten und 250 ms ohne frische X-Daten stoppen das Scrollen.
Rz-/Tastenreports ersetzen keine frischen X-Daten. Alte Profile ohne
Timeline-Scroll-Einstellungen bleiben unveraendert und scrollen nicht.
Speichern, Abbrechen und Ruecknahme gelten wie fuer Jog.
Das native x64-INPUT-Layout verwendet Mausraddaten ab Byte 16 und
Mausflags ab Byte 20; der Regressionstest prueft auch das ungesetzte Zeitfeld.

Alle uebrigen Achsen und beide SpaceMouse-Tasten bleiben beim Originaltreiber.
Die Premiere-Treiberdatei wird weiterhin nicht gelesen oder geschrieben.
Wenn der Treiber fuer Drehen bereits eigene Aktionen ausloest, kann es zu
Doppelbelegungen kommen: Bridge pausieren und die Treiberzuordnung pruefen,
bevor Jog weiter verwendet wird. Chrome und CAD bleiben separat.

**Letzte Aenderung rueckgaengig** stellt ausschliesslich den vorherigen
Premiere-Bridge-Zustand wieder her, ggf. das inaktive Leerprofil. Eigene
Ein echtes J/K/L-Shuttle und das Hinzufuegen beliebiger Anwendungen ueber
die Oberflaeche sind noch nicht implementiert. Eigene Kuerzel und weitere
Premiere-Achsen sind im neuen gemeinsamen Editor verfuegbar.

Praxistest mit SpaceMouse Wireless und Premiere bestaetigt: beide
Jog-Richtungen, auslenkungsabhaengige Geschwindigkeit und Stopp beim
Loslassen funktionieren. Das Bridge-Protokoll bestaetigt die gesendeten
Schritte und Stopps ohne Eingabefehler. Der aktivierte Parametersatz
(damals 12 %, 24 Bilder/s, nicht umgekehrt) wurde gespeichert und beim Start
geladen; Chrome-Einstellungen und beide vorhandenen Benutzer-Treiberprofile
blieben bytegenau unveraendert.

### Vorhandene Chrome-Einstellungen

Die bisherigen Dateipfade und das Version-1-Format bleiben kompatibel.
Bestehende Chrome-Einstellungen ohne Profilkennung werden ohne Umschreiben
geladen. Weder das Anlegen des Premiere-Profils noch das Wechseln zwischen
Anwendungen veraendert Chrome-XML, Chrome-Einstellungen oder dessen letzte
Ruecknahme. Erst ein ausdrueckliches Uebernehmen im Chrome-Editor speichert
eine Aenderung. Zuletzt selbst gespeicherte Belegungen haben Vorrang vor dem
unten beschriebenen historischen Ausgangssatz.

## Chrome-Mapping-Oberflaeche

Im Hauptfenster **Mappings bearbeiten** waehlen. Waehrend der Bearbeitung
pausiert die Bridge-Ausgabe. Der Editor enthaelt alle sechs Achsen und beide
Tasten (HID-Nummern 1/2). Die letzte Auswahl wird erst mit
**Sichern und uebernehmen** wirksam, nach einer ausdruecklichen Bestaetigung.
**Abbrechen** verwirft ungespeicherte Aenderungen.

### Achsen

Jede Achse kann einer dieser Funktionen zugeordnet werden:

| Modus | Ausfuehrung |
|---|---|
| Treiber unveraendert | Aktuelle XML-Belegung behalten; keine Bridge-Aktion |
| Deaktiviert | Achse im Chrome-Treiberprofil deaktivieren; keine Bridge-Aktion |
| Treiber: Scrollen | Kontinuierlich vertikal scrollen |
| Treiber: Shift+Scrollen | Shift+Mausrad, in Chrome ueblicherweise horizontal |
| Treiber: Zoom | Kontinuierlicher Seitenzoom ueber Strg+Mausrad |
| Bridge: Einzelaktionen | Negative und positive Richtung separat zuweisen |

Fuer Treiberausgabe sind Richtung, Verstaerkung (0,05 bis 5,00) und Totzone
(0 bis 250 HID-Einheiten) einstellbar. Fuer Bridge-Ausgabe werden die beiden
Richtungsaktionen und gemeinsame Gestenschwellen verwendet. Die Richtungen
beziehen sich auf die Rohwert-Vorzeichen in der Diagnose; bei Rz ist negativ
links, positiv rechts. Andere physische Achsenbeschriftungen sind eine
Orientierung, keine automatische Kalibrierung.

### Bridge-Aktionen und Tasten

Zur Auswahl stehen: keine Aktion, Zurueck, Vorwaerts, vorheriger/naechster Tab,
Zoom +/- als Einzelschritt, Zoom 100 %, neu laden, Seitenanfang und Seitenende.
Auch jede der beiden Tasten kann eine Aktion beim Loslassen ausfuehren oder
unveraendert beim Treiber bleiben. **Deaktiviert** unterdrueckt die Taste in
Chrome. Bei Bridge-Belegung unterdrueckt das Setup die entsprechende
Treiber-Zuordnung, damit nicht beide Programme dieselbe Eingabe verarbeiten.

**Treiber unveraendert stellt keine alte Belegung wieder her.** Es behaelt
den aktuellen XML-Zustand bei, also gegebenenfalls eine zuvor deaktivierte
Zuordnung. Fuer die vorherige Konfiguration die Ruecknahme verwenden.

### Ausgangsbelegung

Ohne gespeicherte Bridge-Einstellungen werden die vorhandenen Chrome-
Benutzerwerte eingelesen. Auf diesem System entspricht der bestaetigte Satz:

- Rx: vor/zurueck kippen scrollt, `Reversed=true`.
- Z: Ziehen vergroessert, Druecken verkleinert, Verstaerkung `0.50`,
  Totzone `35`, `Reversed=false`.
- Rz: links Zurueck, rechts Vorwaerts ueber die Bridge.
- Tasten und uebrige Achsen zunaechst beim Treiber belassen.
- Bridge-Ausloesung 45 %, Neutralzone 12 %.

Der urspruengliche Treiber-Parametersatz bleibt als
[Vorlage](presets/ChromeWithExtension.xml) im Projekt.

## Speichern, Sicherung und Ruecknahme

- Bridge-Einstellungen: `%LOCALAPPDATA%\SpaceMouseBridge\chrome.json`.
- Chrome-Benutzerprofil:
  `%APPDATA%\3Dconnexion\3DxWare\Cfg\ChromeWithExtension.xml`.
- Sicherungen: `%LOCALAPPDATA%\SpaceMouseBridge\backups\`.
- Premiere-Einstellungen (kompatibel mit dem bisherigen Leerprofil):
  `%LOCALAPPDATA%\SpaceMouseBridge\profiles\premiere\premiere.json`.
- Premiere-Sicherungen und Ruecknahmedaten liegen getrennt im selben
  Premiere-Unterordner, nicht in der Chrome-Ruecknahme.

Uebernehmen sichert den bisherigen Zustand beider Dateien, einschliesslich
vorheriger Abwesenheit. Nur ausgewaehlte Achsen-/Tastenzuordnungen im Chrome-
Benutzerprofil werden zusammengefuehrt. Andere XML-Abschnitte bleiben
erhalten. Mehrdeutige/gereatespezifische Zuordnungen werden als Konflikt
gemeldet, statt blind ueberschrieben zu werden. Jede Datei wird atomar
ersetzt; bei einem nachfolgenden Schreibfehler wird die Aenderung
zurueckgenommen. Dateihashes erkennen Aenderungen seit Oeffnen des Editors.
Bitte XML und Bridge-Einstellungen nicht gleichzeitig extern bearbeiten.

**Letzte Aenderung rueckgaengig** stellt das unmittelbar vorherige gespeicherte
Dateipaar wieder her; es ist kein mehrstufiger Undo-Verlauf. Sicherungen
bleiben lokal erhalten. Neuere externe Aenderungen oder beschaedigte
Sicherungen verhindern die Ruecknahme. Wiederholtes Uebernehmen identischer
Daten erzeugt keine neue Sicherung.

**Treiber neu einlesen** verwirft den Editorentwurf, importiert die aktuelle
Chrome-XML und setzt Bridge-Auswahlfelder auf den Ausgangssatz. Noch nicht
uebernommen! Nach externer XML-Aenderung die Auswahl sorgfaeltig pruefen.
Eine explizit im Treiber deaktivierte Achse wird als deaktiviert importiert.

Nach Treiberaenderungen kurz in ein anderes Programm und dann nach Chrome
wechseln, damit 3DxWare das Profil neu auswaehlt.

Treiberfunktionen bleiben auch ohne laufende Bridge aktiv. Die Checkbox
**Bridge-Ausgabe (alle Profile)** im Hauptfenster pausiert nur die Bridge und ist
ein temporaerer Schalter. Der dauerhafte Startzustand wird im Mapping-Editor
gespeichert. Externe Datei-Aenderungen werden beim Start und im laufenden
Betrieb erkannt; bei Konflikten bleibt die Bridge-Ausgabe aus.

## Ausfuehrung und Schutz vor Fehlbedienung

- Chrome-Hauptfenster und Premiere im Vordergrund erhalten nur die Aktionen
  ihres eigenen aktivierten Profils.
- Chrome: Einzelaktion pro Auslenkung; Halten wiederholt nicht. Erst neutral stellen,
  dann auslenken; ausgefuehrt wird beim Loslassen aller sechs Achsen.
- Keine kuenstliche Wartezeit. Vollstaendige HID-Reports werden vor der
  Gestenpruefung dekodiert, zusaetzliche Freigabepruefung alle 25 ms.
- Die Ausgabe wartet auf freigegebene Zusatztasten. Insbesondere wird Strg
  des Treiber-Zooms weder mitbenutzt noch losgelassen.
- Pro Bewegung ist hoechstens eine Aktion vorgemerkt. Bei gleichzeitig
  ausgeloesten Zuordnungen gewinnt die erste; nach 5 Sekunden ohne sichere
  Freigabe wird sie protokolliert verworfen.
- Fenster-/Quellen-/Empfaengerwechsel und Pause der Bridge verwerfen
  Vormerkungen. Tasten muessen nach einem Wechsel erst losgelassen werden.
- SendInput sendet Druecken und Loslassen gemeinsam; bei Fehlern wird nur
  die eigene noch gehaltene Eingabe freigegeben und die Ausgabe deaktiviert.
- Nur eine interaktive Instanz. Fenster offen lassen, Minimieren ist moeglich.
- Chrome-Kurzbefehle koennen von Seiten/Fokus beeinflusst werden. Zurueck/
  Vorwaerts braucht passenden Verlauf; Verlassen/Neuladen kann ungespeicherte
  Seiteneingaben verlieren lassen.

## Diagnose und Tests

Raw Input empfaengt Generic Desktop / Multi-axis Controller (01/08), filtert
3Dconnexion/Logitech-Collections und dekodiert ueber HID-Deskriptoren. Keine
Aufzeichnung einer normalen Tastatur/Maus. Die Anzeige zeigt letzte Werte,
Reportalter und Fehler. **Empfaenger vorhanden heisst nicht Funkmaus wach.**
Ausbleibende Reports beweisen keine Trennung. Der Empfaenger wird bei
Abziehen/Einstecken neu erkannt; Rueckkehr aus Bewegungspausen verlangt
gegebenenfalls erneut Neutralstellung.

Die Geraeteauswahl (Navigation/Zusatzcollection) bleibt bei laufenden
Werteupdates unveraendert. Nur tatsaechliche Aenderungen der Geraeteliste
bauen ihre Eintraege gebuendelt neu auf; die Auswahl bleibt dabei erhalten,
solange die gewaehlte Collection vorhanden ist. Reine Textupdates loesen
kein erneutes Layout der fest dimensionierten Diagnosezeilen aus.

`.\build.ps1 -Test` kompiliert mit Warnungen als Fehlern. Neben Decoder-,
ABI-, Gesten- und Tastaturtests prueft es die UI-Werteuebernahme, alle
Aktionspakete, Settings-Validierung, XML-Merge, Unterdrueckung der Treiber-
Zuordnung, Idempotenz, Sicherung/Ruecknahme, externe Aenderungen und Rollback
bei Schreibfehlern. Profiltests pruefen zusaetzlich Legacy-Kompatibilitaet,
bytegenaue Chrome-Erhaltung, getrennte Speicherung und Ruecknahme,
Prozess-/Fensterzuordnung, Fehlerisolation, das Verwerfen vorgemerkter
Aktionen beim Fenster-/Profilwechsel sowie die Premiere-Speicherung ohne
Treiberzugriff. Jog-Tests messen Schrittfrequenzen, Totzonengrenzen,
Neutralfreigabe, Richtungswechsel, Umkehrung, Daten-Timeout und fehlende
Aufholsalven. Sie pruefen vollstaendige Pfeiltastenpakete, UI-Werte und
bytegenaue Ruecknahme auf das Leerprofil. Weitere Tests vergleichen die
Vorschauformel mit gezaehlten Schritten im 25-ms-Takt, pruefen alte JSON-
Profile, Wiederholungsverzoegerung, Einzelbildgrenzen und Bereichswechsel,
alle neuen UI-Werte, Entwurf ohne Dateiaenderung sowie Speichern und
Ruecknahme der vollstaendigen Jog-Konfiguration. Beide Vorschauvarianten
wurden visuell geprueft. Beim Neustart bleiben vorhandene Einstellungen,
Ruecknahmedaten und Treiberdateien unveraendert. Dateitests laufen ausschliesslich in einem eigenen
temporaeren Verzeichnis; keine Eingaben werden gesendet.

Die Profilumschaltung wurde ausserdem im laufenden Programm durch Aktivieren
von Chrome und Premiere geprueft: beide Prozesszuordnungen wurden erkannt,
Premiere war dabei noch ohne Bridge-Ausgabe. Chrome-Einstellungen, Chrome-Ruecknahme
und die vorhandenen Benutzer-Treiberprofile beider Anwendungen blieben dabei
bytegenau unveraendert. Das Premiere-Leerprofil wurde nach Neustart erneut
geladen.

Unsichtbarer, zeitlich begrenzter Nur-Lese-Test:

```powershell
$exe = Join-Path $PWD 'bin\SpaceMouseBridge.exe'
$out = Join-Path $PWD 'probe.diagnostic.json'
Start-Process $exe -ArgumentList ('--probe-seconds 5 --output "' + $out + '"') -Wait
Get-Content -LiteralPath $out
```

`--descriptor-test --output <JSON-Datei>` prueft lokal erzeugte Reports im
Arbeitsspeicher gegen die tatsaechlichen HID-Deskriptoren. Keine Uebertragung
an die Hardware. Beide Testmodi schreiben keine Mapping-Dateien.
Exitcode 0 der Probe beweist nur fehlerfreien Empfangsaufbau, nicht Bewegung:
`devices`, `reports` und `errors` im JSON pruefen. Export enthaelt lokale
Geraetepfade und wird nirgendwohin uebertragen.

Am 16.10.2026 vom Benutzer bestaetigt: sechs Achsen/beide Tasten, parallele
CAD-Navigation, Empfaenger-Wiederanschluss, Chrome-Scrollen/Zoom sowie
Tab- und Verlaufsnavigation beim Loslassen. Diese Ergebnisse gelten fuer
die getestete Konfiguration, nicht pauschal fuer alle Geraete/Treiberversionen.
Neue Zuordnungen bitte in Chrome und danach mit CAD-Wechsel praktisch testen.

Die Mapping-Oberflaeche wurde ebenfalls am 16.10.2026 praktisch geprueft:
Uebernahme der bisherigen Belegung mit weiter funktionierendem Scrollen,
Zoom und Drehen, Neuzuordnung von Taste 1 auf naechster Tab ohne zusaetzliches
Treibermenue sowie Ruecknahme auf den vorherigen Zustand vom Benutzer
bestaetigt. Laden der gespeicherten Auswahl in einem neuen Prozess und
Nur-Lese-Probe ohne Datei-/Eingabeaenderungen wurden zusaetzlich geprueft.
