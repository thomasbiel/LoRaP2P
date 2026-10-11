# Radio Configuration (`appsettings.json`)

This file documents all radio configuration settings. The included
[`appsettings.json`](../src/LoRaP2P.Console/appsettings.json) is preconfigured
for an Adafruit RFM95W Radio Bonnet on a Raspberry Pi and Raw LoRa at
869.525 MHz.

> This configuration is not an automatic compliance check. In particular, the
> application does not validate whether frequency, occupied bandwidth, antenna
> gain, effective radiated power, duty cycle, and device class together satisfy
> all regulatory requirements in the country of operation.

## 1. Default Configuration

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

All communicating devices must use the same radio profile. A different
frequency, modulation parameter, sync word, header mode, CRC setting, or IQ
setting will typically prevent frames from being received or cause them to be
discarded as invalid.

## 2. Hardware and Transport Parameters

| Setting | Default | Validity | Meaning |
| --- | ---: | --- | --- |
| `SpiBusId` | `0` | Integer ≥ 0 | Linux SPI bus number. On the Bonnet, this is SPI0. |
| `ChipSelectLine` | `1` | Integer ≥ 0 | Chip select within the SPI bus. On SPI0, `1` corresponds to CE1 or `/dev/spidev0.1`. |
| `ResetPin` | `25` | BCM GPIO ≥ 0 | Output for the RFM95 hardware reset. |
| `Dio0Pin` | `22` | BCM GPIO ≥ 0 | Input for `TxDone` and `RxDone`. During normal operation, the rising edge is evaluated. |

`ResetPin` and `Dio0Pin` must be different. GPIO numbers are BCM numbers, not
physical pin numbers on the Raspberry Pi header.

In `--check` mode, no TX or RX operations are performed. DIO0 is therefore not
requested in that mode, avoiding conflicts with an already-occupied GPIO22
line.

## 3. Frequency and Modulation

### `FrequencyHertz`

- Default: `869525000` Hz, or 869.525 MHz.
- Range accepted by the application: 863 to 870 MHz inclusive.
- Sets the LoRa signal's center frequency.

The range check alone does not mean that every center frequency is permitted
with every bandwidth and duty cycle. The complete occupied signal must remain
within an appropriate allocation. With 125 kHz bandwidth, a signal centered at
869.525 MHz occupies approximately 869.4625 to 869.5875 MHz.

### `SpreadingFactor`

Allowed values:

| Value | Effect |
| --- | --- |
| `Sf7` | highest data rate and shortest time-on-air of the available values |
| `Sf8` to `Sf11` | progressively more robust but slower transmission |
| `Sf12` | highest sensitivity and longest time-on-air |

A higher spreading factor increases symbol duration exponentially. This
increases range and robustness, but also channel occupancy, duty-cycle waiting
time, and energy consumption.

### `Bandwidth`

Allowed values:

- `Khz125`
- `Khz250`
- `Khz500`

A wider bandwidth increases data rate and shortens time-on-air, but typically
reduces sensitivity. Bandwidth also determines the frequency range occupied by
the signal around the center frequency.

### `CodingRate`

Allowed values:

- `FourOfFive`
- `FourOfSix`
- `FourOfSeven`
- `FourOfEight`

More redundancy, such as `FourOfEight`, improves error-correction capability,
but increases time-on-air and reduces payload throughput.

### `OutputPowerDbm`

- Default: `14` dBm.
- Accepted by the application: 2 to 17 dBm.
- Controls output power at the RFM95 PA boost output.

14 dBm corresponds to approximately 25 mW at the transmitter output. However,
the configured value is not automatically the ERP or EIRP relevant to
regulation: antenna gain, cable and connector losses, and the reference
antenna must be taken into account. A gain antenna can raise effective
radiated power above the permitted value.

17 dBm corresponds to approximately 50 mW and is the upper limit of normal PA
boost operation. The +20 dBm mode supported by the RFM95 requires additional
register settings and stricter operating conditions; the driver does not enable
it.

#### ERP and EIRP

**ERP** means *Effective Radiated Power*. It describes the power in the
direction of strongest radiation relative to an ideal half-wave dipole.

**EIRP** means *Equivalent Isotropically Radiated Power*. It uses a theoretical
isotropic antenna, radiating evenly in all directions, as the reference.

For the same physical radio system, approximately:

```text
EIRP in dBm = ERP in dBm + 2.15 dB
```

Examples:

| Power | ERP | corresponding EIRP |
| --- | ---: | ---: |
| 25 mW | 14 dBm | approximately 16.15 dBm |
| 500 mW | 27 dBm | approximately 29.15 dBm |

In simplified form, effective radiated power is calculated as:

