## ADDED Requirements

### Requirement: Public Architecture Health state is atomically current
The stable public Architecture Health endpoint and its publication metadata
SHALL represent one indivisible, current `main` publication. Trusted automation
SHALL publish a ready payload only while the push commit remains the current
`main` tip. A stale event or replayed workflow SHALL make no publication write.
When evidence is absent, rejected, or unavailable, the endpoint SHALL be
replaced with a reviewed CLI-generated `UNASSESSABLE · ? ignores · ? rules`
payload without requiring a CLI restore, build, or execution at publication
time. A failed publication update SHALL leave neither a new payload paired with
old metadata nor old ready data represented as current.

#### Scenario: Fallback remains available when the CLI cannot execute
- **WHEN** trusted publication cannot resolve ready evidence and the runner
  cannot restore, build, or execute the CLI
- **THEN** the stable endpoint is replaced with the reviewed explicit
  unassessable payload
- **AND** its metadata records that unavailable state

#### Scenario: Replayed older main event cannot overwrite the current badge
- **WHEN** a publisher run for an earlier `main` commit reaches its write
  boundary after `main` has advanced
- **THEN** it makes no update to the public endpoint or metadata
- **AND** the newer publication remains intact

#### Scenario: Payload and metadata cannot become partially current
- **WHEN** a publication write encounters a concurrent change or other write
  failure
- **THEN** readers observe either the complete previous publication or the
  complete new publication
- **AND** they do not observe a new payload paired with previous metadata

