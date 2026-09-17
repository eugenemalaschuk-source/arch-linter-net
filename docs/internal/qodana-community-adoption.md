# Qodana Community .NET adoption

Owner: issue #864. Lifecycle: post-v0.8.0 engineering-health stabilization.

## Current state

Phase A implementation is complete and PR #906 is merged. The Phase B burn-in review now has an
explicit **KEEP ADVISORY** decision: `Qodana Community .NET (advisory)` remains optional and is not
a required merge/release gate. The scanner has demonstrated repeatable execution and genuinely
independent diagnostics, but its current repository-wide output mixes a large historical/style
inventory with a much smaller actionable subset and has no reviewed differential/new-code baseline.
Making the current check required would therefore prove scanner execution, not a reviewed
regression policy.

Issue #864 remains open only because a real fork/untrusted PR execution is still unrecorded and
the active OpenSpec change must not be archived until that final trust-boundary proof is complete.
Do not add Qodana to branch protection or a ruleset as part of #864. A failed optional check stays
visibly failed: `continue-on-error` does not turn an infrastructure failure into success.

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

The recorded implementation burn-in demonstrated the canonical `.slnx`, Release configuration
and .NET 10 with Community `2026.2.672` / Inspect Code `2026.2.1`, SDK `10.0.301`, runtime
`10.0.9`. Later ordinary PR runs continue to exercise the committed workflow. Evidence applies
to the recorded image/checkouts, not every future EAP image.

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

The default-branch workflow exposes a manual `workflow_dispatch` interface with the `burn_in`
boolean. The temporary branch-only push trigger used to obtain implementation evidence was
removed before #906 merged.

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
- `<label>-log/` mirrors Qodana's own internal results log directory (regular files only,
  no symlinks, capped per file and in total), separate from `<label>.log`'s Docker
  stdout/stderr. Each per-phase evidence record (`exit_code`, `seconds`, `status`) is built
  before any of this copying runs, so an unreadable individual SARIF or internal-log file
  never discards an already-finished scan's core result: a copy failure is recorded as
  `sarif_copy_error` or `log_errors` (per-file, non-fatal) instead. `cache_bytes` measurement
  runs last; if it fails, `evidence.json` records `cache_bytes: null` and `cache_error`.

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

`wall_seconds` excludes checkout, test setup, artifact upload and Actions scheduling. GitHub's
Actions timing endpoint is used separately below for whole-run duration and reported billable
usage; do not infer a private-repository price from the public-repository observations.

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
cover this configuration, but they do not substitute for an actual fork run. That real fork
execution is the remaining trust-boundary acceptance blocker for #864.

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

### Representative post-merge PR runs and Actions timing

After #906 merged, the committed advisory workflow continued to complete successfully on
independent PR heads. GitHub's Actions timing endpoint reports the following whole-run values:

| Run | Date (UTC) | Result | Run duration | Reported billable Ubuntu usage |
| --- | --- | --- | ---: | ---: |
| [35153454405](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35153454405) | 2026-09-16 | success | 416 s | 0 ms |
| [35181806506](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35181806506) | 2026-09-17 | success | 418 s | 0 ms |
| [35182465418](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35182465418) | 2026-09-17 | success | 426 s | 0 ms |

These runs establish a representative normal-PR runtime band of about 6:56–7:06 and provide
actual public-repository billing API evidence. The zero billable value is an observed API
result for these public-repository runs, not a claim about pricing for private repositories.
Superseded/cancelled workflow attempts are not reclassified as scanner failures merely because
a newer push cancelled obsolete work.

The latest run above analyzed head `81bf2378c003d6d6f9a9ea13601d7670e603fb65` and published
artifact `10480863073` (`sha256:1b7be742e7385d29a50620d78bf53aaa63763d515324f9cc5103630fb10386fe`).

## Inventory and signal review

The original canonical inventory contained 120 rule IDs: 10132 notes, 2029 warnings and 1 error.
Paths split into 8139 findings in tests, 3891 in production source, 116 in benchmarks and
16 in tools. These are scanner diagnostics, **not 12162 confirmed defects**.

The later normal PR run `35182465418` reported 12164 diagnostics: 10143 notes, 2020 warnings
and 1 error. Its path split was 8147 in tests, 3885 in `src`, 116 in benchmarks and 16 in tools.
The small count drift reflects repository changes between analyzed heads; it is not a stability
failure because the source input was different.

