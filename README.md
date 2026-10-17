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
- **Chrome:** einzelne Browseraktionen wie Zurueck/Vorwaerts und ein
  konfigurierbares Bridge-Kontextmenue am Mauszeiger; kontinuierliches Scrollen
  und Zoom ueber das Chrome-Profil des Originaltreibers.
- **Konfigurierbare Radialmenues:** zwei unabhaengige Menues pro Chrome- und
  Premiere-Profil, frei auf die beiden SpaceMouse-Tasten legbar. Jeweils 4 bis 8
  vollstaendig belegte Segmente mit eigenen Tastenkuerzeln und wahlweise Text
  oder einem von 33 einfarbigen Werkzeug-/Aktionsicons. Ein visueller Editor
  zeigt das Menue in Originalgroesse und aktualisiert Aenderungen sofort.
  Radius (40 bis 180 Pixel), Mittelkreis, Konturen, Segmentabstand sowie
  getrennte Text- und Icongroessen sind pro Menue einstellbar.
  Das Menue erscheint am Mauszeiger; ein erneuter Tastendruck schliesst es
  wieder, mit 250 ms Schutz gegen unbeabsichtigtes Doppelausloesen.
- **Profilverwaltung:** getrennte Einstellungen, automatische Profilwahl,
  globaler Ausgabeschalter und lokale Sicherung/Ruecknahme beim Speichern.
- **Alltag und Diagnose:** Windows-Autostart, Tray-Menue und aufklappbare
  HID-Diagnose mit Ereignisprotokoll.

## Screenshots

Aktuelle Oberflaeche mit neutralen Beispielbelegungen; die gezeigten Shortcuts
im Radialmenue sind Beispiele und keine automatisch installierten Premiere-Befehle.

### Radialmenue mit Icon-Auswahl und Livevorschau

![Radialmenue-Editor mit acht Iconsegmenten und einstellbarer Darstellung](assets/screenshots/radial-menu-editor.png)

### Gemeinsame Eingabeliste im Chrome-Profil

![Chrome-Profil mit sechs Achsen und beiden SpaceMouse-Tasten](assets/screenshots/chrome-profile.png)

### Premiere-Profil

![Premiere-Profil mit Eingabeliste und Parametern fuer die ausgewaehlte Eingabe](assets/screenshots/premiere-profile.png)

### Hauptfenster

![Hauptfenster mit Profilauswahl und aufklappbarer Diagnose](assets/screenshots/main-window.png)

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

Beim Windows-Autostart bzw. beim Start mit `--tray` startet die App mit
ausgeblendetem Hauptfenster im Infobereich (Tray) neben der Uhr.
Per Doppelklick auf das Tray-Symbol oder ueber **Einstellungen oeffnen**
im Tray-Menue laesst sich das Fenster aufrufen. Das Schliessen des Fensters
blendet es nur aus; die App laeuft im Hintergrund weiter.
Ein manueller Start ohne `--tray` zeigt das Hauptfenster direkt an.

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
| Bridge-Menue oeffnen (nur Tasten) | Eigenes Premiere-Radialmenue am Mauszeiger |

Tastenkuerzel ueber die Aufzeichnen-Buttons aufnehmen: normale Taste,
optional mit Strg/Umschalt/Alt; Escape bricht die Aufzeichnung ab. Die
Aufzeichnung sendet nichts an Premiere. Eigene Premiere-Shortcuts koennen
von den genannten Standardbefehlen abweichen; dann **Eigenes Tastenkuerzel**
verwenden. Keine Mauszeigersteuerung oder Panel-Fokuserkennung.

#### Oberflaechenanordnung
Das freigegebene Layout ist in Hauptfenster und Mapping-Editoren umgesetzt:
Die Profilauswahl steht naeher an **Mappings bearbeiten**, die Auswahl
**Menue 1/2** und **Menue bearbeiten** sind rechts ausgerichtet. Im Premiere-
Editor steht diese Optionszeile etwas hoeher. Bei geringer Chrome-Fensterbreite
wechseln die Menueoptionen in die zweite Zeile, ohne die uebrigen Optionen
zu ueberdecken. Benutzerprofile und Menuebelegungen bleiben unveraendert.
Der Erklaerungstext unter der Chrome-Eingabeliste steht mit zusaetzlichem
Abstand von 43 Pixeln zur Liste, entsprechend dem aktualisierten UI-Entwurf.

#### Eigenes Premiere-Kontextmenue

Der Chrome-Mapping-Editor zeigt wie der Premiere-Editor alle sechs Achsen und
beide Tasten in einer gemeinsamen Eingabeliste. In Tastenzeilen steht die Aktion
in **Positive Richtung / Tastenaktion**; achsenspezifische Felder sind dort
deaktiviert. Beide Menueaktionen bleiben nur fuer Tasten auswaehlbar.

