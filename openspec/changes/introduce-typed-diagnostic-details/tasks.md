## 1. Normalized finding contract

- [ ] 1.1 Add the versioned normalized finding envelope, typed details hierarchy, identity/location/baseline metadata, and forward-compatibility handling.
- [ ] 1.2 Map every existing diagnostic family and non-violation diagnostic producer to normalized findings with deterministic ordering.

## 2. Adapter integration

- [ ] 2.1 Route human and JSON diagnostic rendering through normalized findings while retaining derived legacy JSON compatibility fields.
- [ ] 2.2 Route SARIF and Testing API projections through normalized findings, preserving SARIF physical locations and baseline lifecycle evidence.

## 3. Contract delivery and validation

- [ ] 3.1 Add the packaged normalized diagnostic JSON schema, compatibility/migration documentation, and schema validation.
- [ ] 3.2 Add exhaustive family matrix, JSON/SARIF/human parity, identity/order, baseline lifecycle, and unknown-version/kind NUnit coverage.
- [ ] 3.3 Run formatting, focused tests, repository acceptance, and OpenSpec validation; synchronize and archive the completed change.
