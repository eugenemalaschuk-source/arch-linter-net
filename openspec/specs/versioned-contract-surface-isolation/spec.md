# versioned-contract-surface-isolation Specification

## Purpose
TBD - created by archiving change add-versioned-contract-surface-isolation. Update Purpose after archive.
## Requirements
### Requirement: Policies declare versioned contract-surface isolation controls
The system SHALL support `strict_versioned_contract_surface_isolation` and
`audit_versioned_contract_surface_isolation` controls. Every control SHALL
declare a non-blank stable ID and name, one local non-empty `surfaces` list, a
`source_surface` ID, and one or more `forbidden_surfaces` IDs. Each surface
SHALL declare a unique non-blank ID and a non-empty `types_matching` selector
using only the existing bounded `name_suffix`, `name_prefix`, `namespace`,
`layer`, `base_type`, `implements_interface`, `has_attribute`, and `role`
vocabulary. Populated selector criteria SHALL combine conjunctively.

The source surface and every forbidden surface reference SHALL resolve to one
declared surface in the same control. A control SHALL reject blank, duplicate,
unknown, empty, unbounded, or self-referential surface declarations or
references as invalid policy configuration. The controls SHALL NOT introduce
regex, free-form expressions, tags, a second semantic-role model, or runtime
version configuration.

#### Scenario: A v1 surface names v2 and implementation targets
- **WHEN** a strict isolation control defines `v1-contracts`, `v2-contracts`,
  and `transport-implementation` surfaces with existing namespace or role
  selectors, selects `v1-contracts` as its source, and forbids the other two
- **THEN** the policy is valid and retains the existing selector and semantic
  role matching behavior

#### Scenario: An unknown forbidden surface is rejected
- **WHEN** a control references a forbidden surface ID that it does not
  declare
- **THEN** policy loading fails before evaluation rather than treating the
  unknown surface as an empty target set

### Requirement: Isolation evaluates recursive visible exposure using selected groups
For every effective isolation control, the system SHALL select the source
surface's exported visible type roots with the existing type-selection and
visible-surface semantics. It SHALL evaluate their recursive
visible-signature and visible-contract-metadata exposure evidence using the
existing exposure index. Types selected by any forbidden surface SHALL be
forbidden targets, including first-party non-exported implementation types
when they occur in an exposure path.

The system SHALL emit one deterministic path-rich finding for every distinct
source/path/target occurrence. Each finding SHALL retain the source surface,
declaring type/member or metadata site, deterministic exposure path, and
assembly-qualified target identity. Same-named types from different namespaces
or assemblies and distinct paths to the same target SHALL remain distinct.

#### Scenario: Nested v2 type leaks from v1
- **WHEN** a v1 exported member exposes `Task<Envelope<V2.Customer>>` and the
  v2 type matches a forbidden v2 surface
- **THEN** the strict control reports a finding with the member-level path
  through both generic wrappers and the reflected identity of `V2.Customer`

#### Scenario: Same-named target types remain distinct
- **WHEN** two forbidden version surfaces select types named `Customer` from
  different assemblies or namespaces and both are exposed by the source
- **THEN** each finding retains its own assembly-qualified target identity and
  stable path evidence

### Requirement: Isolation reuses governance lifecycle and fails closed on group applicability
Each effective isolation control SHALL contribute exactly one required
applicability record using the existing governance applicability model. A
referenced source or forbidden surface that matches zero current types, a
source surface that yields zero exported roots, incomplete required type or
exposure evidence, or an unavailable target universe SHALL make the control
unassessable with deterministic reason and provenance. Zero leak findings
SHALL be a clean assessment only when the control is evaluable.

Strict and audit isolation findings SHALL preserve their normal severity and
reuse the existing exposure diagnostic payload, canonical identity, ignores,
baseline comparison, Human, JSON, SARIF, and Testing projections. Existing
generic contract-surface exposure and all other contract families SHALL remain
unchanged when no isolation control is configured.

#### Scenario: A stale v2 selector cannot create a false green
- **WHEN** the forbidden v2 surface selector matches no current types
- **THEN** the control has deterministic unassessable applicability evidence
  and does not report a trusted zero-finding result

#### Scenario: Audit isolation finding uses normalized exposure output
- **WHEN** an audit isolation control detects a v1-to-v2 exposure
- **THEN** its finding uses the existing contract-surface exposure payload and
  normalized output behavior with audit severity

### Requirement: Documentation defines the static-boundary scope
The contract documentation SHALL show valid versioned-surface isolation
authoring syntax and explain that it statically checks visible CLR signatures
and compiled metadata references. Documentation SHALL distinguish this feature
from runtime endpoint routing, version negotiation, payload-schema execution,
semantic-version decisions, and binary compatibility analysis.

#### Scenario: An author reads the versioned isolation documentation
- **WHEN** an author needs to keep a v1 public contract from leaking v2 or
  transport implementation types
- **THEN** the documentation provides bounded selector/group examples and
  states that no runtime API-version behavior is inferred
