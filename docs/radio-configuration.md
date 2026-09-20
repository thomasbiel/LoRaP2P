# Funkkonfiguration (`appsettings.json`)

Diese Datei dokumentiert alle Einstellungen der Funkkonfiguration. Die
mitgelieferte [`appsettings.json`](../src/LoRaP2P.Console/appsettings.json)
ist für einen Adafruit RFM95W Radio Bonnet am Raspberry Pi und Raw LoRa bei
869,525 MHz vorkonfiguriert.

> Die Konfiguration ist keine automatische Zulässigkeitsprüfung. Insbesondere
> validiert die Anwendung nicht, ob Frequenz, belegte Bandbreite,
> Antennengewinn, effektive Strahlungsleistung, Duty Cycle und Geräteklasse
> zusammen alle regulatorischen Anforderungen des Einsatzlandes erfüllen.

## 1. Standardkonfiguration

```json
{
  "SpiBusId": 0,
  "ChipSelectLine": 1,
  "ResetPin": 25,
  "Dio0Pin": 22,
  "FrequencyHertz": 869525000,
  "SpreadingFactor": "Sf7",
  "Bandwidth": "Khz125",
  "CodingRate": "FourOfFive",
  "OutputPowerDbm": 14,
  "PreambleSymbols": 8,
  "SyncWord": 52,
  "PayloadCrcEnabled": true,
  "ImplicitHeader": false,
  "InvertIq": false,
  "DutyCycle": 0.1,
  "OperationTimeout": "00:00:05"
}
```

Alle kommunizierenden Geräte müssen dasselbe Funkprofil verwenden. Abweichende
Frequenz, Modulationsparameter, Syncword, Header-Modus, CRC- oder
IQ-Einstellung führen typischerweise dazu, dass Frames nicht empfangen oder
als ungültig verworfen werden.

## 2. Hardware- und Transportparameter

| Einstellung | Standard | Gültigkeit | Bedeutung |
| --- | ---: | --- | --- |
| `SpiBusId` | `0` | Ganzzahl ≥ 0 | Nummer des Linux-SPI-Busses. Beim Bonnet ist dies SPI0. |
| `ChipSelectLine` | `1` | Ganzzahl ≥ 0 | Chip-Select innerhalb des SPI-Busses. `1` entspricht bei SPI0 dem Anschluss CE1 beziehungsweise `/dev/spidev0.1`. |
| `ResetPin` | `25` | BCM-GPIO ≥ 0 | Ausgang für den Hardware-Reset des RFM95. |
| `Dio0Pin` | `22` | BCM-GPIO ≥ 0 | Eingang für `TxDone` und `RxDone`. Im normalen Betrieb wird die steigende Flanke ausgewertet. |

`ResetPin` und `Dio0Pin` müssen verschieden sein. Die GPIO-Nummern sind
BCM-Nummern, keine physischen Pin-Nummern des Raspberry-Pi-Headers.

Im Modus `--check` werden keine TX- oder RX-Operationen ausgeführt. Deshalb
wird dort DIO0 nicht angefordert; dies vermeidet Konflikte mit einer bereits
belegten GPIO22-Leitung.

## 3. Frequenz und Modulation

### `FrequencyHertz`

- Standard: `869525000` Hz, also 869,525 MHz.
- Von der Anwendung akzeptierter Bereich: 863 bis 870 MHz einschließlich.
- Legt die Mittenfrequenz des LoRa-Signals fest.

Die reine Bereichsprüfung bedeutet nicht, dass jede Mittenfrequenz mit jeder
Bandbreite und jedem Duty Cycle zulässig ist. Das vollständige belegte Signal
muss innerhalb einer passenden Zuteilung liegen. Bei 125 kHz Bandbreite belegt
ein auf 869,525 MHz zentriertes Signal näherungsweise 869,4625 bis
869,5875 MHz.

### `SpreadingFactor`

Erlaubte Werte:

