## Context

The declared-topology evaluator currently forms its mapped-node projection from
subjects with an exactly-one node classification. That projection is suitable
for directional relationship enforcement, but it is not sufficient authority
for declaration-drift inference when an observed subject is unmapped or
ambiguous: absence from the exactly-mapped projection does not prove absence
from the current observed topology.

The Core public API approval test compares a generated full surface with its
reviewed snapshot. The snapshot must be updated only through the canonical
public-API lifecycle after the final code shape is built.

## Goals / Non-Goals

**Goals:**

- Treat stale-node and stale-edge inference as a second phase that is enabled
  only after all in-scope subjects map exactly once or are reviewed out of
  scope.
- Preserve unmapped and ambiguous subjects as canonical mapping evidence and
  applicability reasons without adding inferred drift findings.
- Prove both review regressions with focused tests and restore the generated
  public API approval baseline.

**Non-Goals:**

- Do not change topology node classification, allowed-edge enforcement, or the
  partial/exhaustive membership model.
- Do not suppress direct forbidden-edge findings whose endpoints are exactly
  mapped.
- Do not introduce topology-specific score, baseline, waiver, or reporting
  seams.

## Decisions

### Gate all declaration-drift inference on mapping completeness

The evaluator will calculate `mappingComplete` from the canonical scoped
classifications: it is true only when no subject is `unmapped` or `ambiguous`,
and an exhaustive `allow_empty: false` scope did not resolve to zero subjects.
When false, stale-node and stale-edge collections stay empty even if
`stale_declarations` is enabled. Their absence then means drift was not
supported by complete mapping evidence, while the canonical unmapped/ambiguous
subject counts and drill-down entries explain why.

This is preferred over trying to infer drift per node or per edge because a
partially mapped graph cannot establish whether an ambiguous/unmapped endpoint
participates in a declared relationship. It avoids turning uncertainty into
multiple deterministic-but-false drift findings.

### Keep exactly-mapped relationships as the enforcement graph

Forbidden-direction enforcement remains based only on exactly-mapped source
and target subjects. This preserves the existing safety rule: incomplete
mapping cannot be guessed into a forbidden edge. The drift gate is independent
of that enforcement projection.

### Regenerate the full reviewed API surface

After code and tests are final, run the public API preview/update/check lifecycle
against a fresh build. The generated snapshot, not a manually curated subset,
is the approval authority for the public evidence types.

## Risks / Trade-offs

- [Incomplete mapping hides a genuinely stale declaration until mappings are
  fixed] → This is intentional fail-safe behavior; the result remains
  unassessable with direct mapping evidence rather than emitting a false drift
  claim.
- [Public API snapshot can drift again after code changes] → Generate it only
  after the final build and verify with both the repository command and the
  approval test.
