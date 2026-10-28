## ADDED Requirements

### Requirement: Read-only operations are isolated from publication execution

The promotion CLI SHALL dispatch `preflight` and `integration-test` through read-only operation-owned execution and failure paths that do not receive raw or Relay publication capabilities. `publish` and `renew` SHALL retain their existing publication and unavailable-snapshot failure behavior. Command names, arguments, exit codes, outputs and reason codes SHALL remain compatible.

#### Scenario: Preflight evaluates current evidence without publishing
- **WHEN** preflight resolves valid push evidence
- **THEN** it forces temporal revalidation, requires the existing valid receipt and uses the existing promotion decision
- **AND** it reports its existing ready or unavailable result without any raw, Relay or unavailable-snapshot publication

#### Scenario: Preflight fails
- **WHEN** preflight encounters an unavailable decision, missing receipt or handled error
- **THEN** it reports the existing unavailable result and performs no publication

#### Scenario: Runtime integration checks the trusted action root
- **WHEN** integration-test runs
- **THEN** it requires the `reference-none` configuration, `none` adapter, matching pull-request event and repository, and the exact trusted action-root relationship
- **AND** it makes no API or publication call and preserves its existing result and reasons

#### Scenario: Publish or renew fails
- **WHEN** publish or renew encounters a handled failure
- **THEN** its existing fail-closed publication and unavailable-snapshot policy remains in force
