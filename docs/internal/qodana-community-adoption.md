# Qodana Community .NET adoption

Owner: issue #864. Lifecycle: post-v0.8.0 engineering-health stabilization.

## Final state

Phase A implementation is complete and PR #906 is merged. Phase B is complete with the reviewed decision **KEEP ADVISORY**.

`Qodana Community .NET (advisory)` remains a separate, non-required PR check. It is intentionally not added to branch protection or a ruleset. The scanner has shown stable execution and useful independent JetBrains/ReSharper diagnostics, but the current recommended profile produces a large repository-wide historical/style inventory and has no reviewed new-code/differential baseline. Making this configuration required would primarily gate scanner execution rather than a trustworthy finding-level regression policy.

The Qodana integration does not replace or redefine SonarCloud, CodeQL, ArchLinterNet self-governance, compiler/analyzer checks, package validation, architecture coverage or release authority.

## Configuration and trust boundary

`qodana.yaml` selects the free `jetbrains/qodana-cdnet:2026.2` Community engine, `qodana.recommended`, `ArchLinterNet.slnx` and Release configuration. There is no Qodana Cloud token, paid dependency, repository baseline, blanket exclusion or broad inspection suppression.

The workflow uses the ordinary `pull_request` event, an ephemeral GitHub-hosted Ubuntu runner, `contents: read`, and checkout with `persist-credentials: false`. It does not reference repository/Qodana secrets, does not use `pull_request_target`, does not grant PR/security-event write permissions, and uploads scanner output only as inert artifacts. The scanner container is not privileged, does not receive the Docker socket or host credentials, and runs with dropped capabilities and `no-new-privileges`.

At closure on 2026-09-17, GitHub repository metadata reports `pull_request_creation_policy: collaborators_only` and zero forks. Under GitHub's repository policy, non-collaborators cannot create pull requests, so a literal untrusted-fork execution is not an available contribution path for this repository. The original implementation follow-up had strengthened the acceptance wording to require an actual fork run; that requirement is treated as **not applicable under the current repository policy**, rather than keeping #864 open for an unsupported path.

The security objective is still satisfied structurally: even a collaborator-originated fork PR would execute the same tokenless, read-only `pull_request` workflow without repository secrets. Static workflow/runner contract tests cover these invariants. If `pull_request_creation_policy` is later changed to `all`, a real untrusted-fork run should be recorded before any future attempt to promote Qodana to a required gate.

## Scanner and determinism evidence

Implementation burn-in run [35072109010](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35072109010), artifact [10437360502](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35072109010/artifacts/10437360502), used Community `2026.2.672` / Inspect Code `2026.2.1`, .NET SDK `10.0.301`, runtime `10.0.9`.

| Phase | Exit | Seconds | Findings | Control rule findings |
| --- | ---: | ---: | ---: | ---: |
| Canonical cold | 0 | 370.614 | 12162 | n/a |
| Canonical warm | 0 | 355.057 | 12162 | n/a |
| Positive probe | 0 | 34.528 | 4 | 1 |
| Corrected negative probe | 0 | 33.515 | 3 | 0 |

Cold and warm inventories had the identical fingerprint `dc802b1bbb9cbc823bb90f91cf3cd4ca7667155a369d5bce5257646d4d12abc5`. A separate clean-worktree run on macOS/Rancher Desktop reproduced the same fingerprint and 12162 findings from an independently pulled image, providing cross-machine repeatability evidence.

The runner checkpoints completed phases, treats missing/malformed/failed SARIF as failure rather than zero findings, bounds scanner/probe/container-cleanup time, rejects oversized/symlink report publication, and preserves failure evidence where possible.

## Post-merge reliability and Actions cost evidence

After #906 merged, the committed advisory workflow completed successfully on multiple independent same-repository PR heads:

| Run | Date (UTC) | Result | Whole-run duration | GitHub timing API: billable Ubuntu |
| --- | --- | --- | ---: | ---: |
| [35153454405](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35153454405) | 2026-09-16 | success | 416 s | 0 ms |
| [35181806506](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35181806506) | 2026-09-17 | success | 418 s | 0 ms |
| [35182465418](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35182465418) | 2026-09-17 | success | 426 s | 0 ms |

