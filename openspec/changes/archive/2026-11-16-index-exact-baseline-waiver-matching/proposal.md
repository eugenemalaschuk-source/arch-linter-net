# Proposal

## Why

`ArchitectureIgnoreMatcher` scans every ignore for each finding and rebuilds structured-waiver fingerprints inside that scan. Large baselines and policies therefore multiply avoidable work by findings, even when almost every entry is an exact identity. Indexing these exact paths can reduce that repeated work while preserving the ordered suppression, usage-tracking, and provenance behavior that callers depend on.

## What Changes

- Add a per-contract lookup for versioned baseline identities, structured waiver fingerprints, and literal exact pairs; keep wildcard patterns in the existing fallback matcher.
- Resolve indexed and wildcard matches in original ignore order, retaining duplicate match indices and the existing tracker/no-tracker distinction.
- Compute a live structured-waiver fingerprint no more than once per applicable finding.
- Add differential semantic coverage and reproducible before/after scale evidence using the existing #502 performance foundation, with wildcard and small-input trade-offs reported separately.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ignore-matching`: specify the ordered semantic parity and bounded work guarantee for exact identity, waiver, and literal-pair matching while retaining wildcard fallback behavior.

## Impact

- Internal Core matching and contract execution context; no public API, YAML schema, wildcard-language, dependency, or diagnostic-identity changes.
- Core NUnit tests and the existing performance evidence approach for deterministic work, timing, and memory measurements.
