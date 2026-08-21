# policy-context-export Specification

## Purpose

Provide AI coding agents with a compact, deterministic, and safe summary of the
effective architecture policy before they generate or modify code.
## Requirements
### Requirement: Effective policy context has a public, versioned Core representation
Core SHALL expose a typed policy-context export operation that loads a selected
policy through the normal effective-policy loader and returns a versioned
context representation. The operation SHALL not parse a second policy model,
invoke project or assembly analysis, or change validation behavior.

#### Scenario: Core caller exports a composed policy
- **WHEN** a Core caller exports a selected root policy that imports fragments
- **THEN** the result represents the same effective layers, contracts,
  classification facts, source-set expansion, and typed provenance that normal
  policy loading uses

#### Scenario: Export does not perform architecture analysis
- **WHEN** a caller exports a valid policy whose configured projects or target
  assemblies are unavailable
- **THEN** the export succeeds without building projects, loading assemblies,
  or claiming that architecture compliance was evaluated

### Requirement: Context contains compact architecture facts and bounded guidance
The context SHALL include the policy identity; active contract IDs and names;
contract modes and families; layers and selectors; each port boundary's typed
adapter bindings (adapter, expected port, and allowed contexts); declared semantic roles and
metadata keys; declared context values; coverage scopes; important exclusions;
and portable authored/effective provenance where available. It SHALL include
only reviewed, static-analysis-safe guidance that tells agents to inspect
facts, use narrow schema-backed changes, avoid broad ignores or overrides,
and keep human review.

The context SHALL project every supported contract family's effective rule
inputs through typed projections. It SHALL preserve ordered and nested inputs,
booleans, source-set references, allowed and forbidden directions, and typed
selectors rather than silently flattening or omitting them. Layer-template
contracts SHALL retain their containers, container exclusions, ordered optional
layers, and exhaustive setting; composition contracts SHALL retain forbidden
APIs and every allowed-only scope, including assembly sets and typed assembly/
type selectors.

For every source-set or container expansion, the context SHALL retain the
authored contract-to-set relation as well as the executable effective instances,
positive inclusions, and source/source-set/container exclusions. Each entry
SHALL retain matched or stale state, optional-empty state and reason where
applicable, and the portable authored, source-set-reference, and exclusion
provenance already produced by effective-policy loading.

#### Scenario: Modular-monolith context exposes governed boundaries
- **WHEN** a policy declares Sales, Catalog, and SharedKernel semantic roles,
  contextual contracts, semantic coverage, and a narrow exception
- **THEN** the context names the declared roles, metadata/context values,
  contract IDs, port adapter bindings, coverage scope, exception, and portable source provenance

#### Scenario: Contract context retains nested executable semantics
- **WHEN** a policy declares a layer template with optional layers and an
  exhaustive composition contract with typed allowed-only locations
- **THEN** the JSON and Markdown context retain those ordered, nested, allowed,
  and forbidden rule inputs from the same effective-policy model

#### Scenario: Source-set fan-out retains authored subtraction evidence
- **WHEN** a fan-out contract selects sources through named source sets and
  subtracts both an explicit source and a source set
- **THEN** the context names the authored contract and sets, the remaining
  effective instances, the pre-subtraction inclusions, matched exclusions, and
  the policy locations of source-set and exclusion references

#### Scenario: Unity/client policy context exposes client classification
- **WHEN** a policy declares Unity player and editor roles with namespace or
  inheritance classification
- **THEN** the context identifies those declared roles and metadata without
  inferring runtime behavior or creating undocumented roles

### Requirement: JSON context output is deterministic and safe for tools
Core SHALL format the policy-context representation as a single deterministic
JSON document with `schema_version: 1` and a stable context kind. Repeated
exports of unchanged policy inputs SHALL produce byte-identical JSON. The JSON
SHALL not include absolute local paths, runtime environment values, build
receipts, target-assembly facts, or other sensitive machine-specific data.

#### Scenario: Imported policy has portable JSON provenance
- **WHEN** JSON is exported for a policy composed from a root and fragment
- **THEN** its provenance names portable policy paths and document roles in a
  deterministic order and contains no rooted filesystem path

### Requirement: Markdown context is concise and derived from the same model
Core SHALL format the same context representation as concise Markdown suitable
for an agent prompt or pull-request instruction. It SHALL identify its policy
scope, boundaries and roles, safe guidance, and that it is not a replacement
for full architecture validation.

#### Scenario: Markdown retains deterministic policy facts
- **WHEN** Markdown is exported for an unchanged policy twice
- **THEN** both outputs are identical and contain the same declared policy
  context as the JSON export without inventing allowed or forbidden examples

### Requirement: Policy context failures preserve policy diagnostics
The export operation SHALL preserve the typed policy diagnostics produced by
normal policy loading and expose the existing portable source locations through
its programmatic and CLI boundaries.

#### Scenario: Invalid imported policy fails consistently
- **WHEN** an imported policy contains an invalid effective value
- **THEN** policy-context export fails with the same typed fragment and root
  diagnostic information as another effective-policy consumer