| Wert | Wirkung |
| --- | --- |
| `Sf7` | höchste Datenrate und kürzeste Time-on-Air der angebotenen Werte |
| `Sf8` bis `Sf11` | stufenweise robustere, aber langsamere Übertragung |
| `Sf12` | höchste Empfindlichkeit und längste Time-on-Air |

Ein höherer Spreading Factor verlängert die Symboldauer exponentiell. Dadurch
steigen Reichweite und Robustheit, aber auch Kanalbelegung,
Duty-Cycle-Wartezeit und Energieverbrauch.

### `Bandwidth`

Erlaubte Werte:

- `Khz125`
- `Khz250`
- `Khz500`

Eine größere Bandbreite erhöht die Datenrate und verkürzt die Time-on-Air,
verringert aber typischerweise die Empfindlichkeit. Die Bandbreite bestimmt
außerdem, welchen Frequenzbereich das Signal um die Mittenfrequenz belegt.

### `CodingRate`

Erlaubte Werte:

- `FourOfFive`
- `FourOfSix`
- `FourOfSeven`
- `FourOfEight`

Mehr Redundanz, beispielsweise `FourOfEight`, verbessert die
Fehlerkorrekturmöglichkeit, erhöht aber die Time-on-Air und senkt den
Nutzdatendurchsatz.

### `OutputPowerDbm`

- Standard: `14` dBm.
- Von der Anwendung akzeptiert: 2 bis 14 dBm.
- Steuert die Ausgangsleistung am PA-Boost-Ausgang des RFM95.

14 dBm entsprechen näherungsweise 25 mW am Senderausgang. Der konfigurierte
Wert ist jedoch nicht automatisch die regulatorisch relevante ERP oder EIRP:
Antennengewinn, Kabel- und Steckerverluste sowie die Bezugsantenne müssen
berücksichtigt werden. Eine Antenne mit Gewinn kann die effektive
Strahlungsleistung über den zulässigen Wert anheben.

#### ERP und EIRP

**ERP** bedeutet *Effective Radiated Power*, auf Deutsch effektive
Strahlungsleistung. Der Wert beschreibt die in die stärkste Abstrahlrichtung
wirkende Leistung relativ zu einem idealen Halbwellendipol.

**EIRP** bedeutet *Equivalent Isotropically Radiated Power*. Dieser Wert
verwendet eine theoretische isotrope, also in alle Richtungen gleichmäßig
strahlende Antenne als Referenz.

Für dieselbe reale Funkanlage gilt näherungsweise:

```text
EIRP in dBm = ERP in dBm + 2,15 dB
```

Beispiele:

| Leistung | ERP | entsprechende EIRP |
| --- | ---: | ---: |
| 25 mW | 14 dBm | ungefähr 16,15 dBm |
| 500 mW | 27 dBm | ungefähr 29,15 dBm |

Vereinfacht ergibt sich die effektive Strahlungsleistung aus:

```text
Senderleistung
- Kabel- und Steckerverluste
+ Antennengewinn gegenüber der jeweiligen Bezugsantenne
```

`OutputPowerDbm` konfiguriert nur den Senderausgang des RFM95. Für einen
regulatorischen Vergleich muss daraus zusammen mit der tatsächlich
verwendeten Antenne ERP beziehungsweise EIRP bestimmt werden. ERP- und
EIRP-Grenzwerte dürfen nicht ohne die Umrechnung von 2,15 dB miteinander
verglichen werden.

### `PreambleSymbols`

- Standard: `8`.
- Minimum der Anwendung: `6`.

Die Präambel ermöglicht dem Empfänger Synchronisation und Paketerkennung.
Mehr Symbole können die Erkennung unter schwierigen Bedingungen verbessern,
verlängern aber jedes Paket.

### `SyncWord`

- Standard: `52` dezimal, entsprechend `0x34`.
- Technischer Wertebereich: 0 bis 255.

