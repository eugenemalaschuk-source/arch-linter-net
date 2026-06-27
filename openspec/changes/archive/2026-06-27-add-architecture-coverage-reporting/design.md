## Context

Coverage contracts (`scope: namespace`, `scope: rule_input`) already run and produce `ArchitectureViolation` findings with status strings `"uncovered namespace"`, `"unresolved"`, and `"empty-input"` (see `ArchitectureContractRunner.Coverage.cs`). `ArchitectureCoverageExclusion.Reason` already carries exclusion reason text but it is discarded after the exclude filter runs — no report shows which items were excluded or why. `scope: project`, `scope: assembly`, and `scope: dependency_edge` are reserved and rejected at load time (`ArchitectureContractLoader`), so the engine has no data for those dimensions yet.

Today the only coverage output is the flat findings list (`Coverage findings:` in human mode, `coverage_findings` array in JSON). There is no summary of how much of the policy surface is covered vs. excluded vs. uncovered vs. stale vs. unknown, and CI/dashboard consumers have to recompute counts themselves from the findings array.

## Goals / Non-Goals

**Goals:**
- Compute a deterministic coverage summary per coverage contract that ran, with counts for covered / excluded / uncovered / stale / unknown.
- Surface exclusion reason text in the summary (not just in YAML).
- Map existing finding statuses to the five summary buckets without changing the underlying coverage validation logic:
  - `scope: namespace`: covered (matched layer/template/exclude), excluded (matched `exclude`), uncovered (`"uncovered namespace"` findings).
  - `scope: rule_input`: stale (`"empty-input"` — rule no longer matches any code), unknown (`"unresolved"` — referenced layer name doesn't exist), excluded (rule's `contract_id` matched an `exclude` entry).
  - `scope: project` / `scope: assembly` / `scope: dependency_edge`: `ArchitectureContractLoader` already rejects these scopes at load time, so no contract instance for them ever reaches the runner — there is nothing to summarize. The summary simply never emits entries for these scopes; this is documented as a known limitation rather than modeled as a placeholder/zero-count entry.
- Render the summary in both human-readable (deterministic ordering, PR-review friendly) and JSON form, reusing `ArchitectureDiagnosticFormatter` conventions (snake_case JSON keys, alphabetical ordering by contract id/name).
- Wire into the existing `validate` command so `--format human` and `--format json` both include the summary alongside existing violations/cycles/coverage_findings output, with no breaking changes to those existing shapes.

**Non-Goals:**
- Implementing `scope: project`, `scope: assembly`, or `scope: dependency_edge` evaluation — out of scope per issue #102's non-goals and tracked separately.
- A standalone `coverage` CLI subcommand — the summary is added to existing `validate` output rather than introducing new CLI surface, keeping the change additive and consistent with current command structure.
- SARIF output — only documented as a future-compatible JSON shape; no SARIF writer is added.
- Performance work.

## Decisions

**Reuse `ArchitectureViolation` status strings as the summary's source of truth, rather than re-deriving coverage state independently.** The coverage runner already classifies each finding; introducing a second classification path risks the two disagreeing. The summary builder takes the same `ArchitectureCoverageInventory` plus the already-computed coverage findings list and exclusion config as input, and buckets by existing status string and contract scope.

**Add a small new type, `ArchitectureCoverageSummary` (in `ArchLinterNet.Core/Reporting`), rather than overloading `ArchitectureViolation`.** Violations represent "what's wrong"; the summary represents "what's the state of the world," including the `covered` count which never appears as a violation. Keeping it a separate read model avoids stretching the violation type with fields (like total covered count) that don't belong on a per-item diagnostic.

**Compute the summary inside `ArchitectureContractRunner` (alongside `CheckCoverageContract`) rather than reconstructing it in the CLI/formatter layer from findings alone.** Some counts (covered, excluded) require the full namespace/contract_id universe — not just the failing items — which only the runner has cheap access to via `ArchitectureCoverageInventory`. The formatter only renders an already-built summary, matching the existing split between execution and reporting.

**JSON shape:** add a top-level `coverage_summary` array (one entry per coverage contract) to `FormatResultForCiArtifacts`, sibling to the existing `coverage_findings` array — not nested inside it — so existing consumers of `coverage_findings` are unaffected and new consumers can opt into `coverage_summary`.

```json
{
  "coverage_summary": [
    {
      "contract": "feature-namespace-coverage",
      "contract_id": "feature-namespace-coverage",
      "scope": "namespace",
      "counts": { "covered": 12, "excluded": 2, "uncovered": 1, "stale": 0, "unknown": 0 },
      "excluded_items": [{ "item": "MyApp.Features.*::Generated", "reason": "Generated code is excluded..." }],
      "uncovered_items": [{ "item": "MyApp.Features.Billing", "evidence": "MyApp.Features.Billing.Invoice" }]
    }
  ]
}
```

Only `namespace` and `rule_input` scopes ever appear, since those are the only scopes the loader allows to be declared today.

**Human output** adds a `Coverage summary:` section after `Coverage findings:`, one line per contract: `- [id] scope: covered=N excluded=N uncovered=N stale=N unknown=N`, with excluded/uncovered items listed indented below when non-empty (mirroring the existing `FormatCoverageForHumans` indentation style).

**Ordering:** summary entries ordered by contract id (falling back to name) using `StringComparer.Ordinal`, matching existing formatter conventions elsewhere in `ArchitectureDiagnosticFormatter`. Excluded/uncovered item sub-lists ordered the same way as their source collections already are (namespace/contract_id, ordinal).

## Risks / Trade-offs

- **Risk:** Bucketing rule-input `"empty-input"` as "stale" and `"unresolved"` as "unknown" is a naming choice not previously formalized in code (only in issue/commit-message prose). → Mitigation: document the mapping explicitly in `docs/contracts/coverage.md` so the terms are stable and discoverable, and cover both mappings with explicit tests.
- **Risk:** Adding a new top-level JSON array could be missed by tooling that does strict JSON schema validation against CI artifacts. → Mitigation: it's purely additive (new key, no existing key changes shape), consistent with how `policy_consistency_findings` was added previously.
- **Risk:** Reviewers may expect `project`/`assembly`/`dependency_edge` rows in the summary since the issue mentions them by name. → Mitigation: call out explicitly in `docs/contracts/coverage.md` and the CLI docs that the summary only covers scopes the loader currently allows (`namespace`, `rule_input`), consistent with the existing "Current limits" section.

## Migration Plan

Purely additive: no existing CLI flags, JSON keys, or YAML fields change behavior. No migration steps needed; documentation updated in the same PR per issue requirements.

## Open Questions

None — scope is bounded by reusing the existing coverage engine output; no unresolved technical decisions remain.
