# Qodana Community .NET adoption

Owner: issue #864. Lifecycle: post-v0.8.0 engineering-health stabilization.

## Current state

Phase A has real scanner and tooling evidence; Phase B adoption review remains incomplete.
The check `Qodana Community .NET (advisory)` is optional, not a required merge/release gate.
Do not add it to branch protection or a ruleset before the reviewed Phase B decision.
A failed optional check stays visibly failed: `continue-on-error` does not turn an
infrastructure failure into success. Draft PR #906 and issue #864 remain open.

The workflow is independent of Coverage + Sonar, CodeQL, ArchLinterNet self-governance,
package validation and all existing required checks. None of their definitions or
baseline semantics changes. This task does not retroactively authorize v0.8.0 publication.

## Configuration and scope

`qodana.yaml` selects the free `jetbrains/qodana-cdnet:2026.2` Community engine, the
recommended inspection profile, and `ArchLinterNet.slnx` in Release configuration.
There is no Qodana Cloud token, subscription, baseline, exclusion or suppressed rule.
Community performs its own build; this workflow does not separately run acceptance,
coverage, a release matrix or another solution restore before analysis.

The image tag is a **version pin, not a manifest digest lock**. Each invocation records
Docker's immutable image ID and uses that ID for every scan in that invocation. Compare
separate runs only when their image IDs and analyzed commits match. A registry manifest
digest was captured in the burn-in image log below; adopting that lock still requires a
configuration change and another real run. The runner accepts a version plus digest, but
that spelling has not been exercised end-to-end. Do not substitute Docker's local image
ID for a registry manifest digest.

The recorded run demonstrated the canonical `.slnx`, Release configuration and .NET 10
with Community `2026.2.672` / Inspect Code `2026.2.1`, SDK `10.0.301`, runtime `10.0.9`.
This evidence applies to that image and checkout, not every future EAP image.

## Running and reviewing

Normal PRs perform one cold scan. Fork PRs use the same tokenless execution path; a real
fork run remains to be recorded. A local invocation requires Linux, a working Docker
daemon and registry/NuGet access:

```bash
export QODANA_ANALYZED_SHA="$(git rev-parse HEAD)"
python3 tools/scripts/qodana_ci.py --output "$(mktemp -d)"
```

Run this from a clean checkout (for example `git worktree add <path> HEAD`), not a working
tree with local `bin/`/`obj/` build output. The runner mounts `--project` as-is; leftover
build artifacts are analyzed too and can inflate the report well past a normal run. A local
run against this repository's own dirty working tree (post-`dotnet build`) produced a 103 MB
`qodana.sarif.json` that exceeded `MAX_SARIF_BYTES` (64 MB) and was reported as a failed,
oversized report, even though the container's own exit code was 0. The same commit scanned
from a clean `git worktree` produced the real 23.5 MB report below instead.

For controlled validation, add `--burn-in`. This performs a cold scan, a warm scan using
the same image/checkout/cache, and two standalone generated projects outside the canonical
solution. The positive project enables `ConditionIsAlwaysTrueOrFalse` and contains a
redundant null condition. The corrected negative project must no longer report that rule.
A missing positive diagnostic or a remaining negative diagnostic fails validation.
The negative control need not have zero unrelated recommended-profile diagnostics.

Once the workflow exists on the default branch, its manual `workflow_dispatch` interface
also offers the `burn_in` boolean. Before that, use the local command; do not assume the
Actions manual-run button is available for a workflow present only on a feature branch.
The temporary branch-only push trigger used to obtain implementation evidence is removed.

`--output` must be new or empty and outside the repository. Evidence lives under its
`artifacts` child, uploaded as `qodana-evidence-<run-id>-<attempt>` with 14-day retention:

- `summary.md` and the Actions job summary show collection status and measured phases.
- `evidence.json` records the commit, image ID, timings, exit codes, findings by rule,
  cache size and an order-independent fingerprint of rule/message/location inventories.
- `cold.sarif.json`, optional warm/probe SARIF files and scanner logs provide diagnostics.
  A nonzero scanner exit with otherwise usable SARIF remains a failed, partial analysis.
  A bounded, non-symlink report is copied to artifacts before validation, so an unsuccessful
  invocation, a `toolExecutionNotifications`/`toolConfigurationNotifications` infrastructure
  error, or a scanner nonzero exit still leaves the raw SARIF available for diagnosis; only
  the structured findings/fingerprint fields are withheld when the report is deemed unusable.

