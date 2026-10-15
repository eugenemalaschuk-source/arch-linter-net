## 1. Core request and completeness behavior

- [x] 1.1 Preserve prepared-artifact selection when standalone Health creates its analysis snapshot.
- [x] 1.2 Carry the prepared-receipt selector through Core topology capture and validation requests without changing public API.
- [x] 1.3 Reject topology observations when the session's reflected type universe is incomplete.
- [x] 1.4 Add focused Core tests for Health request mapping and fail-closed topology completeness.

## 2. Topology CLI and guidance

- [x] 2.1 Add `--use-prepared-receipts` to topology capture, diff, and verify and map it to the existing Core selection.
- [x] 2.2 Update topology command help and the topology workflow guide with the one-time producer and no-build reader sequence.
- [x] 2.3 Add focused CLI option and request-propagation tests.

## 3. Packaged consumer regression

- [x] 3.1 Add the DI Abstractions 10 consumer fixture with property and interface type variants.
- [x] 3.2 Extend packed CLI acceptance to prepare once and run independent Health and topology readers with complete semantic parity.
- [x] 3.3 Assert current receipt verification, no reader build/restore, unchanged output and receipt hashes, and fail-closed stale or missing receipts.

## 4. Validation and spec closure

- [x] 4.1 Run focused Core and CLI regressions, relevant project suites, formatting, and implicated repository checks.
- [x] 4.2 Run OpenSpec validation, synchronize the main specs, archive this change, and inspect the archived result.
