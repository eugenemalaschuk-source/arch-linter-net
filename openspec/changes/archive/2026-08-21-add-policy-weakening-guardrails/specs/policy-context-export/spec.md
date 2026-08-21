## MODIFIED Requirements

### Requirement: JSON context output is deterministic and safe for tools
Core SHALL format the policy-context representation as a single deterministic
JSON document with `schema_version: 2` and a stable context kind. Repeated
exports of unchanged policy inputs SHALL produce byte-identical JSON. The JSON
SHALL not include absolute local paths, runtime environment values, build
receipts, target-assembly facts, or other sensitive machine-specific data. It
SHALL include typed declared analysis inputs and the explicit schema-validated
policy-weakening severity so a separate-state guardrail comparison has no
implicit empty input.

#### Scenario: Imported policy has portable JSON provenance
- **WHEN** JSON is exported for a policy composed from a root and fragment
- **THEN** its provenance names portable policy paths and document roles in a
  deterministic order and contains no rooted filesystem path

## ADDED Requirements

### Requirement: Policy context projects configured weakening severity

The versioned effective policy context SHALL include the explicit,
schema-validated `analysis.policy_weakening` severity that governs an invoked
policy-weakening comparison.  Export SHALL continue to avoid architecture
analysis and SHALL not change ordinary validation behavior.

#### Scenario: Context carries explicit severity
- **WHEN** a valid policy configures `analysis.policy_weakening` as `warn`
- **THEN** JSON and Markdown context output identify that configured severity
alongside the effective policy facts