Nur Geräte mit passendem Syncword erkennen einander zuverlässig. Das
Syncword trennt Funknetze logisch, bietet aber weder Authentifizierung noch
Verschlüsselung und ist kein Geheimnis.

## 4. Frame- und Empfangseinstellungen

### `PayloadCrcEnabled`

Aktiviert den Payload-CRC des RFM95. Bei `true` verwirft die Anwendung
empfangene Pakete mit ungültigem CRC, bevor das P2P-Frame verarbeitet wird.
Der CRC erkennt Übertragungsfehler, schützt aber nicht vor absichtlicher
Manipulation.

### `ImplicitHeader`

- Standard: `false`.
- `false` verwendet den expliziten LoRa-Header mit Payloadlänge.
- `true` entfernt diesen Header und setzt voraus, dass Sender und Empfänger
  die Paketparameter einschließlich der Länge vorab identisch kennen.

Das P2P-Protokoll verwendet variable Payloadlängen. Deshalb soll
`ImplicitHeader` für den normalen LoRaP2P-Betrieb `false` bleiben.

### `InvertIq`

- Standard: `false`.
- Invertiert bei `true` die LoRa-I/Q-Polarität.

Sender und Empfänger müssen kompatible Einstellungen verwenden. Für direkte
Raw-LoRa-P2P-Kommunikation wird normalerweise auf beiden Seiten `false`
verwendet.

### `OperationTimeout`

- Standard: fünf Sekunden im .NET-`TimeSpan`-Format `00:00:05`.
- Muss größer als null sein.

Der Timeout begrenzt beim Senden die Wartezeit auf die lokale
`TxDone`-Signalisierung über DIO0. Er bestätigt keinen Empfang durch eine
Gegenstelle. Empfangsbefehle und P2P-ACKs besitzen eigene Timeouts.

## 5. Duty Cycle

### Bedeutung

`DutyCycle` ist ein dimensionsloser Anteil:

- Standard: `0.1`, also 10 % im deutschen Band 54 bei
  869,4–869,65 MHz.

| JSON-Wert | Prozent |
| ---: | ---: |
| `0.001` | 0,1 % |
| `0.01` | 1 % |
| `0.1` | 10 % |
| `1.0` | 100 % |

Die EU-Definition verwendet das Verhältnis

```text
Duty Cycle = Summe der Sendezeiten / Beobachtungszeitraum
```

Der reguläre Beobachtungszeitraum beträgt, sofern nicht anders festgelegt,
eine kontinuierliche Stunde. Ein Grenzwert ist eine Obergrenze: Ein kleinerer
Duty Cycle ist konservativer und regulatorisch nicht problematischer.

### Umsetzung in LoRaP2P

Nach jeder Übertragung berechnet der Treiber die erforderliche Sendepause:

```text
Sendepause = Time-on-Air × (1 / DutyCycle - 1)
```

Bei der Standardeinstellung `DutyCycle = 0.1` folgt auf 400 ms Sendezeit eine
Pause von 3,6 Sekunden. Bei `DutyCycle = 0.01` wären es 39,6 Sekunden. Der
nächste lokale Sendevorgang wartet automatisch bis zum berechneten Zeitpunkt.

Diese Begrenzung gilt für alle lokalen Übertragungen:

- normale Raw-LoRa-Pakete,
- P2P-Datenframes,
- Broadcasts,
- ACKs,
- Retries und Error-Frames.

Empfangszeit zählt nicht als Sendezeit. Jedes Gerät führt seine eigene
Begrenzung. Der implementierte Algorithmus erzwingt nach jedem Paket eine
Pause und ist damit bei Burst-Verkehr konservativer als eine reine
Stundenbilanz. Nicht implementiert sind:

- getrennte Airtime-Konten je regulatorischem Teilband,
- ein über eine Stunde angesammeltes und später nutzbares Sendebudget,
- Listen Before Talk (LBT),
- Adaptive Frequency Agility (AFA),
- automatische Auswahl regulatorisch passender Frequenzen,
- automatische Berücksichtigung von Antennengewinn oder nationalen Regeln.

