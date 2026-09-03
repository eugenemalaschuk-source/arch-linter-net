## ADDED Requirements

### Requirement: Native Architecture Health badge command
The CLI SHALL expose `badge architecture-health`, which SHALL read one
canonical `architecture-health/v1` JSON document and write a deterministic
Shields endpoint JSON object to stdout with `schemaVersion`, `label`,
`message`, and `color` fields. The command SHALL use the canonical top-level
Health category without recalculating it, and SHALL use the canonical selected
policy-inventory receipt's `ignore_debt.total` and `effective_rule_count`
without parsing policy YAML, findings, or waiver records.

For assessable input, the label SHALL be `architecture` and the message SHALL
contain all three facts in the form `<HEALTH> · <ignores> ignores · <rules>
rules`. The color SHALL be selected deterministically from the typed Health
category in one central badge projection mapping. `healthy`, `debt`,
`degrading`, and `failing` SHALL remain visibly distinct. An unassessable or
incomplete input SHALL produce `UNASSESSABLE · ? ignores · ? rules` and a
non-green unavailable color; it SHALL never invent zero counts or a healthy
state. The command SHALL not run architecture analysis or modify its input.
Its exit code SHALL preserve the canonical Health gate category: `pass` exits
0, `fail` exits 1, and `unassessable` or invalid badge input exits 2.

#### Scenario: Healthy canonical evidence produces a complete headline
- **WHEN** a canonical Health document reports `health=healthy`, zero explicit
  ignore debt, and 42 effective rules
- **THEN** the command writes a deterministic `architecture` badge whose
  message contains `HEALTHY`, `0 ignores`, and `42 rules`
- **AND** it exits 0 without evaluating policy or assemblies

#### Scenario: Reviewed waiver debt remains visible in a passing Health badge
- **WHEN** a canonical Health document reports `health=debt` and its selected
  policy inventory reports seven explicit ignores and 42 effective rules
- **THEN** the command writes a badge whose message contains `DEBT`, `7
  ignores`, and `42 rules`
- **AND** it does not infer the count from finding, source-set, or waiver-record fan-out

#### Scenario: Degrading and failing Health preserve the canonical category
- **WHEN** a canonical Health document reports either `degrading` or `failing`
  with a complete policy inventory
- **THEN** the command writes the corresponding upper-case Health category and
  canonical ignore/rule counts
- **AND** the two states do not share the healthy color

#### Scenario: Unassessable or incomplete evidence cannot fabricate counts
- **WHEN** the Health document, its selected policy-inventory receipt, or the
  required inventory counters are missing, malformed, unsupported, or
  unassessable
- **THEN** the command writes the explicit unassessable badge with unknown
  ignore and rule counts
- **AND** it exits 2 without emitting a healthy-looking or zero-count payload

#### Scenario: Equivalent input has equivalent output
- **WHEN** the command receives equivalent canonical Health and inventory
  evidence more than once
- **THEN** every generated badge JSON payload is byte-for-byte equivalent
