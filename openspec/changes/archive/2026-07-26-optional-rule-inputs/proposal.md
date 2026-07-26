## Why

Rule-input coverage currently treats every declared layer that matches no code as architecture debt. A policy author cannot record a reviewed future layer without excluding the entire contract or weakening coverage for its other inputs.

## What Changes

- Add schema-backed `optional_inputs` declarations to rule-input coverage contracts. Each declaration identifies one contract input exactly and requires a reason.
- Classify a matching empty input as typed `optional-empty`, retain its provenance and reason in coverage outputs, and automatically classify it as covered once code matches.
- Preserve fail-closed stale and unknown diagnostics, including for invalid optional-input identities.
- Extend coverage summaries and their human, JSON, SARIF, explain, and Testing API projections with optional-empty input evidence.
- Update policy schema, capability metadata, authoring documentation, and AI guidance.

## Capabilities

### New Capabilities

- `optional-rule-input-coverage`: Explicit planned-empty lifecycle state for exact rule inputs.

### Modified Capabilities

- `rule-input-coverage-contracts`: Rule-input coverage recognizes optional-empty inputs without changing ordinary empty-input behavior.
- `architecture-coverage-reporting`: Coverage summaries expose optional-empty evidence and counts.
- `adoption-stabilization-compatibility`: Implements the planned-empty rule-input lifecycle design slice.

## Impact

Affected areas include the YAML contract model and schema, coverage validation and execution, normalized coverage summaries and report projections, Testing API, policy-import provenance, docs, and NUnit coverage tests. Existing policies remain compatible because `optional_inputs` is additive.
