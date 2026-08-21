## MODIFIED Requirements

### Requirement: JSON context output is deterministic and safe for tools
Core SHALL format the policy-context representation as a single deterministic
JSON document with `schema_version: 3` and a stable context kind. Repeated
exports of unchanged policy inputs SHALL produce byte-identical JSON. The JSON
SHALL not include absolute local paths, runtime environment values, build
receipts, target-assembly facts, or other sensitive machine-specific data. It
SHALL include typed declared analysis inputs, the explicit schema-validated
policy-weakening severity, and typed ignored-violation `source_type` and
`forbidden_reference` matchers so a separate-state guardrail comparison has no
implicit empty input or display-string parsing.

#### Scenario: Imported policy has portable JSON provenance
- **WHEN** JSON is exported for a policy composed from a root and fragment
- **THEN** its provenance names portable policy paths and document roles in a
  deterministic order and contains no rooted filesystem path

#### Scenario: Ignored violation retains typed matcher evidence
- **WHEN** a policy declares an ignored violation with `source_type: "*"` and
  `forbidden_reference: "*"`
- **THEN** the JSON includes those values as typed matcher evidence alongside
  its human-readable exception detail
