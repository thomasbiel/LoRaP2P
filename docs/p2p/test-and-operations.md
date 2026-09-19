# Test- und Betriebsplan

## 1. P2P-Konfiguration

Die P2P-Daten werden getrennt von der Funkkonfiguration gespeichert.
Standardpfad ist `p2p.json` im Anwendungsverzeichnis. Ein optionaler
Startparameter `--peer-config <path>` überschreibt den Pfad.

Geplantes Format:

```json
{
  "LocalNodeId": 1,
  "Peers": [2],
  "AckTimeout": "00:00:02",
  "MaxRetries": 3,
  "BackoffMinimum": "00:00:00.250",
  "BackoffMaximum": "00:00:01",
  "AckTurnaroundDelay": "00:00:00.100",
  "DuplicateWindow": "00:10:00",
  "DuplicateEntriesPerPeer": 64
}
```

Validierung:

- lokale ID muss 1 bis 65534 sein,
- Peer-IDs müssen 1 bis 65534 sein,
- lokale ID darf nicht in `Peers` stehen,
- Peer-IDs sind eindeutig,
- ACK-Timeout und Zeitspannen müssen positiv sein,
- `BackoffMinimum` darf `BackoffMaximum` nicht überschreiten,
- `MaxRetries` muss 0 bis 10 sein,
- Cachegröße muss 1 bis 1024 sein.

Die Datei enthält keine Schlüssel. Schreiboperationen erfolgen atomar. Ein
Fehler beim Laden oder Speichern wird nicht durch leere Defaults verdeckt.

## 2. CLI-Verhalten

### Peer-Verwaltung

```text
peer id 1
peer add 2
peers
```

- `peer id` ersetzt die lokale ID und speichert sofort.
- `peer add` ist für bereits vorhandene IDs idempotent.
- Broadcast und ID 0 werden als Peer abgelehnt.
- `peers` zeigt konfigurierte IDs sowie sitzungsbezogen letzten Empfang,
  RSSI, SNR und letzte Sequenznummer.

### Empfangen

```text
listen
```

- gibt einen deutlichen Hinweis auf unverschlüsselten Betrieb aus,
- läuft bis Strg+C,
- zeigt Absender, Sequenznummer, Text/Hex, RSSI, SNR und Duplikatstatus,
- bestätigt direkt adressierte gültige Daten,
- bestätigt Broadcasts nicht,
- beendet die gesamte Anwendung bei Strg+C.

### Adressiertes Senden

```text
send-to 2 text "hello world"
send-to 2 hex DEADBEEF
```

Erfolgsausgabe enthält:

- Peer-ID,
- Sequenznummer,
- Anzahl Versuche,
- Time-on-Air des letzten Versuchs,
- ACK-Latenz.

Nach ausgeschöpften Retries wird ein Zustellfehler ausgegeben. `TxDone` allein
gilt nicht als Zustellung.

### Broadcast

```text
broadcast text "hello all"
```

Broadcast meldet nur den lokalen `TxDone`-Abschluss und weist ausdrücklich
darauf hin, dass kein ACK erwartet wird.

Empfangene Broadcast-Texte erscheinen zusätzlich auf dem OLED. Es stehen vier
Zeilen mit jeweils bis zu 21 Zeichen zur Verfügung. Längere Nachrichten
scrollen einmal im Abstand von einer Sekunde zeilenweise nach oben und lassen
die letzte Seite sichtbar. Eine neue Broadcast-Nachricht ersetzt die laufende
Anzeige. Direkt adressierte Nachrichten verändern das OLED nicht.

### Statistik

```text
stats
```

Statistiken gelten für die aktuelle Prozesssitzung und werden nicht
persistiert. Ein Reset-Befehl ist im ersten P2P-Release nicht vorgesehen.

## 3. Nachrichtenpersistenz und Chat-Export

Beim ersten Versand oder Empfang wird je Kombination aus lokaler Geräte-ID und
Gegenstelle eine Datei im Standardverzeichnis `~/LoRaP2P/sessions` geöffnet.
Die Writer bleiben bis zum Prozessende offen und schreiben UTF-8 ohne BOM.
Dateien können währenddessen gelesen und exportiert werden. Broadcasts werden
getrennt in einer Datei mit `peerbroadcast` im Namen geführt.

Dateinamen folgen dem Muster
`yyyyMMddTHHmmssfffZ-node<local>-peer<peer>.jsonl`; bei einer Kollision wird
ein numerischer Suffix ergänzt. Jede nichtleere Zeile enthält genau ein
JSON-Objekt:

| Feld | Bedeutung |
| --- | --- |
| `timestamp` | UTC-Zeitstempel |
| `direction` | `incoming`, `outgoing` oder `broadcast` |
| `localNodeId`, `peerNodeId` | lokale ID und Gegenstelle |
| `sequenceNumber` | Sequenznummer des P2P-Frames |
| `payloadType` | `text` bei gültigem UTF-8, sonst `hex` |
| `text` | Textdarstellung oder `null` |
| `hex` | vollständige Hexdarstellung |
| `status` | `received`, `acknowledged`, `failed` oder `sent` |
| `attempts` | Sendeversuche, falls zutreffend |
| `rssiDbm`, `snrDb` | Empfangswerte, falls zutreffend |

```text
export-chat ~/LoRaP2P/sessions/<session>.jsonl
export-chat ~/LoRaP2P/sessions/<session>.jsonl ./chat.html
```