```text
Transmitter power
- Cable and connector losses
+ Antenna gain relative to the applicable reference antenna
```

`OutputPowerDbm` configures only the RFM95 transmitter output. For a regulatory
comparison, ERP or EIRP must be determined from it together with the antenna
actually used. ERP and EIRP limits must not be compared without converting by
2.15 dB.

### `PreambleSymbols`

- Default: `8`.
- Application minimum: `6`.

The preamble enables receiver synchronization and packet detection. More
symbols can improve detection under difficult conditions, but lengthen every
packet.

### `SyncWord`

- Default: `52` decimal, corresponding to `0x34`.
- Technical range: 0 to 255.

Only devices with a matching sync word reliably recognize one another. The sync
word logically separates radio networks, but provides neither authentication
nor encryption and is not secret.

## 4. Frame and Receive Settings

### `PayloadCrcEnabled`

Enables the RFM95 payload CRC. When `true`, the application discards received
packets with an invalid CRC before processing the P2P frame. The CRC detects
transmission errors but does not protect against deliberate manipulation.

### `ImplicitHeader`

- Default: `false`.
- `false` uses the explicit LoRa header with payload length.
- `true` removes this header and requires sender and receiver to know the packet
  parameters, including length, identically in advance.

The P2P protocol uses variable payload lengths. Therefore, `ImplicitHeader`
should remain `false` for normal LoRaP2P operation.

### `InvertIq`

- Default: `false`.
- When `true`, inverts LoRa I/Q polarity.

Sender and receiver must use compatible settings. Direct Raw LoRa P2P
communication normally uses `false` on both sides.

### `OperationTimeout`

- Default: five seconds in .NET `TimeSpan` format `00:00:05`.
- Must be greater than zero.

During transmission, the timeout limits the wait for the local `TxDone` signal
through DIO0. It does not confirm reception by a peer. Receive commands and P2P
ACKs have their own timeouts.

## 5. Duty Cycle

### Meaning

`DutyCycle` is a dimensionless fraction:

- Default: `0.1`, or 10% in German band 54 at
  869.4–869.65 MHz.

| JSON value | Percent |
| ---: | ---: |
| `0.001` | 0.1% |
| `0.01` | 1% |
| `0.1` | 10% |
| `1.0` | 100% |

The EU definition uses the ratio

```text
Duty Cycle = Sum of transmission times / observation period
```

Unless specified otherwise, the regular observation period is one continuous
hour. A limit is an upper bound: a lower duty cycle is more conservative and
does not create a regulatory problem.

### Implementation in LoRaP2P

After each transmission, the driver calculates the required transmission pause:

```text
Transmission pause = Time-on-Air × (1 / DutyCycle - 1)
```

With the default setting `DutyCycle = 0.1`, 400 ms of transmission time is
followed by a 3.6-second pause. With `DutyCycle = 0.01`, it would be 39.6
seconds. The next local transmission waits automatically until the calculated
time.

This limit applies to all local transmissions:

- normal Raw LoRa packets,
- P2P data frames,
- broadcasts,
- ACKs,
- retries and error frames.

Receive time does not count as transmission time. Each device maintains its own
limit. The implemented algorithm enforces a pause after every packet and is
therefore more conservative for burst traffic than a pure hourly balance. The
following are not implemented:

- separate airtime accounts for each regulatory sub-band,
- a transmission budget accumulated over an hour and available later,
- Listen Before Talk (LBT),
- Adaptive Frequency Agility (AFA),
- automatic selection of regulatory-compliant frequencies,
- automatic consideration of antenna gain or national rules.

Validation technically accepts values above 10% up to a maximum of `1.0`.
This is not a statement about their legal permissibility.

## 6. EU868: Regulatory Guidance

There is no single maximum duty cycle for the entire EU868 spectrum. The limit
depends on sub-band, device class, effective radiated power, bandwidth, channel
access method, and national implementation.

The following ETSI/CEPT sub-band overview is commonly used for non-specific
Short Range Devices:

| Designation | Frequency range | typical maximum duty cycle | typical maximum ERP |
| --- | --- | ---: | ---: |
| K | 863.0–865.0 MHz | 0.1% | 25 mW |
| L | 865.0–868.0 MHz | 1% | 25 mW |
| M | 868.0–868.6 MHz | 1% | 25 mW |
| N | 868.7–869.2 MHz | 0.1% | 25 mW |
| P | 869.4–869.65 MHz | 10% | 500 mW |
| Q | 869.7–870.0 MHz | 1% | 25 mW |

The intermediate ranges are not approved as general fallback frequencies in
this simplified table. They may be assigned to other applications or
conditions.