Completed phases are checkpointed before the next scan; an interrupted later probe does
not erase completed cold/warm evidence. Missing/malformed SARIF or an unsuccessful invocation
is never interpreted as zero findings. Raw diagnostics remain in artifacts, not executable
workflow commands or PR comments. No baseline update or suppression is automatic.

## Limits and cost evidence

The job timeout is 55 minutes. Image pulling is bounded to 300 seconds, each repository scan
to 1,200 seconds, each probe to 180 seconds, and each container cleanup to 30 seconds.
A timed-out Docker client is followed by explicit container removal. Cancellation of the
whole Actions job may prevent artifact publication; record cancelled/missing artifacts
as incomplete evidence, not success.

There is deliberately no cross-run/shared Actions cache. A normal run begins with an empty
scanner cache; the optional warm scan reuses only its own job's cache. Cache measurements are
regular-file logical bytes, not compressed Actions-cache charges. Image pull duration is
separate from scanner durations. Community builds/restores inside the scanner container.

`wall_seconds` excludes checkout, test setup, artifact upload and Actions scheduling. It is
**not GitHub-billed minutes or a monetary charge**. Record job duration from Actions and
actual billable usage from repository/account billing separately. Do not infer free/private
repository billing or assign a cost without that evidence.

## Trust boundaries

Execution uses an ephemeral GitHub-hosted Ubuntu runner, `contents: read`, checkout with
`persist-credentials: false`, and the ordinary `pull_request` event, not `pull_request_target`.
No repository/Qodana secrets or host environment variables are forwarded to the scanner.
The container receives only the source, its results and its cache volumes; no Docker socket,
privileged mode or host credentials. It runs as the host UID/GID with capabilities dropped
and `no-new-privileges`. Network access is necessary for dependency restore.

PR build logic and scanner reports remain untrusted. Valid SARIF is copied as a bounded
regular file; scanner-created symlinks are not published. No privileged downstream consumer,
security-event writer or PR-comment writer consumes these artifacts. Static workflow tests
cover this configuration, but they do not substitute for an actual fork run.

## Recorded burn-in evidence

[Run 35072109010](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35072109010)
on 2026-09-16 completed successfully using commit
`caf29214dca69efc9d2893dc2270b242d14e83fc`. The temporary push-triggered run used the same
scanner/configuration as the production workflow, before the evidence-checkpoint fix.

[Artifact 10437360502](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35072109010/artifacts/10437360502)
contains raw SARIF, logs and `evidence.json`. ZIP SHA-256:
`4f9b674344b4eb562a54c5ef975dd7eab6609def32f4d0dec3da8acde4542e05`.
Image identities are intentionally distinguished:

- Registry manifest digest from `image.log`:
  `sha256:9229bd0ffce00faad4cc45971a8985114ad629a6d82318eab6c25b19749fb752`.
- Local immutable image ID used for all four scans:
  `sha256:5a02c743e704853b9f4cc2f408f6f014a422048e55167389175ac667784b354e`.

| Phase | Exit | Seconds | Findings | Control rule findings |
| --- | --- | --- | --- | --- |
| Canonical cold | 0 | 370.614 | 12162 | Not a probe |
| Canonical warm | 0 | 355.057 | 12162 | Not a probe |
| Positive probe | 0 | 34.528 | 4 | 1 |
| Corrected negative probe | 0 | 33.515 | 3 | 0 |

Cold and warm inventories have identical fingerprint
`dc802b1bbb9cbc823bb90f91cf3cd4ca7667155a369d5bce5257646d4d12abc5`.
Each reported 958158509 logical cache bytes. Image pull took 29.894 seconds;
collector wall time was 824.547 seconds. This single comparison is not a general cache
performance claim or billing measurement. Standalone probes log unavailable Git metadata;
analysis still completes and emits valid SARIF. Cleanup can report an already-removed
container because normal runs also use Docker `--rm`.

### Independent cross-machine repeatability

