# CI Integration

Start with a required pull-request check. Add reviewer reports next, and public
badge publication only when you need it. Neither a hosting account nor Relay,
SonarCloud, Codecov, or GitHub Pages is required to use ArchLinterNet in CI.

This guide uses GitHub Actions. The CLI commands and exit codes also apply to
other providers; see [reference entrypoints](reference-entrypoints.md) for shell,
PowerShell, Make, Task, and Tilt examples.

## Recommended pull-request workflow

First complete [installation](../installation/index.md) and the
[first policy](../getting-started/first-policy.md). Commit the local tool
manifest with the exact CLI version you tested, your policy, and any reviewed
baseline/API snapshots. The example uses `architecture/arch.yml`; change that
path to your policy. Restore the SDKs and dependencies your solution requires.

Save this as `.github/workflows/architecture.yml`:

```yaml
name: Architecture validation

on:
  pull_request:

permissions:
  contents: read

jobs:
  architecture:
    name: Architecture validation
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          fetch-depth: 0
          persist-credentials: false

      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          dotnet-version: 10.0.x

      - name: Restore tools and dependencies
        run: |
          dotnet tool restore
          dotnet restore

      - name: Validate architecture
        run: |
          dotnet arch-linter-net --policy architecture/arch.yml \
            --mode strict --ensure-built --no-restore \
            --report json=architecture-strict.json \
            --report sarif=architecture-strict.sarif

      - name: Keep diagnostics even when validation fails
        if: always()
        uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1
        with:
          name: architecture-results-${{ github.run_id }}-${{ github.run_attempt }}
          path: |
            architecture-strict.json
            architecture-strict.sarif
          if-no-files-found: warn
```

After its first run, require the displayed `Architecture validation` check in
the target branch's protection/ruleset. Test with a deliberate policy violation:
the check must fail and prevent merge. A diagnostic upload cannot turn that
failure into success. Missing diagnostic files may be expected after a build
failure; the validation step still fails the job.

Do not add the same full analysis on `push: main` solely to update a badge.
A separate post-merge publisher can promote the accepted PR result after
verifying its provenance and tree. An independently chosen nightly analysis is
also possible; it must identify its own analyzed revision rather than claim to
be the latest PR result. See [badge adoption](badge-adoption.md).

## Strict vs audit jobs

The starter workflow makes **strict** blocking. Audit is an explicit choice,
not an extra required check to enable by copying a larger example.

For advisory audit, add this step and include its output in the artifact:

```yaml
- name: Advisory architecture audit
  if: always()
  continue-on-error: true
  run: |
    dotnet arch-linter-net --policy architecture/arch.yml \
      --mode audit --ensure-built --no-restore \
      --report json=architecture-audit.json
```

When both modes are deliberately required, the combined
`--mode strict,audit --ensure-built` invocation evaluates them from one
immutable analysis snapshot and fails if either mode fails. Use a pinned CLI
that supports that combined option. Separate invocations do not share
in-memory preparation. Report sinks render completed outcomes; they do not run
another analysis.

## Build once where the workflow permits it

`--ensure-built` gives the CLI responsibility for build preparation. Without
it, the required outputs must already exist and pass preflight. Do not put an
unnecessary `dotnet build` immediately before a CLI-owned build, or assume a
second process reuses the first process's analysis snapshot.

When your product job already builds the governed solution, run against those
outputs with the matching configuration, framework, runtime, and policy inputs.
For compiled-evidence/staging workflows, use the package's documented evidence
contract; copying arbitrary `bin/` directories is not proof of compatibility.
See [CLI build options](../cli/index.md) and
[Unity boundaries](unity-boundaries.md).

## Exit code behavior

| Code | Meaning | Required CI action |
| --- | --- | --- |
| `0` | The requested gate passed. | Pass. |
| `1` | Analysis/comparison completed and the requested gate failed. | Block merge. |
| `2` | The command could not complete, or Health is unassessable. | Block merge; inspect diagnostics. |

A `health` command can write a valid `architecture-health/v1` document while
exiting `1` or `2`. Preserve it for reviewers, but preserve the failing gate as
well. Do not apply `continue-on-error` to the entire required job or use
`|| true` to make publication possible. See [exit codes](../usage/exit-codes.md).

## Baseline in CI

A reviewed baseline belongs to the repository, not to an automatically
regenerated CI artifact. Pass it explicitly when required:

```bash
dotnet arch-linter-net --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml --mode strict --ensure-built
```

### New-debt gate with policy-weakening guardrails

For no-new-debt and policy-weakening decisions, use the
[complete governance workflow](single-tool-workflow.md). It shows the explicit
baseline, actual base/current policy contexts, compatible change snapshots,
Health, and PR report commands. Both `gate` and `health` require an explicit
baseline; the guide includes an explicit workflow-local empty baseline for a
repository with no accepted debt.

Bind the base to the event's exact base commit, not whichever `origin/main`
happens to point to later. With the full-history checkout above, a preparation
step can create the base worktree without persisting checkout credentials:

```yaml
- name: Prepare the exact review base
  env:
    BASE_SHA: ${{ github.event.pull_request.base.sha }}
  run: |
    set -euo pipefail
    git cat-file -e "${BASE_SHA}^{commit}"
    git worktree add --detach "$RUNNER_TEMP/architecture-base" "$BASE_SHA"
```