Ohne zweiten Pfad entsteht die HTML-Datei neben der JSONL-Datei. Der Export
ist eigenständig, kodiert Nachrichteninhalte für HTML und meldet die
Zeilennummer eines ungültigen JSON-Objekts.

## 4. Unit-Testmatrix

### Frame-Codec

- Roundtrip für `Data`, `Ack` und `Error`,
- minimale und maximale Payload,
- falsche Version,
- unbekannter Typ,
- gesetzte reservierte Flags,
- ungültige IDs,
- zu kurzer Header,
- abweichende Payloadlänge,
- ACK mit Payload,
- Broadcast-ACK,
- Big-Endian-Testwerte.

### Peer- und Konfigurationszustand

- Laden einer gültigen Datei,
- fehlende Datei mit klar definiertem Initialzustand,
- ungültiges JSON,
- semantisch ungültige Werte,
- atomisches Speichern,
- doppelter Peer,
- lokale ID als Peer,
- I/O-Fehler beim Speichern.

### Empfang

- direkt adressierter neuer Frame wird einmal ausgeliefert und bestätigt,
- Duplikat wird bestätigt, aber nicht ausgeliefert,
- Broadcast wird ausgeliefert, aber nicht bestätigt,
- fremde Empfänger-ID wird verworfen,
- CRC-Fehler wird gezählt und verworfen,
- ungültiger Frame wird gezählt und verworfen,
- ACK und Error werden nicht als Nutzdaten ausgeliefert,
- Cancellation beendet den Empfang und hinterlässt Standby.

### Zuverlässiges Senden

- passendes ACK beim ersten Versuch,
- nicht passende Absender-ID,
- nicht passende Sequenznummer,
- ACK-Timeout und erfolgreicher Retry,
- drei Retries nach dem ersten Versuch,
- Zustellfehler nach vier Versuchen,
- Backoff liegt im konfigurierten Bereich,
- Broadcast wartet nicht auf ACK,
- Cancellation während TX, ACK-Wartezeit und Backoff,
- andere gültige Frames während der ACK-Wartezeit.

### CLI

- alle neuen Verben werden erkannt,
- quoted Text bleibt erhalten,
- Hex-Payload wird korrekt geparst,
- ungültige IDs und Hexdaten erzeugen verständliche Fehler,
- Ausgaben unterscheiden `TxDone`, ACK und Zustellfehler,
- `peers` und `stats` bilden den Dienstzustand korrekt ab.
- nur Broadcasts werden an die OLED-Anzeige weitergereicht,
- explizite Zeilenumbrüche, Wortumbruch und lange Wörter werden korrekt auf
  21 Zeichen breite Displayzeilen verteilt,
- Senden, Empfang und Broadcast erzeugen die erwarteten JSONL-Sitzungen,
- der Export funktioniert mit Standard- und explizitem Zielpfad,
- fehlerhafte JSON-Zeilen werden mit Zeilennummer abgelehnt,
- HTML-Sonderzeichen in Nachrichten werden nicht als Markup interpretiert.

## 5. Test-Doubles

Der skriptbare Radio-Fake stellt bereit:

- eine Queue für Empfangsergebnisse,
- eine Liste übertragener Payloads,
- steuerbare `TxDone`-Zeitpunkte,
- Timeouts,
- CRC-gültige und CRC-ungültige Pakete,
- Cancellation.

Zeit und Zufall werden injiziert. Tests dürfen nicht real zwei Sekunden auf
ACK-Timeouts oder Duty-Cycle-Pausen warten.

## 6. Hardwareabnahme mit zwei Geräten

### Vorbereitung

- beide Geräte mit demselben Funkprofil konfigurieren,
- Gerät A erhält ID 1 und Peer 2,
- Gerät B erhält ID 2 und Peer 1,
- auf beiden Geräten eine geeignete 868-MHz-Antenne anschließen,
- Abstand zunächst wenige Meter ohne direkte Abschirmung,
- Commit, Publish-Zeitpunkt und Konfigurationsdateien protokollieren.

### Testfälle

1. A lauscht; B sendet Text an A.
2. B lauscht; A sendet Binärdaten an B.
3. Beide Richtungen liefern ein eindeutig zugeordnetes ACK.
4. Ein Broadcast wird empfangen und nicht bestätigt.
5. Das erste ACK wird kontrolliert verworfen; der Retry wird nur einmal
   ausgeliefert.
6. Der Empfänger wird ausgeschaltet; der Sender meldet nach vier Versuchen
   einen Zustellfehler.
7. Beide Geräte senden möglichst gleichzeitig; Backoff löst mindestens einen
   späteren Versuch zeitlich auf.
8. Abweichende Frequenz führt zu Timeout.
9. Abweichendes Syncword führt zu Timeout.
10. Strg+C während `listen` beendet sauber und hinterlässt Standby.

### Zu protokollierende Werte

- Ergebnis und Zeitstempel,
- Sequenznummer,
- Versuchsanzahl,
- Time-on-Air,
- ACK-Latenz,
- RSSI und SNR,
- relevante Statistikzähler,
- beobachtete Duty-Cycle-Pause.

## 7. Releasevalidierung

```powershell
dotnet restore LoRaP2P.slnx
dotnet build LoRaP2P.slnx --configuration Release
dotnet test LoRaP2P.slnx --configuration Release
dotnet publish src/LoRaP2P.Console/LoRaP2P.Console.csproj `
  --configuration Release `
  --runtime linux-arm64 `
  --self-contained true `
  --output artifacts/linux-arm64
```