A separate local cold scan of commit `f8d6b4ebf04b5cf4b66239e3e4f6a176f9e47ef8` (a clean
`git worktree`, not the GitHub-hosted runner) reproduced the exact same fingerprint,
`dc802b1bbb9cbc823bb90f91cf3cd4ca7667155a369d5bce5257646d4d12abc5`, and the same 12162
findings, from a separately pulled copy of the image (macOS host, Rancher Desktop Docker,
registry digest `sha256:9229bd0ffce00faad4cc45971a8985114ad629a6d82318eab6c25b19749fb752`).
The commit only changed CI/runner files, so identical C# input is expected; the value here
is that two different machines, OS/Docker runtimes and independently pulled image copies
converged on byte-identical inventory, not just repeated runs of one cached container.

## Initial inventory and triage boundary

The canonical inventory contains 120 rule IDs: 10132 notes, 2029 warnings and 1 error.
Paths split into 8139 findings in tests, 3891 in production source, 116 in benchmarks and
16 in tools. These are scanner diagnostics, **not 12162 confirmed defects**.

| Sample / family | Observed evidence | Initial disposition |
| --- | --- | --- |
| Collection expressions, method bodies, simple-type `var` | 2709 + 2154 + 2017 findings | Style/profile-review candidates, not automatic correctness failures |
| `NotAccessedField.Compiler` | Sole error: `ArchitecturePublicApiMemberScannerTests.cs:149`, `InternalField` in the API visibility fixture | Test-fixture usage; do not delete the fixture or classify this as a production critical bug |
| `AccessToDisposedClosure` | All 358 occur in tests; sampled `BadgeCommandHandlerTests.cs:29` captures a using-scoped document in `Assert.Multiple` | Review assertion lifetime semantics; sample is noise-prone, not proof that every finding is false |
| `PossibleMultipleEnumeration` | 22 total, including 17 in `src` | Review production enumeration paths and input types before filing defects |
| `RedundantAssignment` | `SarifEvidenceArtifactReader.cs:99` | Small cleanup candidate; no demonstrated functional failure |

This is a complete count inventory and a **sample triage**, not a finding-by-finding review.
Unique value versus Sonar/compiler/other gates is not established. Keep original findings
visible; do not introduce a giant baseline, blanket exclusions or unrelated C# changes.
Confirmed correctness/security issues need focused owning tasks; debt/noise needs justified
rule-specific review. No reviewed baseline or promotion decision has been established.

## Implementation validation and remaining acceptance

[Run 35073258396](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35073258396)
validated `f8d6b4ebf04b5cf4b66239e3e4f6a176f9e47ef8` on GitHub-hosted Ubuntu:

| Check | Result |
| --- | --- |
| `python3 -m unittest discover -s tests/qodana -v` | 36 passed |
| `make test-tooling-coverage` | 546 passed; Qodana runner line coverage 98.4% |
| actionlint 1.7.12 | Passed, 13 workflows; existing configuration |
| zizmor 1.30.1 | Passed with existing repository ignore/suppression configuration unchanged |
| Prettier 3.9.5 / locked mdformat | Passed for workflow/configuration and the runbook at that SHA |
| OpenSpec 1.13.0 `validate --all` | 168 passed, 0 failed; existing unrelated warnings remain |

The tooling artifact includes `validated-sha.txt`, source files and `coverage-python.xml`.
Offline tests validate orchestration and negative paths, not scanner execution. The added
lifecycle regression first failed on the original runner because warm evidence was not
checkpointed before a probe; the fix saves every completed phase. Production C# is unchanged.

Still required for issue completion: actual fork execution, representative independent
reruns/reliability, job/billing evidence, deeper unique-signal/noise/debt triage and a maintainer
reviewed PROMOTE or KEEP ADVISORY decision. Neither successful scans nor this sample review
close Phase B. Existing required checks and rulesets remain untouched. Full repository/PR
acceptance is separate from the tooling run above.

The active OpenSpec change remains unarchived while these requirements are incomplete.
Draft PR #906 is the explicitly requested handoff exception to the ordinary
validation/archive-before-PR lifecycle; it is not permission to mark missing evidence done.

## Upstream references

- [JetBrains: .NET engines, Community licensing and configuration](https://www.jetbrains.com/help/qodana/dotnet.html)
- [JetBrains: Docker image options](https://www.jetbrains.com/help/qodana/docker-image-configuration.html)
- [JetBrains: running and configuring Qodana](https://www.jetbrains.com/help/qodana/configure-qodana.html)
- [JetBrains: ConditionIsAlwaysTrueOrFalse inspection](https://www.jetbrains.com/help/inspectopedia/ConditionIsAlwaysTrueOrFalse.html)
