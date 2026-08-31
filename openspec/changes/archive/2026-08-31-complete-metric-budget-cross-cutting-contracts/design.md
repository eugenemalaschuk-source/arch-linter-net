## Context

See `proposal.md` for motivation. Metric budgets already reuse the metric
evaluator and emit canonical findings, but several cross-cutting consumers use
closed registries or versioned schema resources that were not extended with the
new contract and payload types.

## Goals / Non-Goals

**Goals:**

- Keep budget facts static, deterministic, and comparable in policy context.
- Make every schema advertised for current cache writing accept the payloads
  that the current writer can produce.
- Preserve frozen 0.6.1 schemas as explicit legacy read resources.
- Make package smoke coverage verify both current and legacy schema contracts.

**Non-Goals:**

- Change metric evaluation, threshold semantics, cache envelope identity, or
  the cache's closed-set deserialization security posture.
- Consolidate the two established public-API approval mechanisms.

## Decisions

### Add typed budget facts to the existing static projection

Project `metric`, `minimum`, and `maximum` with the existing contract facts
model and add the budget contract to the projector's closed type set. This
preserves its registry-coverage invariant and lets the generic comparison path
see explicit facts without loading assemblies or running metrics.

### Treat numeric relaxations with dedicated directional comparison

Maximum growth and minimum reduction each broaden the permitted result set, so
the comparator will report them as semantic weakening. Other numeric changes
remain bounded unless a dedicated rule proves their direction; this avoids
inferring governance semantics from arbitrary numeric facts.

### Publish a 0.8 current cache schema and retain 0.6.1 unchanged

Copy the frozen cache schema into the current schema resource family and add
the new explicit payload branch there. The compatibility manifest, registry,
packaging, and current schema resource version move together. The cache envelope
identity remains `analysis-cache/v1`; the old bytes remain packaged and readable
as a legacy contract, avoiding retroactive mutation of the released 0.6.1
document.

### Test real writer output against the advertised schema

An end-to-end cached metric-budget violation will be validated against the
current advertised cache schema. This exercises the writer, explicit converter,
manifest resolution, and schema together rather than merely comparing lists of
payload names.

## Risks / Trade-offs

- [New cache schema path changes the advertised contract] → Retain the stable
  `analysis-cache/v1` envelope identity while packaging 0.6.1 separately as an
  explicit legacy resource; current schema resolution selects 0.8.0.
- [Projection facts are optional for one-sided bounds] → Emit only configured
  bound facts and compare paired values directionally; absence remains a
  separately visible typed-fact change.
- [Two API approvals drift] → Update both deliberately in this follow-up; their
  later consolidation remains out of scope.

## Migration Plan

1. Add current schema resources and update registry/manifest/package metadata.
2. Retain 0.6.1 schema resources and test them as legacy reads.
3. Update current API/order approvals and run focused plus repository gates.
4. Archive this follow-up only after the PR's CI contracts pass.
