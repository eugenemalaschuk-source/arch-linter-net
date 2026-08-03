## ADDED Requirements

### Requirement: Incomplete static evidence is never cache authorization
The system SHALL classify SDK-style evaluation, uninspected imports, unresolved references/analyzers, missing companion-artifact evidence, symlink/reparse-point inputs, and exhausted collection budgets as `cache-ineligible`. It SHALL apply count and aggregate-byte budgets before reading further candidate input bytes.

#### Scenario: SDK project lacks evaluated evidence
- **WHEN** a selected SDK-style project cannot prove SDK, implicit-import, global-property, analyzer, and framework identities
- **THEN** its outcome is `cache-ineligible` and it cannot authorize reuse

#### Scenario: Symlink escapes repository
- **WHEN** a candidate input is a symlink or reparse point
- **THEN** the collector does not hash it as authoritative evidence and returns `cache-ineligible`
