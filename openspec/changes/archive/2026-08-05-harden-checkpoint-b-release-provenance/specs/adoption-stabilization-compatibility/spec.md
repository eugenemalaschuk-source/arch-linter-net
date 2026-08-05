## ADDED Requirements

### Requirement: Checkpoint B authorizes exact release artifacts
Checkpoint B authorization SHALL apply only to the version-resolved package
manifest downloaded by every platform runner and re-verified by the publishing
job. A later pack, dry-run rerun, or package set with different metadata or
digest SHALL require a new Checkpoint B result.

#### Scenario: Publishing candidate differs from tested candidate
- **WHEN** a publishing job observes a candidate package digest not present in
  the successful Checkpoint B manifest
- **THEN** it fails before publication

