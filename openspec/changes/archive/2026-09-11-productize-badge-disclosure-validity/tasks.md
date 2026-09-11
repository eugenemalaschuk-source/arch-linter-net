## 1. Canonical publication evidence

- [x] 1.1 Add Core publication-evidence models and deterministic finite-horizon projection from canonical receipts; verify focused Core tests cover waiver, evaluation-date, external-evidence, missing, unknown, and expired boundaries.
- [x] 1.2 Serialize publication evidence as an additive canonical report receipt without changing Health identity; verify equal complete inputs produce byte-identical output and no system-clock dependency.

## 2. Closed profile CLI boundary

- [x] 2.1 Implement versioned headline-only and headline-plus-freshness profile projection plus explicit upgrade diagnostics; verify focused CLI tests preserve legacy Architecture Health and architecture-policy semantics.
- [x] 2.2 Implement raw UTF-8 closed-profile validation and SHA-256 binding; verify golden vectors and duplicate-key, extra-field, noncanonical-escape, arbitrary-text, wrong-color, and oversize rejection tests.

## 3. Consumer-distributable contract

- [x] 3.1 Package the disclosure schema and canonical/adversarial fixtures in a stable consumer-visible location; verify package-content tests locate every declared asset.
- [x] 3.2 Add an isolated freshly packed CLI/Core consumer test covering accepted and rejected profile fixtures without a source-tree project reference.

## 4. Synchronization and validation

- [x] 4.1 Synchronize actual behavior with OpenSpec delta requirements and run strict change validation.
- [x] 4.2 Run risk-appropriate focused Core/CLI/pack validation, formatter, public-API check, architecture lint, and full OpenSpec validation; inspect the final diff for scope and package artifacts.
