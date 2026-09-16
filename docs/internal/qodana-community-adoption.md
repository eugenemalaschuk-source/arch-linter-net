# Qodana Community .NET adoption

Owner: issue #864. Lifecycle: post-v0.8.0 engineering-health stabilization.

## Current state

Phase A implementation is prepared; scanner adoption is **not yet validated**. The check
`Qodana Community .NET (advisory)` is optional, not a required merge/release gate. Do not
add it to branch protection or a ruleset before the reviewed Phase B decision. A failed
optional check stays visibly failed: `continue-on-error` does not turn an infrastructure
failure into success.

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
separate runs only when their image IDs and analyzed commits match. Review and pin a
registry manifest digest after a real pull, rather than inventing one. The runner accepts
`jetbrains/qodana-cdnet:2026.2@sha256:<reviewed-manifest-digest>` in the same declaration.
Do not substitute Docker's local image ID for a registry manifest digest.

The exact selected image must still prove .NET 10, `.slnx`, configuration and inspection
compatibility in a real run. Documentation research is not execution evidence.

## Running and reviewing

Normal PRs perform one cold scan. Fork PRs follow the same tokenless execution path.
A local invocation requires Linux with a working Docker daemon and registry/NuGet access:

```bash
export QODANA_ANALYZED_SHA="$(git rev-parse HEAD)"
python3 tools/scripts/qodana_ci.py --output "$(mktemp -d)"
```

For controlled validation, add `--burn-in`. This performs a cold scan, a warm scan using
the same image/checkout/cache, and two standalone generated projects outside the canonical
solution. The positive project enables `ConditionIsAlwaysTrueOrFalse` and contains a
redundant null condition. The corrected negative project must no longer report that rule.
A missing positive diagnostic or a remaining negative diagnostic fails validation.
Mocked runner tests do not prove that the real engine detects this inspection.

Once the workflow exists on the default branch, its manual `workflow_dispatch` interface
also offers the `burn_in` boolean. Before that, use the local command; do not assume the
Actions manual-run button is available for a workflow present only on a feature branch.

`--output` must be new or empty and outside the repository. Evidence lives under its
`artifacts` child, uploaded as `qodana-evidence-<run-id>-<attempt>` with 14-day retention:

- `summary.md` and the Actions job summary show collection status and measured phases.
- `evidence.json` records the commit, image ID, timings, exit codes, findings by rule,
  cache size and an order-independent fingerprint of rule/message/location inventories.
- `cold.sarif.json`, optional warm/probe SARIF files and scanner logs provide diagnostics.
  A nonzero scanner exit with otherwise usable SARIF remains a failed, partial analysis.

Missing/malformed SARIF or an unsuccessful invocation is never interpreted as zero findings.
Raw diagnostics remain in artifacts, not executable workflow commands or PR comments.
Correctness/security findings need focused owning issues; duplicate and low-value findings
need rule-specific triage. No baseline update or suppression is automatic.

## Limits and cost evidence

The job timeout is 55 minutes. Image pulling is bounded to 300 seconds, each repository scan
to 1,200 seconds, each probe to 180 seconds, and each container cleanup to 30 seconds.
A timed-out Docker client is followed by explicit container removal. Cancellation of the
whole Actions job may prevent final evidence publication; record cancelled/missing artifacts
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
cover this configuration, but a real fork run remains a required adoption check.

## Acceptance evidence and Phase B decision

Initial inventory: **not collected**. Reviewed baseline: **not established**. No configured
baseline is not a claim that the repository has zero findings.

Record each actual run in the issue with this evidence:

| Evidence | State |
| --- | --- |
| Canonical solution loads and produces understandable SARIF | Pending real scanner run |
| Positive inspection detected and corrected negative control clean | Pending real probe run |
| Cold/warm fingerprint agreement for identical image and commit | Pending real burn-in run |
| Independent rerun stability, crashes and timeouts | Pending representative runs |
| Fork execution with no secrets or unnecessary permissions | Configuration tested; execution pending |
| Cold/warm runtime, job duration, actual billable minutes, cache size | Pending measured runs |
| Unique actionable findings versus Sonar/compiler/other gates | Pending initial inventory review |
| Duplicate/noisy findings, remediation burden and justified baseline | Pending initial inventory review |
| Reviewed PROMOTE versus KEEP ADVISORY decision | Pending burn-in and maintainer review |

This implementation does **not** record a premature Phase B decision. It stays advisory
because EAP reliability, compatibility and signal quality have not yet been demonstrated.
After representative evidence is reviewed, record either PROMOTE with exact unchanged
existing required contexts plus the new context, or KEEP ADVISORY with concrete reliability,
noise or cost reasons. A successful command invocation alone is insufficient.

## Implementation validation boundary

The offline Python runner and workflow contract suite is available as:

```bash
python3 -m unittest discover -s tests/qodana -v
```

During implementation, those tests passed, including negative cases for missing/malformed
reports, scanner failures, timeout cleanup, image selection and positive/negative proof
requirements. They use mocks and are not scanner-runtime evidence.

The implementation environment lacks Docker, .NET, actionlint, zizmor, Prettier, mdformat
and OpenSpec. Dependency downloads are unavailable there. Actual scanner validation,
workflow lint/formatting and OpenSpec CLI validation/archive remain required before opening
the feature PR under `docs/ai/feature-implementation-workflow.md`. The active OpenSpec change
is intentionally not marked archived. Issue #864 remains open until its evidence and review
criteria are met.

## Upstream references

- [JetBrains: .NET engines, Community licensing and configuration](https://www.jetbrains.com/help/qodana/dotnet.html)
- [JetBrains: Docker image options](https://www.jetbrains.com/help/qodana/docker-image-configuration.html)
- [JetBrains: running and configuring Qodana](https://www.jetbrains.com/help/qodana/configure-qodana.html)
- [JetBrains: ConditionIsAlwaysTrueOrFalse inspection](https://www.jetbrains.com/help/inspectopedia/ConditionIsAlwaysTrueOrFalse.html)