| Sample / family | Observed evidence | Review disposition |
| --- | --- | --- |
| Collection expressions, method bodies, simple-type `var` | Original inventory: 2709 + 2154 + 2017 findings | High-volume style/profile signal; useful selectively, not suitable as an unreviewed blocking baseline |
| `NotAccessedField.Compiler` | Sole error in the original inventory: `ArchitecturePublicApiMemberScannerTests.cs:149`, `InternalField` in the API visibility fixture | Test-fixture usage; not a production critical defect |
| `AccessToDisposedClosure` | Original inventory: all 358 occur in tests; sampled `BadgeCommandHandlerTests.cs:29` captures a using-scoped document in `Assert.Multiple` | Noise-prone family requiring lifetime-aware review; not blanket-actionable |
| `PossibleMultipleEnumeration` | Latest run: 17 findings in `src` | Potential performance/semantic value; review individual input/enumeration lifetimes before filing fixes |
| `UsingStatementResourceInitialization` | Latest run: `BuildStateRuntimeBuildProcessExecutor.cs:136` | Distinct resource-safety signal: object-initializer failure can precede ownership by the `using` local; low-frequency but actionable review candidate |
| `RedundantAssignment` | `SarifEvidenceArtifactReader.cs:99` | Small cleanup candidate; no demonstrated functional failure |

For the latest run, the **832 production-source warnings** can be grouped by inspection intent:

| Review bucket | Count | Interpretation |
| --- | ---: | --- |
| Style / mechanical | 488 | Formatting, naming, redundant syntax, docs and similar cleanup; predominantly debt/noise for gate promotion |
| Dead/API-shape | 243 | Unused/not-accessed members, parameters and collection-shape observations; potentially useful but reflection/serialization/public-contract context can make blanket action unsafe |
| Nullable / dataflow | 83 | Nullable-contract and constant-condition observations; useful independent review signal, but many are contract-tightening/cleanup rather than demonstrated defects |
| Performance | 17 | `PossibleMultipleEnumeration`; targeted review candidates |
| Resource safety | 1 | `UsingStatementResourceInitialization`; targeted review candidate |

### Comparison with existing gates

The exact latest Qodana head `81bf2378c003d6d6f9a9ea13601d7670e603fb65` also completed the normal
CI, CodeQL and Package Validation workflows successfully. SonarCloud's check on that same PR head
reported **Quality Gate passed** and **0 new issues**. Qodana nevertheless produced the repository-wide
inventory above. This is useful evidence that the JetBrains inspection model is not redundant with the
existing new-code gate, but it also exposes the present promotion problem: the Qodana check does not
have a reviewed baseline/differential policy that separates historical inventory from PR regressions.

The controlled positive/negative probe proves that a supported inspection can be detected reliably.
The production inventory proves there are plausible additional review candidates. Neither fact means
that all 12164 diagnostics are defects or that a successful full-repository scan should become a
blocking quality verdict.

### Phase B decision — KEEP ADVISORY

Decision recorded 2026-09-17: **KEEP ADVISORY**.

Reasons:

1. Scanner/runtime reliability is acceptable for continued advisory use: cold/warm evidence,
   cross-machine reproduction and multiple independent post-merge PR runs are stable enough to keep
   collecting signal.
2. The engine adds real coverage: targeted performance, nullable/dataflow and resource-safety
   diagnostics exist beyond the current Sonar new-code result.
3. Signal quality is not suitable for promotion as configured: the recommended profile emits a very
   large repository-wide historical/style inventory, while the current workflow has no reviewed
   baseline/new-code differential defining which findings constitute a regression.
4. Making the check required now would primarily require the scanner to execute successfully; it
   would not create a trustworthy finding-level merge policy. That would be a fake blocking-gate
   claim and is explicitly avoided.

Therefore #864 does **not** modify branch protection/rulesets. Qodana remains a visibly separate,
non-blocking analyzer. A future dedicated change may define a reviewed baseline/new-code policy and
reconsider promotion after the Community .NET engine matures; that work is outside the remaining
acceptance of #864.

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

The representative rerun/runtime/billing evidence and Phase B signal review are now complete.
**Still required for issue completion:** execute one real fork/untrusted PR through the committed
workflow and record the result without exposing secrets. Static contract tests and same-repository
PRs do not substitute for that proof.

After that fork execution succeeds, revalidate the final documentation/specifications, complete task
2.5, archive the active OpenSpec change, complete task 2.7 and close #864. Until then the active
OpenSpec change remains deliberately unarchived.

## Upstream references

- [JetBrains: .NET engines, Community licensing and configuration](https://www.jetbrains.com/help/qodana/dotnet.html)
- [JetBrains: Docker image options](https://www.jetbrains.com/help/qodana/docker-image-configuration.html)
- [JetBrains: running and configuring Qodana](https://www.jetbrains.com/help/qodana/configure-qodana.html)
- [JetBrains: ConditionIsAlwaysTrueOrFalse inspection](https://www.jetbrains.com/help/inspectopedia/ConditionIsAlwaysTrueOrFalse.html)
