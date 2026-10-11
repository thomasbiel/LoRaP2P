# Optional AES-GCM Extension

## Status

AES-GCM is not part of the first P2P release. The unencrypted version is
implemented and accepted with two devices first. This file records the
security requirements for a separate follow-up release.

## Threat Model

Without encryption, a third party within radio range can:

- read payloads,
- create frames with foreign sender IDs,
- replay recorded frames,
- manipulate content.

LoRa payload CRC protects only against transmission errors and provides no
authentication.

## Planned Method

- AES-GCM with a 256-bit key,
- 12-byte nonce,
- 16-byte authentication tag,
- protocol header as Additional Authenticated Data,
- encryption of the application payload only,
- no ACK for frames with an invalid tag.

An encrypted Data content consists of:

```text
Nonce (12 Byte) | Ciphertext (0–215 Byte) | Tag (16 Byte)
```

With the unchanged RFM95 limit of 255 bytes and a 12-byte protocol header, a
maximum of 215 bytes of plaintext remains.

## Nonce Strategy

The sequence number is not sufficient as a nonce. The planned strategy is:

- 4-byte random salt persisted per device installation,
- 8-byte persistent monotonically increasing counter,
- atomic reservation of counter ranges before use.

A process crash may cause nonces to be skipped, but never reused. Counter
overflow blocks further encrypted transmission and requires key rotation.

## Key Storage

- Never store keys in `appsettings.json`, `p2p.json`, logs, or the repository.
- Load them through a separate file or a platform secret store.
- The Linux file must be readable only by the service user, typically mode
  `0600`.
- Incorrect file permissions produce at least a clear warning and should
  prevent startup in unattended operation.
- Key material must not appear in error messages or diagnostics.

## Protocol Extension

Flag bit 0 will identify an AES-GCM payload in the future. Recipients without
implemented encryption discard set flags according to Protocol Version 1.
Before activation, decide whether:

- Version 1 is extended with the flag, or
- protocol version 2 is introduced.

A new version is preferable if ACK or Error authentication also changes.

## Additional Tests

- successful encrypt/decrypt round trip,
- incorrect key,
- manipulated headers, ciphertexts, nonces, and tags,
- reused nonce,
- persistent counter after restart,
- simulated crashes during counter reservation,
- maximum encrypted payload,
- no ACKs for unauthenticated frames,
- no keys or plaintext in logs.
