# Test and Operations Plan

## 1. P2P Configuration

P2P data is stored separately from the radio configuration. The default path
is `p2p.json` in the application directory. An optional startup parameter
`--peer-config <path>` overrides the path.

Planned format:

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

Validation:

- local ID must be 1 to 65534,
- peer IDs must be 1 to 65534,
- local ID must not be in `Peers`,
- peer IDs are unique,
- ACK timeout and time spans must be positive,
- `BackoffMinimum` must not exceed `BackoffMaximum`,
- `MaxRetries` must be 0 to 10,
- cache size must be 1 to 1024.

The file contains no keys. Write operations are atomic. An error while loading
or saving is not hidden by empty defaults.

## 2. CLI Behavior

### Peer Management

```text
peer id 1
peer add 2
peers
```

- `peer id` replaces the local ID and saves immediately.
- `peer add` is idempotent for IDs that already exist.
- Broadcast and ID 0 are rejected as peers.
- `peers` shows configured IDs and the session-specific last reception, RSSI,
  SNR, and last sequence number.

### Receiving

```text
listen
```

- displays a clear notice about unencrypted operation,
- runs until Ctrl+C,
- shows sender, sequence number, text/hex, RSSI, SNR, and duplicate status,
- acknowledges valid directly addressed data,
- does not acknowledge broadcasts,
- terminates the entire application on Ctrl+C.

### Addressed Transmission

```text
send-to 2 text "hello world"
send-to 2 hex DEADBEEF
```

Success output contains:

- peer ID,
- sequence number,
- attempt count,
- time-on-air of the last attempt,
- ACK latency.

After retries are exhausted, a delivery failure is displayed. `TxDone` alone
does not count as delivery.

### Broadcast

```text
broadcast text "hello all"
```

Broadcast reports only the local `TxDone` completion and explicitly indicates
that no ACK is expected.

Received broadcast text also appears on the OLED. Four lines of up to 21
characters each are available. Longer messages scroll upward one line at a
time at one-second intervals and leave the last page visible. A new broadcast
message replaces the active display. Directly addressed messages do not change
the OLED.

### Statistics

```text
stats
```

Statistics apply to the current process session and are not persisted. A reset
command is not planned for the first P2P release.

## 3. Message Persistence and Chat Export

On the first transmission or reception, a file is opened for each combination
of local device ID and peer in the default directory `~/LoRaP2P/sessions`.
Writers remain open until the process ends and write UTF-8 without a BOM.
Files can be read and exported while open. Broadcasts are kept separately in a
file with `peerbroadcast` in its name.

File names follow the pattern
`yyyyMMddTHHmmssfffZ-node<local>-peer<peer>.jsonl`; in case of a collision,
a numeric suffix is added. Each non-empty line contains exactly one JSON object:

| Field | Meaning |
| --- | --- |
| `timestamp` | UTC timestamp |
| `direction` | `incoming`, `outgoing`, or `broadcast` |
| `localNodeId`, `peerNodeId` | local ID and peer |
| `sequenceNumber` | P2P frame sequence number |
| `payloadType` | `text` for valid UTF-8, otherwise `hex` |
| `text` | text representation or `null` |
| `hex` | complete hexadecimal representation |
| `status` | `received`, `acknowledged`, `failed`, or `sent` |
| `attempts` | transmission attempts, if applicable |
| `rssiDbm`, `snrDb` | reception values, if applicable |

```text
export-chat ~/LoRaP2P/sessions/<session>.jsonl
export-chat ~/LoRaP2P/sessions/<session>.jsonl ./chat.html
```

Without a second path, the HTML file is created next to the JSONL file. The
export is standalone, encodes message content for HTML, and reports the line
number of an invalid JSON object.

## 4. Unit Test Matrix

### Frame Codec

- round trip for `Data`, `Ack`, and `Error`,
- minimum and maximum payload,
- incorrect version,
- unknown type,
- set reserved flags,
- invalid IDs,
- header that is too short,
- mismatched payload length,
- ACK with payload,
- broadcast ACK,
- Big Endian test values.

### Peer and Configuration State

- loading a valid file,
- missing file with clearly defined initial state,
- invalid JSON,
- semantically invalid values,
- atomic saving,
- duplicate peer,
- local ID as peer,
- I/O error while saving.

### Reception

- new directly addressed frame is delivered once and acknowledged,
- duplicate is acknowledged but not delivered,
- broadcast is delivered but not acknowledged,
- foreign recipient ID is discarded,
- CRC error is counted and discarded,
- invalid frame is counted and discarded,
- ACK and Error are not delivered as payload,
- cancellation ends reception and leaves standby.

### Reliable Transmission

- matching ACK on the first attempt,
- non-matching sender ID,
- non-matching sequence number,
- ACK timeout and successful retry,
- three retries after the first attempt,
- delivery failure after four attempts,
- backoff is within the configured range,
- broadcast does not wait for ACK,
- cancellation during TX, ACK wait, and backoff,
- other valid frames during the ACK wait.

### CLI

- all new verbs are recognized,
- quoted text is preserved,
- hexadecimal payload is parsed correctly,
- invalid IDs and hexadecimal data produce understandable errors,
- output distinguishes `TxDone`, ACK, and delivery failure,
- `peers` and `stats` correctly represent service state.
- only broadcasts are forwarded to the OLED display,
- explicit line breaks, word wrapping, and long words are correctly distributed
  across display lines 21 characters wide,
- transmission, reception, and broadcast create the expected JSONL sessions,
- export works with the default and explicit target paths,
- invalid JSON lines are rejected with their line number,
- HTML special characters in messages are not interpreted as markup.

## 5. Test Doubles

The scriptable radio fake provides:

- a queue for reception results,
- a list of transmitted payloads,
- controllable `TxDone` points in time,
- timeouts,
- CRC-valid and CRC-invalid packets,
- cancellation.

Time and randomness are injected. Tests must not actually wait two seconds for
ACK timeouts or duty-cycle pauses.

## 6. Hardware Acceptance with Two Devices

### Preparation

- configure both devices with the same radio profile,
- assign device A ID 1 and peer 2,
- assign device B ID 2 and peer 1,
- connect a suitable 868-MHz antenna to both devices,
- initially place them a few meters apart without direct shielding,
- record the commit, publication time, and configuration files.

### Test Cases

1. A listens; B sends text to A.
2. B listens; A sends binary data to B.
3. Both directions provide a uniquely matched ACK.
4. A broadcast is received and not acknowledged.
5. The first ACK is deliberately discarded; the retry is delivered only once.
6. The recipient is switched off; after four attempts, the sender reports a
  delivery failure.
7. Both devices transmit as simultaneously as possible; backoff temporally
  separates at least one later attempt.
8. A mismatched frequency causes a timeout.
9. A mismatched syncword causes a timeout.
10. Ctrl+C during `listen` exits cleanly and leaves standby.

### Values to Record

- result and timestamp,
- sequence number,
- attempt count,
- time-on-air,
- ACK latency,
- RSSI and SNR,
- relevant statistics counters,
- observed duty-cycle pause.

## 7. Release Validation

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