Use that path as `BASE_WORKTREE` in the complete workflow. If the exact object
is absent, fetch it through an authenticated read-only checkout or fail; do not
substitute another revision. Use the same pinned CLI for base and candidate,
including when the base has an older tool manifest.

Policy-context export is not a compiled base snapshot. Change reporting needs
real compatible base build evidence. A trusted exact-base evidence producer
can avoid rebuilding it for every PR, but reuse must verify the revision,
CLI/schema, policy/build selectors, provenance, and digests. On a cache miss,
prepare that exact base or report unavailable evidence, never a made-up empty
comparison.

### CI reads baselines; it never writes them

`baseline verify` checks drift; `baseline diff` provides a read-only comparison.
Run `generate`, `update`, `prune`, or `migrate` as a separate reviewed maintenance
change, not as an automatic way to pass a failing check. Do not duplicate a
baseline/weakening analysis already covered by your selected gate.

### Baseline debt semantics in the coverage gate

Accepted debt remains visible; it is not a new regression. New uncovered debt
must not be silently accepted, resolved entries can become stale, and deliberate
exclusions need reviewed reasons. No coverage contracts and zero findings from
real coverage contracts are different states. See
[migration baselines](migration-baselines.md) and
[coverage contracts](../contracts/coverage.md).

## Secure unified Architecture PR report publication

Use the complete workflow to produce canonical Health and a compatible change
report with the same execution context and mode. Then `report pr` renders
Markdown from those files. **`health` performs analysis; `report pr` and badge
projection do not.** Do not append every analytical command to an existing gate
merely to obtain another presentation of the same result.

Keep the PR producer read-only. A separate trusted comment publisher needs only
the permissions necessary to read its evidence and update the PR comment. It
must verify the current PR head, exact producer workflow/job, run and attempt,
artifact shape, size bounds, and digest before writing the exact Markdown.
Treat downloaded files as data; never execute their contents or check out PR
code in the privileged publisher. Fork and Dependabot evidence needs the same
checks. Missing or stale evidence must not leave an older green report presented
as the current head's result.

The [CLI reference](../cli/index.md) documents `report pr` transport context and
bundle navigation. Private reports stay private; publishing a small public
badge does not authorize publishing the report bundle.

## Architecture Health badge payload

Once your selected analysis has produced canonical Health:

```bash
dotnet arch-linter-net badge architecture-health \
  --input artifacts/architecture-health.json \
  --output artifacts/architecture-health-badge.json
```

This command produces the badge payload, not its hosting. Health, Gate, counts,
and colors belong to the CLI. Choose [a publication path](badge-adoption.md)
without adding another evaluator to your CI.

### Reusable trusted promotion

The shipped reusable workflow's adapters are `github-raw`, `relay`, and `none`.
**Within that workflow**, private repositories cannot use `github-raw`.
They can use experimental Relay or keep publication disabled. This is not a
restriction on a separately verified [consumer-owned publisher](badge-direct-hosting.md).
Do not copy the upstream registry ID into another repository: it identifies a
reviewed configuration, not a generic template. Relay users must verify the
[matching distribution](../reference/badge-distribution.md) and follow
[experimental setup](badge-setup.md).

### Verify Architecture Health badge freshness

Check the origin bytes and their publication evidence before inspecting a
cached README image. The [adoption guide](badge-adoption.md#verify-the-result)
explains that sequence. For **ArchLinterNet's own public raw badge**, see the
[repository publication receipt](../reference/repository-ci.md#architecture-health-publication).
A static raw snapshot does not acquire Relay's read-time expiry guarantees.

## Legacy architecture-policy badge payload

`badge architecture-policy --input architecture-strict.json` remains the
narrower strict-validation projection. Its exits are `0` for passing, `1` for
failing, and `2` for unavailable. `coverage report` remains a separate projection
of coverage evidence, not the unified PR comment. Neither is a replacement for
canonical Architecture Health.

## Repository badge policy

The following subjects describe **ArchLinterNet's own CI**, not prerequisites
for consumers. They are now covered by the
[repository CI reference](../reference/repository-ci.md): PR authority,
post-merge SonarCloud/Codecov telemetry, README signals, and release-only Pages
publication.

<a id="test-coverage-with-codecov-and-sonarcloud"></a>
<a id="codecov-auth-and-fork-behavior"></a>
<a id="failure-mode-expectations"></a>
<a id="sonarcloud-analysis"></a>
<a id="pull-requests"></a>
<a id="merged-main"></a>
<a id="required-github-configuration"></a>
<a id="fork-pull-requests"></a>
<a id="automatic-analysis-caveat"></a>
<a id="recommended-required-check"></a>
<a id="post-merge-verification"></a>

These legacy section links are retained for existing README and guide links.
See [quality telemetry and configuration](../reference/repository-ci.md#quality-telemetry).

## Azure Pipelines example

After installing the required .NET SDK and committing a local tool manifest:

```yaml
steps:
  - script: dotnet tool restore
    displayName: Restore architecture tool
  - script: dotnet restore
    displayName: Restore dependencies
  - script: >-
      dotnet arch-linter-net --policy architecture/arch.yml
      --mode strict --ensure-built --no-restore
    displayName: Validate architecture
```
