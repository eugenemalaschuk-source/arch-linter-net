# 0.5.1 Adoption-Stabilization Final Consistency Audit

## Scope and decision

This is the final repository-wide reconciliation required by #411. It compares
the `adoption-stabilization-compatibility` specification with the implementation,
child capability specifications, schema registry, CLI and Testing surfaces,
acceptance corpus, evidence, and public documentation on 2026-08-05.

**Result: coherent for Checkpoint B after the PR #431 review corrections.** All
implementation/documentation children listed by #354 are closed, no unresolved
contract contradiction remains, and #366 may run the packed-artifact release
matrix once this PR is merged. This audit is not Checkpoint B and does not
authorize publishing 0.5.1.

OpenSpec is not otherwise applicable to #411: it changes no product behaviour,
public API, schema, or compatibility guarantee. The existing
`adoption-stabilization-compatibility` requirement, **Final max consistency gate
is mandatory**, already specifies this audit and its release boundary.

## Sources examined

- `openspec/specs/adoption-stabilization-compatibility/spec.md` and all related
  active capability specifications;
- `docs/internal/adoption-stabilization-compatibility.md`, the build-state and
  profile blueprints, and the #403 corpus/evidence documents;
- `schema/0.5.1/compatibility-manifest.json` plus every listed schema resource;
- CLI option/help definitions, `CliExitCodes`, Core/Testing contracts, and their
  focused NUnit coverage;
- public migration, reference-entrypoint, output, exit-code, capabilities, and
  release-note documentation; and
- #354's execution queue together with prerequisite issue status.

All prerequisite implementation, documentation, schema, cache, profile, and
parallelism tasks in #354 are closed. #403 supplies the shared synthetic corpus;
#366 remains open as the only packed-artifact release gate.

## Reconciliation results

| Area | Canonical contract | Observed reconciliation |
| --- | --- | --- |
| Release boundary | `0.5.1` / `adoption-stabilization/v1`; Checkpoint A is internal only | Blueprint, migration guide, capabilities page, release notes, release process, and Checkpoint A evidence agree. No Checkpoint A wording authorizes publication. |
| Registry and schemas | Immutable release-qualified package registry | The manifest lists policy root/fragment, baseline v2, API snapshot, normalized finding, build state, cache, and profile. Each resource uses a 0.5.1 ID and digest; profile correctly declares write-only support. |
| Finding and baseline identity | `finding/v1`; baseline v2 with `identity_version: 1` | Documentation and compatibility spec keep identity separate from display text, paths, timings, and report destinations; migration guidance preserves v1 reader semantics and explicit requalification. |
| Snapshot and build state | `analysis-build-state/v1` owns inputs, artifacts, completed session, and snapshot ownership | Cache/profile/parallel specifications consume that ownership model rather than defining a second equality or snapshot key. |
| Reporting | One normalized result; repeatable `--report` | CLI help and migration/output guidance retain legacy `--format`/`--json`, reserve command `--output` for artifacts, and describe per-file commit with typed `partial-output`, not a global transaction. |
| Cache, profile, and parallelism | Opt-in verified cache; evidence-only profile; bounded deterministic work | CLI/default documentation and profile evidence agree: cache is disabled by default, `--max-parallelism 1` is supported, and default bounded work cannot change canonical findings or exit category. |
| Cancellation and exit statuses | Numeric categories 0/1/2 plus typed completion | `CliExitCodes`, CLI help, output/exit guidance, and profile dictionary consistently reserve `2` for incomplete execution including output failure, partial output, and cancellation. |
| CLI, Testing, and generic CI | Equivalent load-bearing semantics | The shared acceptance corpus and migration/reference-entrypoint guidance use one policy/result model; provider-specific GitHub Actions content is documented as an example, not a product semantic dependency. |
| Privacy and examples | Synthetic adopter-facing evidence only | Corpus fixture names, namespaces, policies, reports, and migration examples are synthetic. Repository-maintenance links remain product metadata, not adopter identities. |

## Divergences corrected

The audit found one repository-state divergence: the already merged cache-boundary
and parallel-evidence implementation still had a completed active OpenSpec change.
It is now archived as
`2026-08-05-fix-cache-boundary-and-parallel-evidence`; its nine requirement
additions were synchronized into the six owning capability specifications.
There are no remaining active changes or contradictory active capability claims.

PR #431 review then identified four final consistency defects. This corrective
change resolves them in their owners: `diagnostics-model` now has a substantive
purpose with no archive placeholder; #354's authoritative queue identifies #373
as completed rather than in progress; `analysis-snapshot` consistently defines
metadata-only `CreateSnapshot` preparation, cache lookup before runner
materialization, and `AssemblyLoads` as lazy post-miss loads; and the generated
post-optimization JSON and Markdown record source, executed Debug binary, and
the selected packed CLI package ID/version/SHA-256 separately. The correction is
archived through `fix-final-consistency-gaps` and
`reconcile-snapshot-lazy-wording`, so those owning specifications are the single
source of truth.

## Migration and support-contract disposition

The 0.5.0-to-0.5.1 guide covers root/fragment imports, selector/source-set
expansion, legacy baseline v1 and baseline-v2 identity review, baseline lifecycle,
public API snapshots, normalized JSON/SARIF/Testing findings, explicit build-state
preparation, opt-in cache, profiles, concurrency, offline schema discovery, and
provider-neutral CI wrappers. It does not rely on private repository knowledge or
automatic debt approval.

Checkpoint A has observed macOS x86_64 scoped evidence only and explicitly makes
no release support claim. Linux x64, Windows x64 with PowerShell, macOS arm64,
macOS x86_64 regression coverage, redirected/non-TTY execution, offline installed
artifacts, sequential execution, and generic CI are therefore not represented as
completed release evidence here. #366 owns the executable packed-artifact matrix
for those claims. This keeps the current public documentation conditional on the
single 0.5.1 release contract rather than overclaiming a released support matrix.

## Verification record

- `rtk openspec archive fix-cache-boundary-and-parallel-evidence --yes` — passed;
  archived and synchronized the completed child change.
- `rtk openspec validate --all` — passed after archiving both completed
  corrective changes.
- The final implementation validation remains `rtk make fmt` followed by
  `rtk make acceptance`; those commands are run for this audit change before PR.

## Next boundary

#366 may execute against this coherent release model. A green #366 is still
required before #354 may close or version 0.5.1 may be published.
