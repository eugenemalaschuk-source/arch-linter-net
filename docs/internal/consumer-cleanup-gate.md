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

## Release-scope closure

`tools/release/release-scope.json` declares the authoritative #434 release scope: the required
items, and the items explicitly excluded with their reasons. It lives in the repository so the set
of blocking work is reviewed like any other release artifact rather than derived from mutable issue
text at release time.

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

The gate is in the `make test` **E2E** bucket (`TEST_E2E_FILTER`), not the unit bucket: it packs the whole solution, installs the tool from an isolated feed, and builds the synthetic consumer fixtures, so leaving it on the unit critical path serialized it behind ~2,500 unit tests.

That bucket runs without coverage instrumentation — `make test-coverage` collects only from the unit process, by design — so `CheckpointBReleaseGateTests*.cs` is listed in `sonar.coverage.exclusions`. The harness is fully executed on every run; only its line coverage is unmeasurable, and reporting it as untested would be wrong.

The release workflow runs the same entrypoint on all four platforms against the real candidate feed; that run, not the PR run, is what authorizes publication.

## Release evidence

Each platform job writes `checkpoint-b-platform-evidence/v1` including typed `policy_shape` counters. The aggregator validates the platform matrix, the scenario inventory, the policy shape, and the independently produced repository gates, then emits `checkpoint-b-release-evidence.json`/`.md` with an explicit PASS or FAIL publication statement and exits non-zero on FAIL.

The aggregator rejects a candidate whose canonical consumer policy still needs a workaround shape, even when every scenario passed: a forced monolith, directional assembly contracts authored per module, a copied project inventory, or an inline public-API inventory.

Run its regressions with:

```bash
make test-release-evidence
```

## Current verdict

The gate was executed against a locally packed `0.6.1` candidate at the commit that introduced it. All thirty required scenarios pass and the tracked-defect registry is empty.

Executing the matrix originally found one real defect — on a composed policy the effective-schema failure reported an unrelated imported-fragment location and resurfaced inapplicable discriminator branches ([#471](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/471)). It was fixed in the same branch, so `actionable-schema-diagnostics` gates the release rather than being registered as a known failure.

A PASS from a single local platform is not a release authorization: publication requires the aggregated four-platform evidence produced by the release workflow.
