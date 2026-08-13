# Packed-artifact consumer-cleanup gate

This is internal project documentation. It is intentionally excluded from the public MkDocs/GitHub Pages product site.

## Scope

The v0.6.0 packed-artifact gate (#366) proves that one immutable candidate installs and behaves identically on every supported platform. It does not prove what v0.6.1 exists to prove: that the F1–F11 correctness fixes plus source-set authoring (#465) let a real external consumer delete its 0.6.0 adoption workarounds.

The consumer-cleanup matrix (#466) adds that proof. It runs inside the same
`CheckpointBReleaseGateTests` entrypoint, against the candidate tool and packages installed from the isolated local feed — never against a source-tree `ProjectReference`.

## Synthetic modular consumer

`tests/ArchLinterNet.Core.Tests/AdoptionAcceptance/Fixtures/modular-consumer` is the release's policy-shape evidence, not just a test input. It is a 23-project synthetic solution: 20 module assemblies, shared abstractions, a composition host, and one deliberately excluded test project. All identities are synthetic; no private adopter repository, namespace, or fixture is referenced.

Its composed policy is authored the way the release intends a consumer to author one:

| Governance concern | Authored shape | Removed 0.6.0 workaround |
|---|---|---|
| Module → abstractions direction | one `strict_assembly_allow_only` over `source_sets: [module_assemblies]` | 20 copied contract blocks differing only by `source` |
| Module → host direction | one `strict_assembly_dependency` over the same set | 20 more copied contract blocks |
| Project metadata | two contracts sharing `project_sets: [production_projects]` | the same `.csproj` inventory repeated per contract |
| Composition boundary | one `allowed_only_in_namespaces` glob | a literal namespace per module, silently matching nothing |
| Intentional layer overlap | `overlaps_with` on the narrower layer | globally relaxing `analysis.policy_consistency` |
| Policy size | four imported fragments | a forced policy monolith |
| Reviewed public API | snapshot file with `api_comparison: exact` | a bulk inline `declared_api` inventory |

`dependencies.cycles.arch.yml` is a companion probe policy for the strict-cycles baseline scope regression. It declares one genuinely cyclic contract and one ordinary acyclic inter-layer contract over the same scanned assemblies, so baseline update must persist candidates for the first and nothing for the second.

## Scenario inventory

| Scenario | Finding |
|---|---|
| `composed-policy-assembly-free-check` | F1 |
| `non-destructive-ensure-built` | F2 (CLI) |
| `packaged-testing-ensure-built` | F2 (packaged Testing API) |
| `public-api-snapshot-workflow` | F3 |
| `strict-cycles-baseline-scope` | F4 |
| `dependency-contract-id-parity` | F5 |
| `actionable-schema-diagnostics` | F6 |
| `missing-shared-framework-diagnostic` | F7 |
| `layer-overlap-allowance` | F8 |
| `namespace-allowance-pattern` | F9 |
| `json-configuration-error-format` | F10 |
| `release-identity-consistency` | F11 |
| `source-set-assembly-authoring` | #465 |
| `discovered-project-set-authoring` | #465 |
| `source-set-enrolment` | #465 |
| `stale-source-selector-fail-closed` | #465 |
| `consumer-policy-shape` | policy-shape acceptance |

Every scenario is required on every platform in the matrix. A scenario that a platform cannot execute is recorded as `not_applicable` with a reason and must pass on at least one platform.

## Public-API surface-selector consumer-exit matrix

v0.6.4 (#525/#526/#529, story #527) lets `strict_public_api_surface`/`audit_public_api_surface`
contracts select an intentional reviewed compatibility surface with `surface_selector`, instead of
governing every exported type in an assembly. The consumer-exit matrix proves this from the same
packed candidate, using a dedicated
`tests/ArchLinterNet.Core.Tests/AdoptionAcceptance/Fixtures/api-surface-selector` fixture rather
than extending `modular-consumer` — the two fixtures prove unrelated release narratives and keeping
them separate keeps this addition from risking the already-passing 0.6.1 scenarios.

The fixture governs one assembly with a deliberately large incidental exported surface (implementation/domain/configuration types with no compatibility intent) alongside three sibling `strict_public_api_surface` contracts over the same assembly: `assembly-wide-api` (no selector — the unchanged #94 baseline), `marker-selected-api` (`has_attribute` — the primary user-owned orthogonal-marker adoption path), and `namespace-selected-api` (`namespace` — a second bounded selector source, proving selection is not annotation-specific). A `strict_context_allow_only` contract keyed on `role: ValueObject` proves a selected type's existing semantic role stays governed by ordinary role-based rules, unchanged by selection.

| Scenario | Proves |
|---|---|
| `public-api-surface-selector-snapshot-reduction` | Selected snapshots omit incidental types the assembly-wide sibling still governs; selected contracts validate independently of the assembly-wide one; BCL-only members need no evidence; the orthogonal marker is never mapped into classification |
| `public-api-surface-selector-role-preservation` | A selected `ValueObject`-role type stays governed by an ordinary role-based contract, unchanged by selection |
| `public-api-surface-selector-exact-delta-lifecycle` | Added/removed/changed signatures on a selected type are all observed exactly, and `update` restores a clean comparison |
| `public-api-surface-selector-membership-review-visibility` | Adding or removing selector-matching evidence on a type is a review-visible snapshot delta in both directions |
| `public-api-surface-selector-escape-fails-closed` | A selected member referencing an unselected first-party exported type fails closed instead of silently escaping |
| `public-api-surface-selector-strict-run-is-green` | The full policy, with every permanent selector contract in place, validates green |
| `public-api-surface-selector-testing-parity` | The CLI and the packaged `ArchLinterNet.Testing` API resolve the same effective selected surface and normalized findings |

These scenarios are required alongside the existing consumer-cleanup matrix; the same tracked-defect and not-applicable rules apply.

## Release-scope closure

`tools/release/release-scope.json` declares the authoritative release scope for the *current*
release cycle: the required items, and the items explicitly excluded with their reasons. It lives
in the repository so the set of blocking work is reviewed like any other release artifact rather
than derived from mutable issue text at release time. It advanced from the shipped 0.6.1/#434 scope
to the current 0.6.4/#527 scope when the surface-selector consumer-exit matrix was added.

`create_release_scope_evidence.py` resolves only the *current* state of those issues from the issue
tracker and binds the result to the candidate manifest digest and source commit. The aggregator
refuses to authorize publication while any required item is open, and emits the inventory —
including exclusions — in both the JSON and Markdown evidence.

Adding release-blocking work under the story means adding it to the declaration in the same change.

## Tracked defects

A consumer-cleanup scenario whose failure is a known, separately tracked product defect is listed in `_trackedConsumerCleanupDefects` in
`tests/ArchLinterNet.Core.Tests/CheckpointBReleaseGateTests.ConsumerCleanup.cs`.

The registry does not suppress anything. A registered scenario is still recorded as `failed` in the platform evidence, and `tools/release/aggregate_checkpoint_b_evidence.py` refuses to authorize publication while any required scenario is failed. The registry only keeps the executable gate honest in both directions: a **new** failure fails the test immediately, and a tracked defect that has silently been fixed also fails it, so the entry gets removed and the scenario gates the release again.

Adding an entry is a release decision, not a test-maintenance convenience. Every entry must name the tracking issue.

## Where it runs

The gate is its own **packed-artifact** bucket (`TEST_PACKED_ARTIFACT_FILTER`, run via `make test-packed-artifact`), not the unit bucket and not the ordinary E2E bucket: it packs the whole solution, installs the tool from an isolated feed, and builds the synthetic consumer fixtures across the consumer-cleanup matrix, so leaving it on the unit critical path serialized it behind ~2,500 unit tests, and leaving it in ordinary E2E made that bucket contend with it on the same runner.

The bucket runs without coverage instrumentation — `make test-coverage` collects only from the unit bucket, by design — so `CheckpointBReleaseGateTests*.cs` is listed in `sonar.coverage.exclusions`. The harness is fully executed on every run; only its line coverage is unmeasurable, and reporting it as untested would be wrong.

For the same reason the coverage targets no longer invoke it at all: `test-coverage`/`test-coverage-main-ci` run only the unit bucket, so they don't spend time on a run that produces no coverage. `make test`, `make acceptance`, and the independent `test-packed-artifact` CI jobs still run it on every platform, so the correctness signal is unchanged — it just runs on its own runner instead of contending with unit or E2E.

The release workflow runs the same entrypoint on all four platforms against the real candidate feed; that run, not the PR run, is what authorizes publication.

## Release evidence

Each platform job writes `checkpoint-b-platform-evidence/v1` including typed `policy_shape` counters. The aggregator validates the platform matrix, the scenario inventory, the policy shape, and the independently produced repository gates, then emits `checkpoint-b-release-evidence.json`/`.md` with an explicit PASS or FAIL publication statement and exits non-zero on FAIL.

The aggregator rejects a candidate whose canonical consumer policy still needs a workaround shape, even when every scenario passed: a forced monolith, directional assembly contracts authored per module, a copied project inventory, or an inline public-API inventory.

Run its regressions with:

```bash
make test-release-evidence
```

## Current verdict

The 0.6.1 consumer-cleanup matrix (thirty scenarios) was executed against a locally packed `0.6.1`
candidate at the commit that introduced it and passed with an empty tracked-defect registry.

The 0.6.4 public-API surface-selector consumer-exit matrix (seven scenarios) was validated locally
against a packed candidate through a temporary focused harness isolating the new scenarios from the
rest of the entrypoint, since an unrelated pre-existing platform-line-ending issue in
`layer-overlap-allowance` (tracked separately) currently prevents the full sequential entrypoint from
reaching them locally on this environment. All seven new scenarios passed. The full entrypoint,
including both matrices together, is exercised on every PR and release-workflow platform job; that
run is authoritative for anything a local, risk-based check cannot fully reproduce.

Executing the matrix originally found one real defect — on a composed policy the effective-schema failure reported an unrelated imported-fragment location and resurfaced inapplicable discriminator branches ([#471](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/471)). It was fixed in the same branch, so `actionable-schema-diagnostics` gates the release rather than being registered as a known failure.

A PASS from a single local platform is not a release authorization: publication requires the aggregated four-platform evidence produced by the release workflow.
