## MODIFIED Requirements

### Requirement: Authoritative assessment completion preserves CLI exit categories
When a CLI command executes an authoritative governance assessment, it SHALL
map trusted completion `fail` to exit code `1` and valid-but-unassessable
completion to exit code `2`. Completion `pass` SHALL map to exit code `0` only
when the ordinary validation outcome also passed; an ordinary failed outcome
SHALL exit `1` even when transport-only completion evidence says `pass`. Exit
code `2` for unassessable evidence SHALL expose a stable typed completion status
and reason that distinguishes it from invalid invocation, invalid
policy/configuration, runtime, cancellation, and output-routing failures.
Non-gating/read-only commands SHALL not represent unassessable evidence as a
successful clean governance result.

#### Scenario: Unassessable authoritative validation exits two
- **WHEN** a valid authoritative validation request has a required control
  whose evidence is unexpectedly empty or missing
- **THEN** the command exits `2` and exposes the unassessable completion status
  and canonical reason instead of exit `0` or an ordinary violation exit `1`

#### Scenario: Completion pass cannot hide ordinary failure
- **WHEN** a valid authoritative assessment has transport-only completion
  evidence of `pass` but ordinary validation has a blocking violation
- **THEN** the command exits `1`, not `0`

#### Scenario: Completed formats retain machine-readable completion evidence
- **WHEN** a valid authoritative assessment completes with `unassessable`
- **THEN** its Human, JSON, and SARIF result formats expose equivalent stable
  completion status and reason evidence without emitting a synthetic ordinary
  architecture finding

#### Scenario: Invalid CLI token remains distinct
- **WHEN** a CLI invocation contains an unknown or malformed command token
- **THEN** the command exits `2` through its invalid-arguments path and does
  not claim valid-but-unassessable architecture evidence