Chrome und Premiere besitzen jeweils **zwei unabhaengige Radialmenues**.
Im Mapping-Editor **Menue 1** oder **Menue 2** auswaehlen und **Menue bearbeiten**
oeffnen. Jedes Menue hat eigene Eintraege und eigene Darstellungsparameter.
Bestehende Menueeintraege, Darstellung und Tastenzuweisungen bleiben als
**Menue 1** erhalten; Menue 2 startet unkonfiguriert, ohne automatische Befehle.
Die Aktionen **Menue 1 oeffnen** und **Menue 2 oeffnen** sind beiden Tasten
frei zuweisbar, auch vertauscht oder beide Tasten fuer dasselbe Menue.
Nicht konfigurierte Menues melden beim Tastendruck Konfigurationsbedarf;
andere Achsen- und Tastenbelegungen bleiben nutzbar.
Die **Anzahl Segmente** auf **4 bis 8** einstellen. Alle Segmente teilen den
Vollkreis gleichmaessig auf; es gibt keine Seiten- oder Navigationssegmente.
Der **Radius (Pixel)** ist per Regler und Zahlenfeld von **40 bis 180** einstellbar.
Alte Profile mit bis zu 200 Pixeln bleiben lesbar; Vorschau und Laufzeit begrenzen
deren Darstellung auf 180. Beim Uebernehmen im Menueeditor wird der begrenzte Wert gespeichert.
Die 1:1-Vorschau reagiert sofort, einschliesslich Beschriftung und Klickflaechen.
Beim Aendern des Radius bleibt die Kreismitte auf der Vorschauflaeche fest;
das Menue waechst oder schrumpft gleichmaessig um diesen Punkt. Die Vorschau
benoetigt keine Scrollbalken.
Der Radius wird getrennt pro Profil gespeichert; bestehende Profile behalten
77 Pixel (164 x 164 Pixel inklusive Rand). Abbrechen veraendert den Radius nicht.
**Mittelkreis (%)** stellt den Mittelkreisradius per Regler und Zahlenfeld
auf 15 bis 60 Prozent des Aussenradius ein (Standard: 33 Prozent).
Vorschau, Segmentflaechen und Fensterkontur passen sich sofort an.
Der Wert wird ebenfalls pro Profil gespeichert. Der Mittelkreis verwendet
dieselbe Hintergrundfarbe wie die nicht ausgewaehlten Segmente.
Radius- und Mittelkreis-Regler stehen nebeneinander. **Konturlinie (Pixel)**
stellt die Segmentkonturen von 0,5 bis 5 Pixeln mit sofortiger Vorschau ein.
Die Linien werden nach innen gezeichnet; Segmentabstaende und Aussenradius
bleiben unveraendert. Die Breite bleibt auch bei anderem Radius in Pixeln
konstant und wird pro Profil gespeichert (Standard: 1 Pixel).
**Segmentabstand (Pixel)** stellt den freien Abstand von 0 bis 8 Pixeln ein
(Standard: 2 Pixel). Die gegenueberliegenden geraden Segmentkanten sind
parallel: Die Luecken bleiben innen und aussen gleich breit, statt sich
keilfoermig zu verbreitern. Livevorschau, Klickflaechen und Fensterkontur
verwenden dieselbe Geometrie. Der Abstand wird pro Profil gespeichert.
Der Groessenregler unter der Segmentanzahl ist kontextsensitiv: Bei einem
Textsegment zeigt er **Text (pt)** (4 bis 16 pt, Standard 6), bei einem
Iconsegment **Icon (px)** (8 bis 48, Standard 24). Jedes Menue speichert eine
Textgroesse und eine separate Icongroesse fuer alle entsprechenden Segmente.
Segment- und Darstellungswechsel behalten beide Werte. Vorschau und echtes
Menue aktualisieren sich identisch. Beide Groessen skalieren mit dem Radius;
Icon-Pixel beziehen sich auf den Standardradius 77. Die tatsaechliche Icongroesse
wird durch den verfuegbaren Platz im Segment begrenzt.
Die Vorschau zeigt das Menue in Originalgroesse mit derselben
Zeichenroutine und Beschriftung wie zur Laufzeit. Segment- und Mittelkreisflaechen
verwenden das Flaechengrau der App, Konturen deren graue Eingabefeldumrandung;
die ausgewaehlten Segmente bleiben blau hervorgehoben.
Ein Segment direkt anklicken, rechts den Namen eingeben und das Tastenkuerzel
aufzeichnen. Das ausgewaehlte Segment ist blau; Aenderungen erscheinen sofort.
Pro Segment unter **Darstellung im Segment** zwischen **Text** und **Icon**
waehlen. Die Iconliste zeigt Symbol und Namen; Auswahl und Vorschau aktualisieren
sich sofort. Text- und Iconsegmente koennen in beiden Menues beliebig gemischt
werden, auch in Chrome. Name/Beschreibung und Kuerzel bleiben fuer jedes Segment
erforderlich. Icons aendern nicht den ausgefuehrten Shortcut; die Funktion muss
zum eigenen Premiere- bzw. Chrome-Kuerzel passen.
Der eingebaute einfarbige Satz im Stil **A1 - Weiche Kontur** enthaelt
33 eigenstaendig gezeichnete Vektorsymbole:
Auswahl, Spurauswahl, CUT / Rasierklinge, Hand, Zoom, Text, Stift, Wiedergabe/Pause, Stopp,
Schritt vor/zurueck, Marker, Rueckgaengig/Wiederholen, Speichern, Ripple/Rolling
Edit, Slip/Slide, In/Out, Einfuegen/Ueberschreiben, Verknuepfen, Magnet, Stumm,
Clip teilen, Trimmen, separates Play und Pause, Rendern, Entfernen und Luecke
schliessen. CUT verwendet eine diagonale Rasierklinge, keine Schere.
Es gibt nur diesen einen Stil, keine zusaetzliche Icon-Set-Konfiguration.
Es werden keine Adobe-Grafiken oder Schrift-Icons verwendet. Die Symbole skalieren
mit dem Menue und passen in die Segmentflaeche. Bestehende Menueeintraege bleiben
unveraendert im Textmodus; fehlende Icongroessen verwenden den bisherigen Wert 24.
**Jedes Segment muss vollstaendig belegt sein.** Neue Segmente haben keine
automatischen Befehle; leere Namen oder fehlende Kuerzel verhindern die
Uebernahme mit Fehlerhinweis. Beim Reduzieren fragt der Editor nach, bevor
belegte Segmente aus dem Entwurf entfernt werden.
**Entwurf uebernehmen** uebergibt das Menue nur an den Mapping-Entwurf;
erst **Sichern und uebernehmen** speichert es mit der vorhandenen
Sicherung/Ruecknahme. Abbrechen schreibt nichts.
Alte gespeicherte Menues bleiben lesbar und werden nicht automatisch gekuerzt.
Alte Premiere-Profile mit Menue-Tastenbelegung, aber ohne Menueeintraege,
bleiben ebenfalls ladbar; nur die Menueoeffnung meldet Konfigurationsbedarf.
Ein fehlerhaftes Profil wird nicht als leerer Entwurf zum Ueberschreiben geoeffnet.
Beim Bearbeiten werden kurze Altmenues auf mindestens vier zu belegende
Segmente erweitert; mehr als acht Eintraege muessen bewusst reduziert werden.