### Default Configuration for Germany: 869.4–869.65 MHz

In Germany, the **Vfg. 91/2025** general allocation of the Federal Network
Agency has applied since November 2025. Among other things, it replaces the
former Vfg. 133/2019 and is limited until December 31, 2035.

Band 54 of Vfg. 91/2025 specifies the following for 869.4–869.65 MHz:

| Feature | Condition |
| --- | --- |
| Device category | Short-range devices for non-specific applications |
| maximum transmit power | 500 mW ERP |
| channel access | appropriate spectrum access and interference mitigation techniques |
| alternative to the channel access method | duty cycle ≤ 10% |

The definition of this device category explicitly names general data
transmission as a typical use. Raw LoRa P2P can therefore generally operate
under band 54, provided that all technical conditions are met.

LoRaP2P currently implements no qualified spectrum access or interference
mitigation method. For the current implementation, the explicitly named
alternative `DutyCycle = 0.1` is therefore applicable.

The default configuration uses the existing 125 kHz profile:

```json
{
  "FrequencyHertz": 869525000,
  "Bandwidth": "Khz125",
  "OutputPowerDbm": 14,
  "DutyCycle": 0.1
}
```

The 125 kHz signal centered at 869.525 MHz occupies approximately
869.4625–869.5875 MHz and is therefore within the allocated sub-band. 500 mW
ERP is an upper limit, not a required transmit power. The default still
operates the RFM95 output at 14 dBm, or approximately 25 mW before accounting
for antenna and losses. Up to 17 dBm, or approximately 50 mW, can be
configured.

Changing from 1% to 10% reduces the required pause from 99 times to 9 times
time-on-air. With the SF7/BW125/CR-4/5 profile unchanged, this increases the
long-term maximum payload throughput from approximately 6.1 bytes/s to 60.8
bytes/s. The instantaneous physical data rate does not change.

The following also remains important:

- The entire transmission must remain within the sub-band; BW500 is too wide
  for this.
- Antenna gain and losses must be included in the ERP calculation.
- Data, ACKs, retries, and broadcasts each count toward the transmitting
  device's duty cycle.
- The frequencies are used non-exclusively, without interference protection,
  and without protection from interference. There is neither interference
  protection nor guaranteed transmission quality.
- The radio equipment must comply with the German Radio Equipment Act and the
  applicable harmonized standards.
- Under the general allocation, the user is responsible for complying with the
  conditions.

### Alternative Channel Access Methods

Some regulatory allocations permit appropriate spectrum access and interference
mitigation techniques, such as LBT with AFA, instead of a fixed duty-cycle
limit. This is not a general authorization for continuous operation. The method
must meet the requirements of the relevant harmonized standard and the specific
allocation.

LoRaP2P currently implements no LBT/AFA. Merely listening with
`ReceiveSingleAsync` is not a standards-compliant LBT method. Therefore, the
duty cycle must not be increased on the grounds that the application can in
principle receive.

### National Review

The assessment above confirms the German frequency allocation, but does not
replace a conformity assessment of the specific radio equipment. Before
production operation, at least the following must be checked:

1. equipment conformity under the German Radio Equipment Act and the Radio Equipment Directive,
2. applicable harmonized standard and measurement methods,
3. effective radiated power of the complete antenna system,
4. current validity and continued applicability of Vfg. 91/2025.

## 7. Sources and Status

Documentation status: September 20, 2026.

- European Commission, Implementing Decision (EU) 2022/172 with the definition
  of duty cycle and harmonized technical conditions:
  <https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022D0172>
- CEPT, current entry page for ERC Recommendation 70-03 and link to EFIS for
  national implementations:
  <https://docdb.cept.org/document/845>
- CEPT EFIS, national SRD regulations:
  <https://efis.cept.org/sitecontent.jsp?sitecontent=srd_regulations>
- Federal Network Agency, general allocation for SRD, Vfg. 91/2025, in
  particular Table 1, band 54 in Table 2, and note [7]:
  <https://www.bundesnetzagentur.de/DE/Fachthemen/Telekommunikation/Frequenzen/Allgemeinzuteilungen/_DL/vfg91_2025.pdf?__blob=publicationFile&v=3>
- ETSI EN 300 220-2, harmonized standard for Short Range Devices:
  <https://www.etsi.org/deliver/etsi_en/300200_300299/30022002/>
- The Things Network, summary EU863-870 sub-band table with references to ETSI
  EN 300 220:
  <https://www.thethingsnetwork.org/docs/lorawan/regional-parameters/eu868/>

The summary sub-band table is provided for technical guidance. In case of
discrepancies, current EU requirements, national regulations, and the
applicable harmonized standard take precedence.
