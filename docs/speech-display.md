# Sprache auf dem Radio-Bonnet-Display

`LoRaP2P.SpeechDisplay` ist ein eigenstaendiges .NET-10-CLI fuer Raspberry Pi
OS 64-bit. Ein externes ALSA-Mikrofon liefert Sprache, Vosk wandelt sie lokal
in Text um. Das SSD1306-OLED des Adafruit Radio Bonnet zeigt vier Zeilen mit
jeweils bis zu 21 Zeichen. Funkhardware wird nicht verwendet. Netzwerkzugriff
ist nur fuer den erstmaligen Download eines noch nicht vorhandenen Modells
noetig; Spracherkennung erfolgt weiterhin offline.

## Raspberry Pi vorbereiten

Das Bonnet hat kein Mikrofon. Ein USB-Mikrofon anschliessen und I2C aktivieren:

```bash
sudo raspi-config nonint do_i2c 0
sudo apt-get update
sudo apt-get install -y alsa-utils unzip curl libatomic1 libstdc++6
sudo usermod -aG audio,i2c,gpio "$USER"
```

Danach neu anmelden. Display-Zuordnung: I2C1, Adresse `0x3C`, Reset BCM GPIO4,
128 x 32 Pixel. Die Bonnet-Taster werden vom Sprachtool nicht verwendet.
Nicht gleichzeitig mit `LoRaP2P.Console` starten: Beide Programme greifen auf
dasselbe OLED und denselben Reset-Pin zu.

## Mikrofon pruefen

```bash
arecord -l
arecord -L
arecord --device plughw:1,0 --format S16_LE --rate 16000 --channels 1 \
  --duration 5 /tmp/microphone.wav
aplay /tmp/microphone.wav
```

`plughw:1,0` durch das eigene Aufnahmegeraet ersetzen. `plughw` erlaubt ALSA,
das Mikrofonformat auf 16 kHz, Mono und 16-Bit-Little-Endian-PCM umzusetzen.
Ein stabiler Geraetename wie `plughw:CARD=USB,DEV=0` verhindert Probleme durch
wechselnde Kartennummern. Ohne `--device` wird `default` verwendet.

## Vosk und Sprachmodell

Das NuGet-Paket `Vosk` 0.3.38 stellt die C#-API bereit, enthaelt aber keine
Linux-ARM64-Bibliothek. Seine hostabhaengigen Kopierregeln sind im Projekt
deaktiviert, damit ein Windows-Publish keine Windows-DLL auf den Pi kopiert.
Die offizielle Vosk-0.3.45-AArch64-Bibliothek liegt unveraendert unter
`src/LoRaP2P.SpeechDisplay/runtimes/linux-arm64/native` und wird bei
`linux-arm64` automatisch neben das CLI kopiert. Lizenz und Herkunftsnachweis
werden unter `licenses` mit veroeffentlicht. Andere Zielarchitekturen erhalten
diese ARM64-Bibliothek nicht.

Das Tool laedt standardmaessig `libvosk.so` aus seinem Programmverzeichnis,
unabhaengig vom aktuellen Arbeitsverzeichnis. `--vosk-library <path>` ist ein
optionaler Override, etwa fuer andere Architekturen oder eigene Builds.

Ohne `--model` laedt das Tool beim ersten Start automatisch
<https://alphacephei.com/vosk/models/vosk-model-small-de-0.15.zip> herunter.
Modelle liegen im Benutzerordner unter `~/LoRaP2P/models/<URL-SHA256>`.
Die URL bestimmt den Cache-Schluessel; unterschiedliche Modell-URLs erhalten
getrennte Verzeichnisse. Vorhandene vollstaendige Modelle werden ohne
Netzwerkzugriff wiederverwendet.

`--model-url https://example.org/anderes-vosk-modell.zip` waehlt ein anderes
Modellarchiv. Nur HTTPS-URLs ohne Zugangsdaten oder Fragment sind erlaubt.
Das ZIP muss genau ein Vosk-Modell im Archivwurzelverzeichnis oder in einem
direkten Unterverzeichnis enthalten. Es wird zuerst in einen temporaeren
Ordner geladen und entpackt, dann anhand der Modell-, Konfigurations- und
Graphdateien geprueft und atomar in den Cache verschoben. ZIP und temporaere
Dateien werden auch nach Fehlern oder Abbruch entfernt. Das Gesamtzeitlimit
fuer die Modellbereitstellung betraegt 30 Minuten.

`--model /pfad/zum/entpackten/modell` verwendet stattdessen ein vorhandenes
lokales Modell, ohne Download oder Aenderung dieses Ordners. Damit ist auch
ein vollstaendig netzwerkfreier Erststart moeglich.