Die Validierung akzeptiert technisch Werte größer als 10 % bis maximal `1.0`.
Das ist keine Aussage über deren rechtliche Zulässigkeit.

## 6. EU868: regulatorische Orientierung

Für das gesamte EU868-Spektrum existiert kein einzelner maximaler Duty Cycle.
Die Grenze hängt von Teilband, Geräteklasse, effektiver Strahlungsleistung,
Bandbreite, Kanalzugriffsverfahren und nationaler Umsetzung ab.

Für nicht-spezifische Short Range Devices wird häufig folgende
ETSI-/CEPT-Teilbandübersicht verwendet:

| Bezeichnung | Frequenzbereich | typischer maximaler Duty Cycle | typische maximale ERP |
| --- | --- | ---: | ---: |
| K | 863,0–865,0 MHz | 0,1 % | 25 mW |
| L | 865,0–868,0 MHz | 1 % | 25 mW |
| M | 868,0–868,6 MHz | 1 % | 25 mW |
| N | 868,7–869,2 MHz | 0,1 % | 25 mW |
| P | 869,4–869,65 MHz | 10 % | 500 mW |
| Q | 869,7–870,0 MHz | 1 % | 25 mW |

Die Zwischenbereiche sind in dieser vereinfachten Tabelle nicht als
allgemeine Ausweichfrequenzen freigegeben. Sie können anderen Anwendungen
oder Bedingungen zugeordnet sein.

### Standardkonfiguration für Deutschland: 869,4–869,65 MHz

In Deutschland gilt seit November 2025 die Allgemeinzuteilung
**Vfg. 91/2025** der Bundesnetzagentur. Sie ersetzt unter anderem die frühere
Vfg. 133/2019 und ist bis zum 31. Dezember 2035 befristet.

Band 54 der Vfg. 91/2025 legt für 869,4–869,65 MHz fest:

| Merkmal | Bedingung |
| --- | --- |
| Gerätekategorie | Geräte geringer Reichweite für nicht näher spezifizierte Anwendungen |
| maximale Sendeleistung | 500 mW ERP |
| Kanalzugang | geeignete Frequenzzugangs- und Störungsminderungstechniken |
| Alternative zum Kanalzugangsverfahren | Arbeitszyklus ≤ 10 % |

Die Definition dieser Gerätekategorie nennt allgemeine Datenübertragung
ausdrücklich als übliche Verwendung. Raw-LoRa-P2P kann daher grundsätzlich
unter Band 54 betrieben werden, sofern sämtliche technischen Bedingungen
eingehalten werden.

LoRaP2P implementiert derzeit kein qualifiziertes Frequenzzugangs- oder
Störungsminderungsverfahren. Für die vorhandene Implementierung ist deshalb
die ausdrücklich genannte Alternative `DutyCycle = 0.1` maßgeblich.

Die Standardkonfiguration verwendet mit dem vorhandenen 125-kHz-Profil:

```json
{
  "FrequencyHertz": 869525000,
  "Bandwidth": "Khz125",
  "OutputPowerDbm": 14,
  "DutyCycle": 0.1
}
```

Das auf 869,525 MHz zentrierte 125-kHz-Signal belegt näherungsweise
869,4625–869,5875 MHz und liegt damit innerhalb des zugeteilten Teilbands.
500 mW ERP sind eine Obergrenze, keine erforderliche Sendeleistung. Der
Treiber begrenzt den RFM95-Ausgang weiterhin auf 14 dBm, also ungefähr 25 mW
vor Berücksichtigung von Antenne und Verlusten.

Die Änderung von 1 % auf 10 % verkürzt die vorgeschriebene Pause von der
99-fachen auf die 9-fache Time-on-Air. Bei unverändertem SF7/BW125/CR-4/5-Profil
steigt dadurch der langfristige maximale Nutzdatendurchsatz ungefähr von
6,1 Byte/s auf 60,8 Byte/s. Die momentane physikalische Datenrate ändert sich
nicht.

