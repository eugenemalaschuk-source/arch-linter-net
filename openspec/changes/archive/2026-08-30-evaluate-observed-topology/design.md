## Context

`topology` is already a validated, provenance-aware policy declaration with a
closed selector vocabulary, explicit subject kind and scope, declared nodes,
allowed directed edges, reviewed exclusions, and optional stale-declaration
checks. The validation pipeline has no evaluator for those facts. Core already
owns a per-run type/reference/project session, a canonical applicability
expected-record join, normalized finding projection, cache reconstruction, and
CLI/Testing adapters. The implementation must compose those seams rather than
create a topology-specific result or output model.

## Goals / Non-Goals

**Goals:**

- Derive one deterministic observed first-party subject universe at the policy's
  declared granularity from the current analysis session.
- Classify each in-scope subject set-wise as reviewed out of scope, mapped,
  unmapped, or ambiguous, independently of declaration and input order.
- Evaluate observed dependencies between exactly-mapped components, reporting
  one deterministic witness for each forbidden component direction.
- Retain canonical native mapping and declaration-drift evidence in the shared
  topology applicability record so later consumers can render it without
  reading YAML or rescanning assemblies.
- Preserve distinct structural, relational, declaration-drift, and
  applicability/unassessable categories through the existing normalized
  finding, cache, CLI, Testing, and baseline seams.

**Non-Goals:**

- Add a topology YAML syntax, a diagram parser, a second baseline/debt identity,
  a topology quality score, or automatic policy modification.
- Treat unmapped partial-topology subjects as a completeness failure.
- Re-evaluate existing protected-surface or interface contracts; topology has no
  separate interface declaration and continues to compose only through those
  existing contract families.
- Scan runtime calls, service discovery, external dependencies, or every
  possible dependency path.

## Decisions

### Derive facts once from the analysis session

The evaluator will be a Core execution service that consumes session-owned type,
reference, target-assembly, and project-discovery facts. It will materialize
canonical observed facts for the declared `subject_kind` only: types from the
type index, namespaces with their canonical owner identity, selected first-party
projects, or selected first-party assemblies. A subject identity includes kind,
project, assembly, and subject value, preventing same-named namespaces/types in
different owners from coalescing. Dependency facts are similarly reduced to the
configured subject kind and deduplicated before evaluation.

Reusing the session is chosen over building a graph-command adapter or a second
scanner: it keeps topology evaluation aligned with validation input closure,
cancellation, and cache semantics.

### Classify before evaluating relationships

For each observed fact, scope selection happens first. An in-scope subject that
matches a reviewed exclusion is classified out of scope; otherwise exactly one
matching node is mapped, no matching node is unmapped, and two or more matching
nodes is ambiguous. Match lists, classifications, evidence, and witnesses use
ordinal canonical ordering.

Only exactly-mapped source and target subjects participate in component-edge
evaluation. This avoids turning an applicability gap into a guessed forbidden
edge. The evaluator groups observed dependencies by mapped `(sourceNode,
targetNode)` direction, skips intra-node relationships, and emits one
relational diagnostic for each direction absent from `allowed_edges`, with the
lexicographically first source/target subject dependency as its witness.

### Make applicability mode-sensitive and fail closed

The presence of a topology creates one canonical topology applicability control.
An exhaustive topology is required; an explicit partial topology is visible but
does not turn an otherwise unmapped subject into a completeness error. Ambiguous
subjects, enabled stale declarations, and a required unexpected-empty universe
are unassessable in either mode because the evaluator cannot establish their
declared mapping. An exhaustive unmapped subject is unassessable; a partial
unmapped subject remains native evidence but does not make the record
unassessable. `allow_empty: true` permits an otherwise empty exhaustive
universe. Existing policies with no topology still produce no applicability
entries or records.

This follows the existing expected-membership/record join rather than adding an
evaluator-owned gate or a second applicability summary.

### Retain native evidence inside the shared record

The shared applicability record will gain a bounded, typed family-evidence
extension. Topology supplies a canonical mapping-evidence value containing
declared-component count; observed, mapped, reviewed-out-of-scope, unmapped,
and ambiguous subject evidence; enabled stale nodes/edges; and observed
component relationship witnesses. This is evidence attached to the shared
record, not a topology-only result envelope. It is copied through cache and
projected by existing output/adapter seams so #680 and later Health/report work
can consume authoritative counts and bounded drill-down without recounting.

### Keep finding classes separate

The evaluator produces existing normalized `ArchitectureFinding` values through
typed Core diagnostics:

- structural mapping diagnostics for ambiguous subject classification;
- relational diagnostics for forbidden mapped component directions;
- declaration-drift diagnostics for enabled stale nodes and allowed edges; and
- shared applicability diagnostics for incomplete required evidence.

Ordinary relational diagnostics carry source node, target node, and a canonical
subject-level dependency witness. Applicability diagnostics retain their
existing reason/provenance identity and never masquerade as ordinary forbidden
edges. All are ordered canonically and follow existing strict/audit projection.

## Risks / Trade-offs

- [Project/namespace ownership can be incomplete in synthetic or unusual
  builds] → retain the missing/ambiguous mapping as unassessable native evidence
  rather than guessing an owner.
- [Embedding new evidence in a shared public record extends Core API] → update
  the reviewed public API snapshot only through its explicit lifecycle and add
  model/cache/output compatibility tests.
- [Topology data could be re-counted by consumers] → expose only canonical
  evidence from Core and document that renderers must not parse topology YAML or
  scan source/assemblies.
- [A large graph could create noisy findings] → deduplicate by component
  direction and emit one deterministic witness, while retaining bounded native
  evidence for drill-down.

## Migration Plan

The evaluator is opt-in: policies without `topology` remain unchanged. A
partial topology can be adopted without treating an unmapped subject as a
completeness failure; changing to exhaustive activates required mapping
evidence. Removing the new implementation cleanly restores the previous
no-evaluator behavior because no policy is rewritten.

## Open Questions

None. The reviewed #505–#508 contracts establish the mapping, completion, and
projection boundaries used here.
