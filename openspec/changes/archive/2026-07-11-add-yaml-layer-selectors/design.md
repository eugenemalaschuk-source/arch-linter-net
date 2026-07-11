## Context

The policy schema reserves a `selector` object on each layer, while the
runtime `ArchitectureLayer` model drops it during YAML deserialization. Layer
resolution is currently namespace-only and is reused by many contract families,
the coverage inventory, and configuration checks. `ArchitectureRoleIndex` is
already session-scoped and lazily computes role descriptors for the same type
universe used by `ArchitectureTypeIndex`.

The existing semantic-classification design required `namespace` because the
old resolver parsed every layer's namespace unconditionally. Issue #111 changes
that boundary: selector-only layers are now supported. Namespace-only policies
must retain their exact, glob, suffix, and external behavior.

## Goals / Non-Goals

**Goals:**

- Deserialize selector role and scalar metadata constraints into the layer model.
- Resolve layer types by selector, namespace, or both, with both constraints
  required to match when both are present.
- Reuse the per-run role index and compare canonical role-index metadata values
  using exact ordinal/key equality and numeric-domain equality already defined
  by semantic classification.
- Make all existing layer-bearing execution paths consume the same resolved type
  set where technically applicable.
- Validate malformed selector data early and report empty selector matches as
  deterministic configuration diagnostics.
- Keep diagnostics clear about namespace versus semantic selection.

**Non-Goals:**

- New classification sources, role catalogs, wildcard/regex selector values, or
  contextual contract families.
- Replacing namespace matching or changing namespace glob/suffix semantics.
- Adding semantic-role coverage scope or changing informational classification
  findings into pass/fail contract violations.

## Decisions

1. **Typed selector model.** Add an `ArchitectureLayerSelector` with required
   non-empty `Role` and a scalar metadata map. This mirrors the JSON schema and
   keeps selector intent explicit rather than representing it as arbitrary YAML.

2. **Single session-aware matcher.** Add a session-level layer type lookup that
   first applies namespace matching when a namespace exists, then filters by the
   role index. `ArchitectureTypeIndex` remains the namespace/type-universe
   index; it does not gain a dependency on classification. Existing execution
   callers route through the session helper so all contract families share the
   same semantics.

3. **AND and deterministic ordering.** Namespace and selector predicates are
   AND-combined. Selector results follow the role index/type-index deterministic
   order. A selector-only layer does not parse `GlobPattern`; namespace-specific
   operations return no namespace match for it rather than throwing.

4. **Configuration diagnostics at policy validation/execution boundary.** A
   selector must have a role, metadata keys must be non-empty, and selector
   values must be supported scalar forms. A declared selector with zero matched
   types is reported using the existing configuration/empty-layer diagnostic
   path, with wording that identifies semantic selection. External layers keep
   their existing suppression behavior.

5. **Schema and docs are synchronized with runtime.** Remove the schema's
   namespace requirement for layers, retain selector-only and namespace+selector
   examples, and update the semantic-classification and layer documentation to
   state that selectors are implemented and exact/AND-combined.

**Alternatives considered:**

- Adding selector logic separately to each contract family was rejected because
  it would produce inconsistent matching and diagnostics.
- Making `ArchitectureTypeIndex` depend directly on `ArchitectureRoleIndex` was
  rejected to preserve the existing index boundary and avoid a construction
  cycle.
- Treating namespace and selector as OR was rejected because it makes a layer
  unexpectedly broad and conflicts with the reviewed exact selector semantics.

## Risks / Trade-offs

- **Risk:** Some namespace-centric diagnostics and containing-layer logic have
  no meaningful namespace for selector-only layers. → **Mitigation:** guard
  namespace-only paths, use selector descriptions, and add focused tests for
  each affected path.
- **Risk:** A broad role selector may match many types and alter existing
  contract results when enabled. → **Mitigation:** selector-only policies are
  opt-in, empty matches are explicit diagnostics, and matching remains exact.
- **Risk:** Existing tests may assume every layer has a namespace. →
  **Mitigation:** retain namespace defaults in test fixtures and add schema/runtime
  regression cases for selector-only layers.

## Migration Plan

No data migration is required. Existing policies continue to deserialize and
match by namespace unchanged. Authors can add `selector` incrementally; a
selector-only layer becomes available once its role classification is present.
Rollback is a branch/release rollback because the schema and runtime changes are
additive for existing policy documents.

## Open Questions

None. The issue acceptance criteria resolve the previous design ambiguity in
favor of supporting selector-only layers while preserving namespace behavior.
