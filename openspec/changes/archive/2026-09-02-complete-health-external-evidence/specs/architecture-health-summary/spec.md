## MODIFIED Requirements

### Requirement: Health JSON carries canonical reporting evidence
The machine-readable Architecture Health projection SHALL retain a versioned reporting-evidence payload produced from the same immutable Health evaluation receipts as its `architecture-health/v1` summary. The payload SHALL preserve canonical policy inventory and waiver lifecycle records, applicability completion and control-level reasons, configured topology and external-evidence evidence, findings and supplied remediation guidance, baseline/debt and policy-weakening receipts, and stable provenance references needed by the architecture PR-report projection.

When a supported Health CLI invocation supplies external-evidence artifacts and
producer context, Health SHALL bind them through the existing canonical
external-evidence trust, selection, and normalization authority before the
evaluation is projected. The payload SHALL retain the resulting one-per-logical
requirement trust receipts, including valid zero-result evidence and
wrong-context evidence, rather than writing an empty receipt set for declared
requirements.

The payload SHALL be additive to the Health summary and SHALL NOT alter Health gate or health precedence. It SHALL not reread policy YAML, rescan assemblies, revalidate external evidence, re-evaluate waiver lifecycle, or construct a second Health result. Absent compatibility-era receipts SHALL remain absent so downstream reporting can identify unavailable evidence rather than manufacture clean values.

#### Scenario: One Health evaluation supplies its report evidence
- **WHEN** Health evaluates complete current architecture governance and baseline-debt receipts
- **THEN** its JSON artifact retains the corresponding canonical reporting evidence with the same gate, health, control, lifecycle, and provenance facts
- **AND** exporting the artifact does not initiate another architecture analysis or authority evaluation

#### Scenario: Health binds current zero-result external evidence
- **WHEN** a Health CLI invocation supplies a required SARIF external-evidence
  artifact with a matching producer repository, revision, and scope and the
  selected run has zero results
- **THEN** its JSON report evidence retains that logical requirement's
  canonical current trust receipt with result count zero
- **AND** a compatible PR report can project external evidence as complete

#### Scenario: Health retains wrong-revision external evidence
- **WHEN** a Health CLI invocation supplies a required SARIF artifact whose
  producer revision differs from the requested context
- **THEN** its JSON report evidence retains that logical requirement's
  canonical wrong-revision trust receipt
- **AND** downstream reporting identifies the evidence as unavailable or
  unassessable rather than clean

#### Scenario: Missing receipt remains unavailable to reporting
- **WHEN** a compatibility or incomplete Health evaluation lacks policy inventory, applicability, topology, external-evidence, lifecycle, or debt receipt information
- **THEN** its reporting-evidence payload identifies that absence without serializing a zero or passing substitute
- **AND** a downstream PR report can render the affected authority as unavailable or unassessable