Die native Bibliothek benoetigt `libatomic.so.1`, `libstdc++.so.6`, `libm.so.6`,
`libgcc_s.so.1` und `libc.so.6`. Diese Systembibliotheken muessen auf dem Pi
vorhanden sein; ein selbstenthaltenes .NET-Publish ersetzt sie nicht.
Nach Uebertragung im Publish-Verzeichnis `ldd ./libvosk.so` ausfuehren und
Eintraege mit `not found` beheben. Ein Sprachmodell ist kein Bestandteil
des Publish-Ordners, wird aber bei Bedarf automatisch heruntergeladen. Das kleine
deutsche Modell ist etwa 45 MB gross und benoetigt laut Anbieter ungefaehr
300 MB RAM zur Laufzeit. Auf dem Pi 3B+ ein kleines Modell verwenden.

Quelle und Modelllizenzen: <https://alphacephei.com/vosk/models>.

## Bauen und starten

Auf dem Entwicklungsrechner:

```powershell
dotnet publish src/LoRaP2P.SpeechDisplay/LoRaP2P.SpeechDisplay.csproj `
  --configuration Release `
  --runtime linux-arm64 `
  --self-contained true `
  --output artifacts/speech-display/linux-arm64
```

Den Publish-Ordner auf den Pi uebertragen und dort starten:

```bash
chmod +x LoRaP2P.SpeechDisplay
./LoRaP2P.SpeechDisplay \
  --device plughw:1,0 \
  --scroll-ms 1000
```

`--help` zeigt alle Optionen. `--no-display` erlaubt Mikrofon- und Vosk-Tests
unter Linux ohne angeschlossenes Bonnet; erkannter Text erscheint im Terminal.
`--scroll-ms` akzeptiert 100 bis 10000 Millisekunden. Ctrl+C beendet Aufnahme
und Scroll-Task, gibt Vosk/GPIO/I2C frei und schaltet das OLED aus.

## Anzeigeverhalten

Nach einer Sprechpause liefert Vosk einen bestaetigten Textabschnitt. Dieser
wird im Terminal ausgegeben und mit Wortumbruch in die Anzeige eingereiht.
Vosk-Teilresultate werden nicht angezeigt, damit korrigierte Hypothesen keine
doppelten Woerter erzeugen. Deshalb erfolgt die Anzeige nicht sofort pro Wort;
lange Saetze erscheinen erst nach der naechsten Sprechpause.

Pro Intervall wird eine weitere Zeile eingefuegt; nach vier Zeilen wandert die
aelteste nach oben aus dem Display. Neue Saetze ersetzen keine laufende
Anzeige. Ohne neue Sprache bleibt der letzte Text stehen. Der Puffer ist auf
256 ausstehende Zeilen begrenzt; bei Ueberlast entfallen die aeltesten noch
nicht angezeigten Zeilen. Umlaute und scharfes S sind im Font vorhanden,
unbekannte Zeichen erscheinen als `?`.

Die Anwendung speichert weder Audio noch Transkripte. Der erkannte Text geht
jedoch an stdout und kann durch Terminalprotokollierung gespeichert werden.
Die Mikrofon-Testaufnahme unter `/tmp/microphone.wav` bei Bedarf entfernen.

## Validierung

Hardwarefreie NUnit-Tests decken Scroll-Puffer, Zeilenumbruch, Framebuffer,
Vosk-JSON, ALSA-Argumente, fragmentierte PCM-Reads und Cancellation ab.
Downloadtests verwenden simulierte HTTP-Antworten fuer Cache, lokale Modelle,
eigene URLs, Fehler, Abbruch sowie unvollstaendige und unsichere ZIPs.

```powershell
dotnet test tests/LoRaP2P.Console.Tests/LoRaP2P.Console.Tests.csproj `
  --configuration Release `
  --filter "FullyQualifiedName~SpeechDisplayTests|FullyQualifiedName~RadioBonnetTextLayoutTests|FullyQualifiedName~VoskModelStoreTests"
```

Auf echter Hardware pruefen: Mikrofon verstaendlich, deutscher Text nach
Sprechpausen, Wortumbruch und Aufwaertsscrollen bei mehr als vier Zeilen,
mehrere aufeinanderfolgende Saetze, Ctrl+C und anschliessender Neustart.
Ein erfolgreicher ARM64-Publish ersetzt weder den Test der nativen
Vosk-Bibliothek auf dem Pi noch den Mikrofon-/OLED-Test.
