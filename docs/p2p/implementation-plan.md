# Implementation Plan for the P2P Extension

## Overview

The phases build on one another. A phase is considered complete only when its
tests and documentation changes are included.

| Phase | Outcome | Status |
| ---: | --- | --- |
| 0 | Bidirectional Raw LoRa baseline test | Open |
| 1 | Project scaffold and protocol models | Implemented |
| 2 | Frame codec | Implemented |
| 3 | Configuration, peers, and statistics | Implemented |
| 4 | Reception, ACK, and duplicates | Implemented |
| 5 | Reliable transmission and broadcast | Implemented |
| 6 | Console commands from PRD 12.6 | Implemented |
| 7 | JSONL session persistence and HTML export | Implemented |
| 8 | System test, documentation, and release | Software validated, hardware open |
| 9 | Optional AES-GCM extension | Planned |

## Phase 0: Confirm Hardware Baseline

Tasks:

- configure two devices with identical radio profiles,
- transmit text and binary data in both directions,
- validate RSSI and SNR plausibility,
- verify timeout with an incorrect frequency,
- verify timeout with an incorrect syncword,
- confirm cancellation and timeout followed by standby.

Completion criterion:

- All hardware-related acceptance criteria from PRD 10 are recorded.

## Phase 1: Project and Public Models

Tasks:

- create `LoRaP2P.Protocol` and `LoRaP2P.Protocol.Tests`,
- add both projects to `LoRaP2P.slnx`,
- define `P2pNodeId` with range 1–65534,
- define message types and immutable `P2pFrame`,
- model options for ACK, retry, backoff, and duplicate window,
- retain nullable and warnings-as-errors.

Completion criterion:

- Solution builds; models reject invalid IDs and options.

## Phase 2: Frame Codec

Tasks:

- implement encoding according to the [protocol specification](protocol-v1.md),
- implement decoding without partial results,
- centralize Big Endian conversion,
- enforce maximum payload sizes,
- validate type-specific payload rules,
- reject unknown versions, flags, and types.

Completion criterion:

- All valid frame types pass round-trip tests.
- Each validation rule has at least one negative test.

## Phase 3: Configuration, Peers, and Statistics

Tasks:

- implement `P2pConfiguration` and validation,
- add separate JSON file and `--peer-config`,
- implement atomic configuration store,
- persistently manage local ID and peer IDs,
- model session-specific peer metadata,
- implement thread-safe statistics counters,
- implement sequence number generator with random initial value,
- implement bounded duplicate cache per peer.

Completion criterion:

- Configuration changes survive a reload.
- Invalid files produce understandable errors instead of defaults.
- Statistics and duplicate cache are tested deterministically.

## Phase 4: Reception, ACK, and Duplicates

Tasks:

- implement `P2pNode.ListenAsync`,
- discard CRC errors before the frame decoder,
- handle local and broadcast addressing,
- deliver new data,
- acknowledge duplicates again, but do not deliver them again,
- send ACK after 100 ms,
- discard foreign and invalid frames without ACK,
- update peer metadata and statistics,
- propagate cancellation through to radio standby.

Completion criterion:

- Reception, ACK, and duplicate cases pass without hardware.

## Phase 5: Reliable Transmission and Broadcast

Tasks:

- implement addressed `SendAsync`,
- match ACK using peer ID and sequence number,
- enforce a fixed total ACK timeout of 2 seconds per attempt,
- implement up to three retries,
- choose backoff from 250 to 1000 ms using injectable randomness,
- process other valid frames during the ACK wait,
- report delivery failure after exhausting attempts,
- implement broadcast without ACK or retry,
- use the existing radio's duty-cycle behavior unchanged.

Completion criterion:

- lost first ACK leads to exactly one retry and one delivery.
- four unsuccessful attempts lead to an explicit delivery failure.

## Phase 6: Console Commands

Tasks:

- extend `CommandContext` with P2P services,
- implement and register new verbs:
  - `peer id <id>`,
  - `peer add <id>`,
  - `listen`,
  - `send-to <id> text <value>`,
  - `send-to <id> hex <bytes>`,
  - `broadcast text <value>`,
  - `peers`,
  - `stats`,
- generate CommandLineParser help from attributes,
- distinguish parsing, delivery, and radio errors in error messages,
- visibly identify unencrypted operation when starting `listen`, `send-to`,
  and `broadcast`,
- add complete examples to the README.

Completion criterion:

- Each new command has at least one success and one failure case.
- Existing version 1 commands behave unchanged.

## Phase 7: JSONL Session Persistence and HTML Export

Tasks:

- open an append-only JSONL file per local ID and peer on the first
  transmission or reception,
- record incoming, acknowledged, failed, and broadcast messages,
- close all writers when the process ends,
- use default path `~/LoRaP2P/sessions`,
- implement `export-chat <session.jsonl> [output.html]`,
- atomically write standalone HTML with encoded payloads,
- test persistence, export, invalid input, and HTML encoding.

Completion criterion:

- JSONL files can be exported during the active session.
- One message corresponds to exactly one validated JSON object per line.

## Phase 8: System Test and Release

Tasks:

- run the complete Release build and all tests,
- publish self-contained `linux-arm64`,
- run the two-device test matrix,
- intentionally discard the first ACK,
- initiate simultaneous transmission attempts,
- document duty-cycle wait times,
- update PRD status and architecture diagram,
- document known limitations.

Completion criterion:

- All criteria from PRD 12.7 are fulfilled or explicitly documented as open
  due to hardware.

## Phase 9: AES-GCM as a Follow-up Release

This phase begins only after acceptance of the unencrypted P2P protocol.
Details are in [security.md](security.md).

## Definition of Done

A work package is complete when:

- production code and relevant tests are included,
- build and tests succeed without warnings,
- unit tests require no real hardware,
- errors are reported explicitly,
- README, PRD, and P2P documents are consistent,
- the `linux-arm64` publish succeeds for platform changes,
- hardware tests are recorded with date, devices, and radio profile.
