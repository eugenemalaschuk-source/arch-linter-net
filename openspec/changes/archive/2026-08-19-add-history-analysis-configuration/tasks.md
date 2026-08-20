## 1. Policy configuration

- [x] 1.1 Add the optional `history_analysis` model, defaults, fixed categories, profiles, and exact-decimal validation to the Core policy lifecycle.
- [x] 1.2 Extend the live policy schema and raw YAML validation so monolithic and imported policies reject invalid or unknown history-analysis configuration consistently.

## 2. History integration

- [x] 2.1 Implement bounded literal task extractors and connect effective policy configuration to the existing canonical TaskKey extraction seam.
- [x] 2.2 Implement exact no-normalization path classification and pre-analysis ignore matching as Core seams for later scoring stages.
- [x] 2.3 Add optional history-ingest policy loading without creating a second configuration authority.

## 3. Verification and documentation

- [x] 3.1 Add focused Core tests for configuration defaults, schema/raw validation, extractors, path classification, ignores, profiles, and thresholds.
- [x] 3.2 Add CLI tests for policy-backed ingestion and update contributor-facing release-forensics configuration documentation.
- [x] 3.3 Run formatter, focused test projects, relevant lint/spec checks, and review public API drift.