Weiterhin zu beachten:

- Die gesamte Aussendung muss im Teilband bleiben; BW500 ist dafür zu breit.
- Antennengewinn und Verluste müssen in die ERP-Berechnung eingehen.
- Daten, ACKs, Retries und Broadcasts zählen jeweils zum Arbeitszyklus des
  sendenden Geräts.
- Die Frequenzen werden nicht exklusiv, nichtstörend und ungeschützt genutzt.
  Es besteht weder Störungsschutz noch eine garantierte Übertragungsqualität.
- Die Funkanlage muss das Funkanlagengesetz und die anwendbaren
  harmonisierten Normen erfüllen.
- Der Nutzer ist laut Allgemeinzuteilung für die Einhaltung der Bedingungen
  verantwortlich.

### Alternative Kanalzugriffsverfahren

Bestimmte regulatorische Zuteilungen erlauben statt eines festen
Duty-Cycle-Grenzwerts geeignete Spektrumzugangs- und
Störungsminderungstechniken, beispielsweise LBT mit AFA. Das ist keine
pauschale Freigabe für Dauerbetrieb. Das Verfahren muss die Anforderungen der
einschlägigen harmonisierten Norm und der konkreten Zuteilung erfüllen.

LoRaP2P implementiert derzeit kein LBT/AFA. Das bloße Lauschen mit
`ReceiveSingleAsync` ist kein normkonformes LBT-Verfahren. Deshalb darf der
Duty Cycle nicht mit der Begründung erhöht werden, die Anwendung könne
grundsätzlich empfangen.

### Nationale Prüfung

Die obige Bewertung bestätigt die deutsche Frequenzzuteilung, ersetzt aber
keine Konformitätsbewertung der konkreten Funkanlage. Vor einem produktiven
Betrieb sind mindestens zu prüfen:

1. Gerätekonformität nach Funkanlagengesetz und Radio Equipment Directive,
2. anwendbare harmonisierte Norm und Messverfahren,
3. effektive Strahlungsleistung der vollständigen Antennenanlage,
4. Aktualität und Fortgeltung der Vfg. 91/2025.

## 7. Quellen und Stand

Stand dieser Dokumentation: 20. September 2026.

- Europäische Kommission, Durchführungsbeschluss (EU) 2022/172 mit der
  Definition des Duty Cycle und harmonisierten technischen Bedingungen:
  <https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022D0172>
- CEPT, aktuelle Einstiegsseite zu ERC Recommendation 70-03 und Verweis auf
  EFIS für nationale Umsetzungen:
  <https://docdb.cept.org/document/845>
- CEPT EFIS, nationale SRD-Regelungen:
  <https://efis.cept.org/sitecontent.jsp?sitecontent=srd_regulations>
- Bundesnetzagentur, Allgemeinzuteilung für SRD, Vfg. 91/2025, insbesondere
  Tabelle 1, Band 54 in Tabelle 2 und Erläuterung [7]:
  <https://www.bundesnetzagentur.de/DE/Fachthemen/Telekommunikation/Frequenzen/Allgemeinzuteilungen/_DL/vfg91_2025.pdf?__blob=publicationFile&v=3>
- ETSI EN 300 220-2, harmonisierte Norm für Short Range Devices:
  <https://www.etsi.org/deliver/etsi_en/300200_300299/30022002/>
- The Things Network, zusammenfassende EU863-870-Teilbandtabelle mit
  Verweisen auf ETSI EN 300 220:
  <https://www.thethingsnetwork.org/docs/lorawan/regional-parameters/eu868/>

Die zusammenfassende Teilbandtabelle dient der technischen Orientierung.
Bei Abweichungen haben aktuelle EU-Vorgaben, nationale Regelungen und die
anwendbare harmonisierte Norm Vorrang.
