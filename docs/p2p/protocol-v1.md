# P2P-Protokoll Version 1

## 1. Grundsätze

Das Protokoll liegt unmittelbar in der RFM95-Payload. Der SX1276 übernimmt
LoRa-Modulation und Payload-CRC; das P2P-Protokoll ergänzt Adressierung,
Sequenznummern und Nachrichtentypen.

- Maximale RFM95-Payload: 255 Byte
- Feste Headerlänge: 12 Byte
- Maximale unverschlüsselte Anwendungs-Payload: 243 Byte
- Mehrbyte-Felder: Big Endian
- Halbduplexbetrieb: Senden und Empfangen erfolgen nie gleichzeitig

## 2. Binäres Frameformat

| Offset | Größe | Feld | Kodierung |
| ---: | ---: | --- | --- |
| 0 | 1 | Protokollversion | Für diese Spezifikation `0x01` |
| 1 | 1 | Nachrichtentyp | `Data=0x01`, `Ack=0x02`, `Error=0x03` |
| 2 | 1 | Flags | In Version 1 immer `0x00` |
| 3 | 2 | Absender-ID | `UInt16`, Big Endian |
| 5 | 2 | Empfänger-ID | `UInt16`, Big Endian |
| 7 | 4 | Sequenznummer | `UInt32`, Big Endian |
| 11 | 1 | Payloadlänge | Anzahl der folgenden Bytes |
| 12 | 0–243 | Payload | Abhängig vom Nachrichtentyp |

Die gesamte Framegröße muss exakt `12 + Payloadlänge` entsprechen.
Zusätzliche oder fehlende Bytes machen den Frame ungültig.

## 3. IDs

- Gültige Geräte-IDs liegen zwischen 1 und 65534.
- `0xFFFF` adressiert alle Geräte als Broadcast.
- `0x0000` ist ungültig und darf weder als Absender noch als regulärer
  Empfänger gesendet werden.
- Ein Gerät verarbeitet nur Frames an seine eigene ID oder an `0xFFFF`.
- Frames mit der eigenen ID als Absender werden verworfen.

## 4. Nachrichtentypen

### 4.1 Data

- Payloadlänge: 1 bis 243 Byte.
- Direkt adressierte Frames werden mit einem `Ack` beantwortet.
- Broadcast-Frames werden niemals bestätigt.
- Die Interpretation als Text oder Binärdaten ist eine CLI-Eigenschaft und
  nicht Teil des Wire-Formats.

### 4.2 Ack

- Payloadlänge muss 0 sein.
- Absender und Empfänger sind gegenüber dem bestätigten `Data`-Frame
  vertauscht.
- Die Sequenznummer entspricht exakt dem bestätigten `Data`-Frame.
- ACKs an Broadcast sind ungültig.
- Ein ACK bestätigt nur den technisch gültigen Empfang des P2P-Frames.

### 4.3 Error

Ein `Error` darf nur als Antwort auf einen syntaktisch gültigen, direkt
adressierten Frame erzeugt werden, der nicht verarbeitet werden kann.
Ungültige, beschädigte oder fremd adressierte Frames werden still verworfen.

Die Error-Payload besteht aus:

| Offset | Größe | Feld |
| ---: | ---: | --- |
| 0 | 1 | Fehlercode |
| 1 | 0–242 | optionale UTF-8-Diagnose ohne Geheimnisse |

Fehlercodes der Version 1:

| Wert | Bedeutung |
| ---: | --- |
| 1 | Nachrichtentyp oder Operation nicht unterstützt |
| 2 | Empfänger vorübergehend beschäftigt |
| 3 | Verarbeitung intern fehlgeschlagen |

`Error` gilt nicht als Zustellbestätigung.

## 5. Framevalidierung

Ein Empfänger verwirft einen Frame ohne ACK, wenn mindestens eine Bedingung
zutrifft:

- Payload-CRC des RFM95 ist ungültig,
- Frame ist kürzer als 12 Byte,
- Version ist nicht `0x01`,
- Flags sind nicht `0x00`,
- Nachrichtentyp ist unbekannt,
- Absender-ID ist 0 oder `0xFFFF`,
- Empfänger-ID ist 0,
- deklarierte und tatsächliche Payloadlänge unterscheiden sich,
- Payloadregeln des Nachrichtentyps sind verletzt,
- Empfänger-ID ist weder lokal noch Broadcast,
- Absender-ID entspricht der lokalen ID.

Parserfehler dürfen keine teilweise initialisierten Frames zurückgeben.

## 6. Sequenznummern

- Jeder lokal erzeugte `Data`-Frame erhält eine neue `UInt32`-Sequenznummer.
- Die initiale Sequenznummer wird bei jedem Prozessstart kryptografisch
  zufällig gewählt.
- Nach `UInt32.MaxValue` folgt 0.
- Retries verwenden dieselbe Sequenznummer und denselben Frameinhalt.
- `Ack` und `Error` übernehmen die Sequenznummer des auslösenden Frames.

Die Sequenznummer allein bietet keine Replay-Sicherheit und darf später nicht
als AES-GCM-Nonce verwendet werden.

## 7. ACK- und Retry-Ablauf

1. Der Sender überträgt einen direkt adressierten `Data`-Frame.
2. Anschließend wartet er bis zu 2 Sekunden auf ein passendes ACK.
3. Passend bedeutet: Typ `Ack`, erwartete Absender-ID, lokale Empfänger-ID und
   identische Sequenznummer.
4. Andere gültige Frames werden während der Wartezeit verarbeitet, verlängern
   den ACK-Timeout aber nicht.
5. Nach einem Timeout wartet der Sender zufällig 250 bis 1000 ms.
6. Danach sendet er denselben Frame erneut.
7. Nach dem ersten Versuch sind maximal drei Retries erlaubt, also insgesamt
   höchstens vier Übertragungsversuche.
8. Nach dem letzten Timeout wird ein expliziter Zustellfehler gemeldet.

Die bereits im RFM9x-Treiber implementierte Duty-Cycle-Wartezeit gilt für
jeden Versuch und jedes ACK. Der Backoff ersetzt diese Wartezeit nicht.

## 8. ACK-Erzeugung

Ein gültiger direkt adressierter `Data`-Frame wird nach 100 ms beantwortet.
Die kurze Wartezeit ermöglicht dem Sender den Wechsel von TX nach RX.

Ein Duplikat wird erneut bestätigt, aber nicht erneut an die Anwendung
ausgeliefert. Damit kann ein verlorenes ACK durch einen Retry geheilt werden.

## 9. Duplikaterkennung

Der Empfänger hält pro Absender maximal 64 Kombinationen aus Sequenznummer und
Empfangszeitpunkt. Ein Eintrag gilt 10 Minuten lang als bekannt.

- Bekannter Frame: erneut ACK senden, nicht erneut ausliefern.
- Neuer Frame: ausliefern, merken und ACK senden.
- Älteste Einträge werden zuerst entfernt.
- Der Cache ist sitzungsbezogen und wird nicht persistent gespeichert.

Die zufällige initiale Sequenznummer reduziert Kollisionen nach einem Neustart.

## 10. Gleichzeitige Sendeversuche

Überlappende Funkübertragungen können kollidieren. Bleiben dadurch ACKs aus,
verteilen die zufälligen Backoffs die Retries zeitlich. Eine garantierte
Kollisionsfreiheit besteht nicht.

Alle P2P-Transaktionen eines Geräts werden serialisiert. Eine zweite lokale
Sendetransaktion wartet, bis die erste abgeschlossen oder fehlgeschlagen ist.

