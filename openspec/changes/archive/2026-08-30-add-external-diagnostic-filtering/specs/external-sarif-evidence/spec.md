## MODIFIED Requirements

### Requirement: Trust provenance is deterministic and reusable
Every completed artifact read SHALL expose the configured logical evidence identity, selected
producer/run identity, normalized repository-relative artifact path, deterministic lowercase
SHA-256 content hash, result count, and validated context bindings. A valid trusted read SHALL
also expose an immutable typed collection of the selected run's source diagnostics for the
external-diagnostic filtering boundary. Equivalent bytes and context SHALL produce equivalent
provenance regardless of host path separators or read order. The reader SHALL preserve these facts
for later filtering and normalized-finding work without invoking a producer-specific service API.

Source diagnostics SHALL be exposed only after the selected artifact succeeds every required trust
and context check. A trust failure SHALL retain the applicable evidence provenance but SHALL expose
no selectable source diagnostic. The reader SHALL obtain every exposed source fact from the same
bounded bytes that it hashes and validates; a later consumer SHALL NOT need to reopen an artifact
to select its trusted diagnostics.

#### Scenario: Identical artifact bytes have a stable hash
- **WHEN** identical SARIF bytes are read in separate local or CI assessments with equivalent
  explicit context
- **THEN** both trust outcomes expose the same artifact content hash and canonical provenance

#### Scenario: A trusted result collection is bound to the validated run
- **WHEN** a valid selected SARIF run contains diagnostic results
- **THEN** its typed source diagnostics are available with the same logical evidence and validated
  context provenance as the trusted read result

#### Scenario: Optional artifact is deliberately absent
- **WHEN** an explicitly optional logical evidence input has no supplied artifact
- **THEN** the outcome is explicitly optional/not-configured and is distinct from required missing
  evidence or a valid successful zero-result run
