## ADDED Requirements

### Requirement: PR report artifacts share one compatible execution context
The PR-report projection SHALL accept a Health reporting-evidence artifact and
an architecture-change report only when both artifacts declare the same
non-empty execution identifier and condition-set scope.  It SHALL select a
Health validation receipt whose canonical mode equals the change report mode.
Different execution identifiers, condition sets, receipt modes, missing
context fields, duplicate candidate receipts, or unsupported context versions
SHALL be rejected as incompatible input; the projection SHALL not select a
different receipt, infer compatibility, or render an availability state from
mixed artifacts.

#### Scenario: Different runs are rejected
- **WHEN** a strict Health artifact and a strict change report have different
  execution identifiers
- **THEN** PR-report generation fails with the established invalid-artifact
  error contract

#### Scenario: Different mode or condition set is rejected
- **WHEN** the change report's mode or condition-set scope does not match a
  canonical Health validation receipt in the same execution context
- **THEN** PR-report generation fails closed without rendering Markdown

#### Scenario: Legacy Health cannot bypass correlation
- **WHEN** the Health input lacks the versioned reporting-evidence envelope
- **THEN** PR-report generation rejects the artifact rather than pairing its
  headline with a change report from an unproven execution

### Requirement: PR report evidence is a closed authority payload contract
The projection SHALL validate each reporting receipt's availability map against
the complete known authority-key set and its allowed wire values.  Every
authority declared `available` SHALL have its required canonical payload, and
every absent payload SHALL use its authority's explicitly allowed unavailable
or not-configured state.  Unknown keys, unknown wire values, duplicate keys,
and a mismatch between availability and policy inventory, waiver lifecycle,
applicability, topology, external-evidence, or finding payloads SHALL be
rejected as malformed input.

#### Scenario: Available external evidence has no payload
- **WHEN** a receipt declares `external_evidence=available` but omits the
  external-evidence payload
- **THEN** PR-report generation fails closed rather than projecting complete
  availability or a clean external-evidence result

#### Scenario: Unknown availability data is rejected
- **WHEN** a receipt contains an unrecognized availability key or wire token
- **THEN** PR-report generation fails with an invalid-artifact error

### Requirement: Markdown is safe and concise for canonical evidence
The CLI SHALL escape every repository-controlled or artifact-controlled value
according to its Markdown context.  Inline-code values SHALL not close or
extend their code span, and plain-text values SHALL not create links, comments,
HTML, headings, or control-character-driven structure.  A complete clean
report SHALL omit empty blocker and non-blocking-debt drill-down sections, and
empty bounded sections SHALL not emit `Showing 0 of 0` detail text.

#### Scenario: Hostile evidence cannot alter report structure
- **WHEN** a policy, finding, remediation, or change-artifact value contains
  backticks, HTML-comment delimiters, Markdown-link syntax, or control
  characters
- **THEN** the rendered output displays it as inert text and retains the
  report's intended section structure

#### Scenario: Clean complete report has no empty debt drill-downs
- **WHEN** a report is complete, passes, and has no blockers or non-blocking
  debt
- **THEN** the Markdown contains neither blocker nor debt drill-down headings
  and no empty bounded-detail indicator

#### Scenario: Baseline lifecycle remains distinct from accepted debt
- **WHEN** a debt-gate receipt contains matched and non-matched baseline
  lifecycle entries
- **THEN** only matched entries render as non-blocking existing debt, while
  non-matched lifecycle states remain blocking baseline-integrity evidence

### Requirement: Blockers preserve their canonical authority semantics
The renderer SHALL select blockers from the owning canonical strict receipt,
its canonical lifecycle blocking state, and the debt-gate receipt's explicit
blocking result.  It SHALL retain audit-mode findings, external evidence,
baseline configuration diagnostics, and preflight diagnostics as evidence
unless their canonical authority explicitly classifies them as blocking.  A
failing aggregate gate SHALL not reclassify every finding as a blocker.

#### Scenario: Strict blocker does not promote audit evidence
- **WHEN** one strict finding is blocking and the same Health artifact also
  contains audit findings
- **THEN** the strict finding appears in blockers while the audit findings
  remain non-blocking evidence