Anschliessend fuer **Taste 1 oder Taste 2** die Funktion **Menue 1 oeffnen
(nur Tasten)** oder **Menue 2 oeffnen (nur Tasten)** zuweisen und speichern. Vorhandene Belegungen werden
nicht automatisch ersetzt. Beim Druecken der Taste erscheint sofort ein eigenes
Radialmenue mit seinem Mittelpunkt am Mauszeiger; am Bildschirmrand wird es
in den sichtbaren Bereich verschoben. Achsenwerte oder Neutralstellung sind
zum Oeffnen nicht erforderlich, auch bei reinen Tasten-Zusatzcollections.
Halten wiederholt die Oeffnung nicht. Ein erneuter Druck auf dieselbe Taste
schliesst das Menue ohne Befehl. Zum Schutz vor versehentlichen Doppeldruecken
werden solche Schliessversuche in den ersten 250 ms nach dem Oeffnen ignoriert.
Den Mauszeiger in ein Segment bewegen und
anklicken; Escape oder ein Klick ausserhalb schliesst ohne Befehl.
Es wird kein Premiere-Rechtsklick simuliert.

Waehrend das Menue offen ist, verarbeitet die Bridge keine Achsen-/Tasten-
Aktionen. Danach ist erneut Neutralstellung bzw. Loslassen zum Freischalten
noetig. Ein gewaehltes Kuerzel geht nur an das vorherige Premiere-Fenster:
Fenster/Prozess, Fokus, Profil-/Quellenwechsel, Pause und Modifier werden
erneut geprueft. Wechselt der Fokus zu einer anderen App, wird nichts
gesendet und kein Fokus erzwungen. Fehlgeschlagene Fokuswiederherstellung
wird protokolliert. Die Menueanzeige blockiert die Bridge nicht; die vorhandene
SendInput-Teilfehlerfreigabe bleibt aktiv.
Das Menue erkennt keine Clips, Panels oder Textfelder; konfigurierte Kuerzel
wirken auf den zuvor fokussierten Premiere-Bereich. Andere Anwendungen
bekommen dadurch kein Menue und keine neuen Belegungen.
Selbsttests oeffnen das echte Popup vor einem isolierten Testfenster und
pruefen Fokus-Rueckgabe, genau ein Strg+F8 samt Freigabe sowie Abbruch/Reset
ohne Ausgabe. Weitere Tests pruefen Segmentklicks, 4 bis 8 Segmente,
Vollkreisaufteilung, vollstaendige Belegung, Profilisolation, Speicherung
und bytegenaue Ruecknahme.

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

### Vorhandene Chrome-Einstellungen

Die bisherigen Dateipfade und das Version-1-Format bleiben kompatibel.
Bestehende Chrome-Einstellungen ohne Profilkennung werden ohne Umschreiben
geladen. Weder das Anlegen des Premiere-Profils noch das Wechseln zwischen
Anwendungen veraendert Chrome-XML, Chrome-Einstellungen oder dessen letzte
Ruecknahme. Erst ein ausdrueckliches Uebernehmen im Chrome-Editor speichert
eine Aenderung.

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
