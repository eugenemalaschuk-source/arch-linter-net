# semantic-port-boundary-contracts Specification

## Purpose
TBD - created by archiving change add-semantic-port-boundaries. Update Purpose after archive.
## Requirements
### Requirement: Semantic port-boundary contracts support strict and audit modes
The policy schema and runtime SHALL support `contracts.strict_port_boundaries`
and `contracts.audit_port_boundaries`. Each entry SHALL declare `name`, an
optional `id`, `source`, `target_context`, a non-empty `allowed_seams` list, a
non-empty `forbidden` list, and `reason`; it MAY declare `exclude` and
`ignored_violations`. Selectors SHALL use the existing contextual role and
metadata grammar.

#### Scenario: Strict direct boundary violation fails validation
- **WHEN** a selected source directly references a selected target-context type
  that matches `forbidden` and no allowed seam or exclusion
- **THEN** strict validation SHALL emit a build-failing port-boundary violation

#### Scenario: Audit boundary violation remains report-only
- **WHEN** the same rule is declared under `audit_port_boundaries`
- **THEN** the finding SHALL be reported using the existing audit-family
  behavior without becoming a strict validation failure

### Requirement: Approved seams do not exempt direct prohibited targets
For a selected source and target context, a direct target SHALL be permitted
only when it matches an `allowed_seams` selector and no `forbidden` selector.
An `exclude` selector SHALL remove a target before this evaluation and
`ignored_violations` SHALL suppress only an otherwise complete named finding.

#### Scenario: Catalog port is an allowed cross-context seam
- **WHEN** Sales Application references a Catalog type classified as `Port`
  with reviewed Catalog metadata and the target matches `allowed_seams`
- **THEN** the contract SHALL report no direct-dependency violation

#### Scenario: Direct Catalog domain reference is forbidden
- **WHEN** the same Sales source directly references a Catalog `DomainLayer`
  target selected by `forbidden`
- **THEN** the contract SHALL report the direct forbidden edge and expected
  approved seam

#### Scenario: Concrete adapter reference is not exempted by a port rule
- **WHEN** a selected source directly references a Catalog `Adapter` target
  selected by `forbidden` while another Catalog port exists
- **THEN** the contract SHALL report the adapter reference as forbidden

### Requirement: Adapter bindings are checked from deterministic interface facts
The policy schema and runtime SHALL support adapter binding entries that select
an adapter, an expected port, and approved adapter contexts. The implementation
SHALL evaluate the adapter's compiled full interface set and SHALL report a
mismatch when no implemented interface matches the expected port selector or
when the adapter is outside its approved context.

#### Scenario: Adapter implements its declared port
- **WHEN** a selected `StripePaymentAdapter` implements a `Payment` secondary
  port matching its reviewed metadata and is in an approved infrastructure
  context
- **THEN** the adapter binding check SHALL report no violation

#### Scenario: Adapter metadata identifies an unrelated port
- **WHEN** a selected adapter claims the `Payment` port but only implements a
  `Catalog` port
- **THEN** the adapter binding check SHALL report a mismatched-port violation

### Requirement: Anti-corruption boundaries use explicit selected seams
A port-boundary rule SHALL allow an `AntiCorruptionLayer` seam selected by
role/metadata and SHALL forbid explicitly selected direct infrastructure,
persistence, or database-adapter targets. The rule SHALL not infer an ACL from
namespace or type naming alone.

#### Scenario: Legacy context uses an approved ACL
- **WHEN** a selected LegacyCRM source references a target classified as an
  approved `AntiCorruptionLayer`
- **THEN** the rule SHALL report no violation

#### Scenario: Legacy context directly references a database adapter
- **WHEN** the same source directly references a selected database or
  infrastructure adapter target
- **THEN** the rule SHALL report a forbidden direct edge and the expected ACL
  seam

### Requirement: Unsupported evidence fails closed with explicit diagnostics
The evaluator SHALL use only deterministic direct compiled-type references and
compiled interface implementation facts. If the policy needs an unavailable or
ambiguous dependency kind, it SHALL produce a configuration or coverage
diagnostic and SHALL NOT claim an allowed seam.

#### Scenario: Policy requests unavailable method-body evidence
- **WHEN** a port-boundary rule requires a dependency kind that the compiled
  static analysis cannot determine
- **THEN** validation SHALL emit an explicit configuration or coverage
  diagnostic instead of passing the rule

