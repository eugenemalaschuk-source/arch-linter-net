## Why

The initial metric-budget implementation correctly reuses metric evaluation, but
the surrounding static-policy, cache-format, packaged-schema, and approval
contracts do not yet recognize the new payload and contract type. Those gaps
make policy-context export fail, hide governance weakening, and leave the
published cache and package smoke contracts inconsistent with the writer.

## What Changes

- Project metric-budget contracts into policy-context facts, including metric
  identity and both absolute bounds.
- Recognize relaxed maximum and minimum bounds as directional policy weakening.
- Publish a current analysis-cache schema that admits `MetricBudgetPayload`,
  while retaining the frozen 0.6.1 cache schema as a legacy read contract.
- Move packaged-schema validation to the current normalized-finding and cache
  schemas, with explicit legacy invariants for frozen resources.
- Update the reviewed Core API and raw-validator-order approvals, and add
  regression coverage for every repaired cross-cutting contract.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-metric-budgets`: budget contracts participate in static policy
  context and policy-weakening semantics.
- `analysis-cache`: the advertised write schema covers every cache payload the
  current writer can emit while legacy schema bytes remain readable.
- `packaged-schema-registry`: current packaged schema resources and package
  validation distinguish current write contracts from frozen legacy contracts.
- `policy-context-export`: effective context exports typed metric-budget facts.
- `policy-weakening-guardrails`: directional metric-budget bound relaxations
  are reported as semantic weakening.

## Impact

Core policy-context projection and comparison, cache schema packaging and
compatibility manifest entries, the package-validation workflow, public API
approval data, raw validation pipeline approvals, and focused Core tests.