This establishes a representative normal-PR runtime band of about 6:56–7:06. The zero billable value is the observed GitHub Actions timing response for these public-repository runs; it is not a claim about private-repository pricing. Superseded attempts cancelled by a newer push are not counted as scanner reliability failures.

Run `35182465418` analyzed head `81bf2378c003d6d6f9a9ea13601d7670e603fb65` and published artifact `10480863073` with digest `sha256:1b7be742e7385d29a50620d78bf53aaa63763d515324f9cc5103630fb10386fe`.

## Inventory and signal review

The original canonical scan reported 120 rule IDs: 10132 notes, 2029 warnings and 1 error; 8139 findings were under tests, 3891 under `src`, 116 under benchmarks and 16 under tools. These are scanner diagnostics, not 12162 confirmed defects.

The later normal PR run `35182465418` reported 12164 diagnostics: 10143 notes, 2020 warnings and 1 error; 8147 were under tests, 3885 under `src`, 116 under benchmarks and 16 under tools. The small count drift is explained by a different source revision.

The 832 production-source warnings from that run group as follows:

| Review bucket | Count | Interpretation |
| --- | ---: | --- |
| Style / mechanical | 488 | High-volume cleanup/profile signal; unsuitable as an unreviewed blocking baseline |
| Dead / API-shape | 243 | Potentially useful, but reflection/serialization/public-contract context makes blanket action unsafe |
| Nullable / dataflow | 83 | Independent contract/dataflow signal, often cleanup rather than demonstrated defects |
| Performance | 17 | `PossibleMultipleEnumeration`; targeted review candidates |
| Resource safety | 1 | `UsingStatementResourceInitialization`; targeted review candidate |

Representative evidence includes a resource-initialization warning in `BuildStateRuntimeBuildProcessExecutor.cs`, 17 production `PossibleMultipleEnumeration` findings, and low-value/noise-prone families such as high-volume collection-expression/style suggestions and test-only `AccessToDisposedClosure` observations.

On the exact same head `81bf2378c003d6d6f9a9ea13601d7670e603fb65`, normal CI, CodeQL and Package Validation passed and SonarCloud reported **Quality Gate passed** with **0 new issues**. Qodana therefore contributes genuinely independent analysis, but its current full-repository signal is not equivalent to a reviewed PR regression gate.

## Phase B decision — KEEP ADVISORY

Decision recorded 2026-09-17: **KEEP ADVISORY**.

Reasons:

1. Runtime/reliability is sufficient for continued advisory operation: cold/warm evidence, cross-machine reproduction and multiple post-merge PR runs are stable.
2. Qodana adds real independent signal, including targeted performance, nullable/dataflow and resource-safety observations.
3. The current recommended profile mixes useful findings with substantial historical/style debt and has no reviewed baseline/new-code policy.
4. Promoting the current configuration would create a required scanner-execution check without a trustworthy finding-level regression definition.

No required checks or rulesets are changed. A future dedicated change may define a reviewed differential/baseline policy and reconsider promotion after the Community .NET engine matures.

## Acceptance closure

The implementation, controlled inspection proof, deterministic rerun evidence, representative runtime/reliability evidence, public-repository billing evidence, signal/noise review and explicit Phase B decision are complete.

Fork/untrusted behavior is resolved as policy-N/A for the current repository: non-collaborators cannot create PRs under `pull_request_creation_policy: collaborators_only`, while the committed Qodana workflow itself is tokenless/read-only and references no secrets. This is narrower and more accurate than preserving the post-#906 self-imposed requirement for an actual untrusted fork execution.

The OpenSpec change `add-qodana-community-ci` is archived as `2026-09-17-add-qodana-community-ci`, with the capability synchronized to `openspec/specs/qodana-community-ci/spec.md`. Issue #864 can close after PR validation/merge.

## Upstream references

- [GitHub repository pull-request creation policy](https://docs.github.com/en/rest/repos/repos)
- [JetBrains: .NET engines, Community licensing and configuration](https://www.jetbrains.com/help/qodana/dotnet.html)
- [JetBrains: Docker image options](https://www.jetbrains.com/help/qodana/docker-image-configuration.html)
- [JetBrains: running and configuring Qodana](https://www.jetbrains.com/help/qodana/configure-qodana.html)
- [JetBrains: ConditionIsAlwaysTrueOrFalse inspection](https://www.jetbrains.com/help/inspectopedia/ConditionIsAlwaysTrueOrFalse.html)
