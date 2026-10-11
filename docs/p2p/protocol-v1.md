# P2P Protocol Version 1

## 1. Principles

The protocol is carried directly in the RFM95 payload. The SX1276 handles
LoRa modulation and payload CRC; the P2P protocol adds addressing, sequence
numbers, and message types.

- Maximum RFM95 payload: 255 bytes
- Fixed header length: 12 bytes
- Maximum unencrypted application payload: 243 bytes
- Multi-byte fields: Big Endian
- Half-duplex operation: transmission and reception never occur simultaneously

## 2. Binary Frame Format

| Offset | Size | Field | Encoding |
| ---: | ---: | --- | --- |
| 0 | 1 | Protocol version | `0x01` for this specification |
| 1 | 1 | Message type | `Data=0x01`, `Ack=0x02`, `Error=0x03` |
| 2 | 1 | Flags | Always `0x00` in version 1 |
| 3 | 2 | Sender ID | `UInt16`, Big Endian |
| 5 | 2 | Recipient ID | `UInt16`, Big Endian |
| 7 | 4 | Sequence number | `UInt32`, Big Endian |
| 11 | 1 | Payload length | Number of following bytes |
| 12 | 0–243 | Payload | Depends on message type |

The total frame size must be exactly `12 + payload length`. Additional or
missing bytes make the frame invalid.

## 3. IDs

- Valid device IDs range from 1 to 65534.
- `0xFFFF` addresses all devices as a broadcast.
- `0x0000` is invalid and must not be sent as either sender or regular recipient.
- A device processes only frames addressed to its own ID or to `0xFFFF`.
- Frames with the local ID as sender are discarded.

## 4. Message Types

### 4.1 Data

- Payload length: 1 to 243 bytes.
- Directly addressed frames are answered with an `Ack`.
- Broadcast frames are never acknowledged.
- Interpretation as text or binary data is a CLI property and not part of the wire format.

### 4.2 Ack

- Payload length must be 0.
- Sender and recipient are swapped relative to the acknowledged `Data` frame.
- The sequence number exactly matches the acknowledged `Data` frame.
- ACKs to broadcasts are invalid.
- An ACK confirms only technically valid reception of the P2P frame.

### 4.3 Error

An `Error` may be generated only in response to a syntactically valid,
directly addressed frame that cannot be processed. Invalid, damaged, or
foreign-addressed frames are silently discarded.

The Error payload consists of:

| Offset | Size | Field |
| ---: | ---: | --- |
| 0 | 1 | Error code |
| 1 | 0–242 | Optional UTF-8 diagnostic without secrets |

Version 1 error codes:

| Value | Meaning |
| ---: | --- |
| 1 | Message type or operation not supported |
| 2 | Recipient temporarily busy |
| 3 | Internal processing failed |

`Error` does not count as delivery confirmation.

## 5. Frame Validation

A recipient discards a frame without ACK if at least one condition applies:

- RFM95 payload CRC is invalid,
- frame is shorter than 12 bytes,
- version is not `0x01`,
- flags are not `0x00`,
- message type is unknown,
- sender ID is 0 or `0xFFFF`,
- recipient ID is 0,
- declared and actual payload lengths differ,
- message type payload rules are violated,
- recipient ID is neither local nor broadcast,
- sender ID matches the local ID.

Parser errors must not return partially initialized frames.

## 6. Sequence Numbers

- Each locally generated `Data` frame receives a new `UInt32` sequence number.
- The initial sequence number is selected cryptographically at random on each  process start.
- 0 follows `UInt32.MaxValue`.
- Retries use the same sequence number and the same frame content.
- `Ack` and `Error` use the sequence number of the triggering frame.

The sequence number alone provides no replay protection and must not later be
used as an AES-GCM nonce.

## 7. ACK and Retry Flow

1. The sender transmits a directly addressed `Data` frame.
2. It then waits up to 2 seconds for a matching ACK.
3. Matching means: type `Ack`, expected sender ID, local recipient ID, and
   identical sequence number.
4. Other valid frames are processed during the wait, but do not extend the ACK timeout.
5. After a timeout, the sender waits randomly for 250 to 1000 ms.
6. It then retransmits the same frame.
7. A maximum of three retries is allowed after the first attempt, for a total
   of at most four transmission attempts.
8. After the final timeout, an explicit delivery failure is reported.

The duty-cycle wait already implemented in the RFM9x driver applies to every
attempt and every ACK. Backoff does not replace this wait. Calculation,
configuration effect, implementation limits, and regulatory context are
described in the [radio configuration documentation](../radio-configuration.md#5-duty-cycle).

## 8. ACK Generation

A valid directly addressed `Data` frame is answered after 100 ms. The short
wait allows the sender to switch from TX to RX.

A duplicate is acknowledged again but not delivered to the application again.
This allows a lost ACK to be recovered by a retry.

## 9. Duplicate Detection

The recipient keeps a maximum of 64 combinations of sequence number and
reception time per sender. An entry is considered known for 10 minutes.

- Known frame: send ACK again, do not deliver again.
- New frame: deliver, record, and send ACK.
- Oldest entries are removed first.
- The cache is session-specific and is not persisted.

The random initial sequence number reduces collisions after a restart.

## 10. Simultaneous Transmission Attempts

Overlapping radio transmissions can collide. If this causes ACKs to be lost,
random backoffs distribute retries over time. Collision freedom is not
guaranteed.

All P2P transactions on a device are serialized. A second local send
transaction waits until the first is complete or has failed.
