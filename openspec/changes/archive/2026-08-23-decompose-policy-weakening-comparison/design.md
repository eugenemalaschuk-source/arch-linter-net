## Context

`ArchitecturePolicyWeakeningComparer` currently implements six independent
rule families in one 521-line public static type: enforcement, authored
analysis scope, static/source-set/source-expansion scope, contract facts and
optionality, exceptions, and selectors/membership evidence. It also directly
calls `ArchitecturePolicyWeakeningFormatter.ValidateComparableContexts` and
`TryGetMembership`. The formatter therefore mixes artifact validation and
comparison support with deserialization, canonical digesting, result
normalization, and Human/JSON/SARIF projection.

The existing public comparer, formatter, and models are reviewed Core API.
The v0.7 policy-context artifacts are the complete trust boundary: comparison
must neither reload YAML nor inspect a candidate repository. The active
`decompose-god-classes` OpenSpec change has no overlap with this package.

## Goals / Non-Goals

**Goals:**

- Keep the public comparer signature and result model unchanged.
- Make each pre-existing rule family independently changeable behind an
  internal, purpose-named evaluator.
- Keep one façade-owned aggregation stage responsible for identity
  de-duplication and ordinal final ordering.
- Relocate context compatibility validation, membership resolution, and digest
  calculation to comparison/shared support; preserve the formatter's public
  digest method by delegation.
- Prove behavior through focused family tests, a cross-family aggregation
  regression, public API review, architecture, size, and formatting checks.

**Non-Goals:**

- No new weakening kinds, directional rules, schema version, severity, public
  API, formatter wording, output shape, YAML loading, candidate-policy
  analysis, plugin framework, or policy editing capability.
- No broad test-fixture reorganization or unrelated cleanup.

## Decisions

### Retain a thin public façade and use internal static evaluators

The comparer will validate its request, construct the existing maps it needs,
invoke one named internal evaluator per existing rule family, then de-duplicate
and order its collected normalized findings exactly as today. Evaluators append
findings using existing canonical comparison helpers; they do not introduce an
interface, registry, or plugin model. This keeps the public orchestration and
final aggregation obvious while giving each family an exclusive change point.

An interface/registry was rejected because all evaluators are fixed,
compile-time internal rules with a known ordering and no runtime extensibility
requirement.

### Create a comparison context-support boundary

`ArchitecturePolicyWeakeningContextSupport` will own complete-context
validation, compatible-pair validation, membership-evidence verification, and
the canonical SHA-256 digest over the existing policy-context JSON. The
formatter will call this seam for deserialization and retain
`ComputeContextDigest` as a public forwarding API. The comparer and selector
evaluator will call the same seam directly, eliminating their formatter
dependency without copying validation or digest rules.

Moving only the two comparer-called methods was rejected because membership
verification needs the same canonical digest rule, and retaining that rule in
the formatter would leave the semantic comparison dependency in place.

### Preserve existing normalized-finding helpers

`ArchitecturePolicyWeakeningComparisonSupport` remains the single source for
control identity, fact/selector evidence, exception keys, and normalized
finding construction. Family evaluators reuse it rather than making family
local copies. The façade continues to de-duplicate by finding identity and
sort by kind, control identity, then identity with `StringComparer.Ordinal`.

## Risks / Trade-offs

- [Moving methods changes a byte-stable projection or finding evidence] →
  retain the existing formatter and comparer regressions, add a cross-family
  ordering/de-duplication assertion, and compare public API snapshots.
- [Support extraction duplicates digest or validation semantics] → route both
  formatter and comparison through the one internal context-support method.
- [Several evaluators obscure evaluation ordering] → preserve the current
  façade call sequence and a single final normalization point.
- [Internal extraction becomes a speculative framework] → use direct static
  evaluator calls with fixed signatures and no registration abstraction.

## Migration Plan

1. Add the context-support seam and delegate formatter compatibility APIs to
   it.
2. Extract the six current method bodies into named internal evaluators and
   reduce the comparer to request validation, orchestration, and aggregation.
3. Add focused family and cross-family regressions; run risk-based Core and
   repository checks.
4. Archive the OpenSpec change after synchronization. Rollback is a safe
   reversion of the internal movement because public contracts and serialized
   output are unchanged.

## Open Questions

None. The issue and existing policy-weakening specification define the
required behavior and compatibility boundary.
