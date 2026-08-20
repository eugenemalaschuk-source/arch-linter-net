## Context

The CLI can already perform complete validation, export a dependency graph, emit normalized findings, maintain exact-identity baselines, and emit coverage and semantic-classification facts. Those outputs answer isolated questions but do not provide a stable comparison unit for a PR. Comparing raw validation JSON is insufficient because it does not retain all observed surfaces or dependency edges, while re-running only changed projects would violate the issue's authoritative analysis boundary.

## Goals / Non-Goals

**Goals:**

- Persist a versioned, deterministic snapshot of a complete architecture analysis.
- Compare two snapshots by stable identities and report additions/removals independently from finding/debt classification.
- Reuse the graph, coverage, semantic-role, normalized-finding, and baseline identity models.
- Offer a composable `change snapshot` / `change report` CLI surface for CI and local PR work.

**Non-Goals:**

- Inspecting Git history, checking out a base ref, calling GitHub APIs, or selecting a changed-file or changed-project subset.
- Changing strict/audit validation semantics, suppressing a violation, or writing a baseline.
- Creating a dashboard, auto-approving changes, or inferring policy changes as safe.

## Decisions

1. **Use a dedicated `architecture-change-snapshot/v2` artifact.** A `change snapshot` command runs a complete requested-mode analysis and serializes the resulting namespaces, project and assembly identities, semantic roles/contexts, dependency edges, coverage blind spots, findings, and baseline-state facts. A snapshot records its schema/version, mode, and condition-set scope so a report can reject unsupported, incomplete, or mismatched input. This is preferable to overloading ordinary validation JSON, which deliberately omits several observed surfaces.
2. **Compare two persisted snapshots, never a Git ref.** `change report --base <path> --current <path>` is deterministic and permits CI to create the base snapshot in its own full-analysis job. This avoids making Git availability or a branch checkout an implicit analysis authority.
3. **Use canonical identities as set keys.** Surface entries have stable typed IDs; dependency edges use `(level, source, target)`; roles include subject/role/sorted metadata; findings use the existing normalized canonical identity. Comparison outputs are ordinally sorted by kind then identity. This preserves same-named entities from distinct assemblies and makes JSON suitable for an AI consumer.
4. **Report drift and debt separately.** Added/removed surfaces are structural deltas. Current findings whose identity is absent from the base are `new`; identities present in both are `existing`; matched baseline identities are separately listed as `baseline_debt`. No category is treated as a pass/fail decision by the report command.
5. **Keep Core comparison and CLI transport separate.** Core owns snapshot records, validation, construction, comparison, JSON serialization, and human formatting. The CLI module validates paths/options, delegates to the Core application service, and routes output. This follows the existing instance-handler/module composition and preserves CLI/Core boundaries.

## Risks / Trade-offs

- [Snapshots from different policies or scopes can create noisy deltas] → require CI to pair snapshots produced from the same policy inputs, and reject incompatible schema, requested mode, or condition-set scope before comparison.
- [A graph or semantic shape evolves] → version the artifact and reject unknown versions rather than silently interpreting it as empty data.
- [Large reports can be noisy] → keep full JSON lossless but use ordered per-kind counts and bounded human representatives with an explicit omitted count.
- [Baseline suppression hides debt] → snapshot baseline entries as an explicit identity collection rather than inferring debt from the absence of a current violation.

## Migration Plan

The feature is additive. CI can first produce full base/current snapshots and optionally consume the report as an artifact; no existing validation or baseline invocation changes. Rollback is a normal code revert and leaves snapshots as inert build artifacts.

## Open Questions

None.
