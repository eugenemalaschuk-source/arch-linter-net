## 1. Normalized finding contract

- [x] 1.1 Add the versioned normalized finding envelope, typed details hierarchy, identity/location/baseline metadata, and forward-compatibility handling.
- [x] 1.2 Map every existing diagnostic family and non-violation diagnostic producer to normalized findings with deterministic ordering.

## 2. Adapter integration

- [x] 2.1 Route human and JSON diagnostic rendering through normalized findings while retaining derived legacy JSON compatibility fields.
- [x] 2.2 Route SARIF and Testing API projections through normalized findings, preserving SARIF physical locations and baseline lifecycle evidence.

## 3. Contract delivery and validation

- [x] 3.1 Add the packaged normalized diagnostic JSON schema, compatibility/migration documentation, and schema validation.
- [x] 3.2 Add exhaustive family matrix, JSON/SARIF/human parity, identity/order, baseline lifecycle, and unknown-version/kind NUnit coverage.
- [x] 3.3 Run formatting, focused tests, repository acceptance, and OpenSpec validation; synchronize and archive the completed change.
