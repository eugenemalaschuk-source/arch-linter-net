## MODIFIED Requirements

### Requirement: Baseline lifecycle is safe and reviewable
Baseline writers SHALL emit `version: 2` and preserve canonical exact identity. Generate, migrate, update, prune, diff, and verify SHALL share one lifecycle vocabulary:

- `new`: a current finding has no exact baseline entry;
- `matched`: an entry and current finding have equal canonical identity;
- `resolved`: a valid, evaluable baseline identity has no current finding;
- `stale`: the entry references a contract, family, source instance, schema, or identity form that is no longer valid/evaluable, distinct from resolved debt;
- `changed`: a deterministic predecessor/successor relationship can be shown but canonical identity differs, so the entry does not suppress until explicitly reviewed;
- `ambiguous`: more than one candidate could correspond to an entry and the tool refuses to guess;
- `configuration-error`: malformed, unsupported, or inconsistent input prevents safe classification.

The system SHALL admit, for a cycle contract, only non-ignored reference evidence whose directed layer edge participates in an actual detected cycle as baseline candidates. Considered graph edges that do not participate in a detected cycle SHALL remain internal analysis evidence and SHALL NOT be serialized or reported as baseline debt.

Existing files SHALL not be overwritten without explicit intent; update/prune SHALL preview changes and use atomic replacement. Reviewed reasons and metadata SHALL be preserved when safe round-trip is supported, otherwise the command SHALL stop with an actionable diagnostic and leave the original unchanged. `changed`, `stale`, `ambiguous`, and `configuration-error` SHALL never silently suppress a current finding. Baseline verification SHALL be in sync only when there are zero `new`, `resolved`, `stale`, and `ambiguous` entries; its human output, JSON `inSync` field, and exit code SHALL reflect that same verdict.

CI guidance SHALL verify baselines but SHALL NOT automatically approve or write new debt.

#### Scenario: Baseline update fails while writing
- **WHEN** serialization, validation, or atomic replacement fails
- **THEN** the original baseline bytes remain unchanged and the command exits as incomplete

#### Scenario: Comment-preserving round trip is unavailable
- **WHEN** a file contains reviewed content that the implementation cannot safely preserve
- **THEN** update/prune refuses the write and produces a preview plus an actionable manual path

#### Scenario: Acyclic strict-cycle analysis has no debt candidates
- **WHEN** a strict-cycle contract analyzes a multi-layer graph with directed edges but no cycle
- **THEN** no strict-cycle baseline candidates are generated, update persists none, and verify reports zero new cycle debt as in sync

#### Scenario: Strict-cycle findings retain exact baseline evidence
- **WHEN** a strict-cycle contract finds a cycle in a multi-layer graph
- **THEN** only reference evidence for edges participating in that cycle is baseline-eligible, with deterministic exact identity

#### Scenario: New baseline debt is out of sync in every output format
- **WHEN** baseline verification finds one or more new candidates
- **THEN** human output, JSON `inSync`, and the validation-failure exit code all report the baseline as out of sync
