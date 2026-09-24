# CLI Reference

Use `arch-linter-net --help` or `arch-linter-net <command> --help` for the
installed version. This reference describes current commands; a local tool
manifest pins the package used by your repository. Package and persisted schema
versions are separate identities.

Start with [CI integration](../guides/ci-integration.md). For Health and a PR
report, use the [base/current workflow](../guides/single-tool-workflow.md), not a
sequence of every analytical command in this catalog.

## Command map

<!-- cli-command: validate -->
<!-- cli-command: badge -->
<!-- cli-command: badge architecture-policy -->
<!-- cli-command: badge architecture-health -->
<!-- cli-command: badge repository-metrics -->
<!-- cli-command: badge setup -->
<!-- cli-command: badge doctor -->
<!-- cli-command: badge apply-handoff -->
<!-- cli-command: badge lifecycle -->
<!-- cli-command: baseline -->
<!-- cli-command: baseline generate -->
<!-- cli-command: baseline migrate -->
<!-- cli-command: baseline update -->
<!-- cli-command: baseline prune -->
<!-- cli-command: baseline diff -->
<!-- cli-command: baseline verify -->
<!-- cli-command: cache -->
<!-- cli-command: cache inspect -->
<!-- cli-command: cache clear -->
<!-- cli-command: change -->
<!-- cli-command: change snapshot -->
<!-- cli-command: change report -->
<!-- cli-command: coverage -->
<!-- cli-command: coverage report -->
<!-- cli-command: coverage extract -->
<!-- cli-command: explain -->
<!-- cli-command: gate -->
<!-- cli-command: health -->
<!-- cli-command: health revalidate-publication -->
<!-- cli-command: graph -->
<!-- cli-command: measure -->
<!-- cli-command: history -->
<!-- cli-command: history analyze -->
<!-- cli-command: policy -->
<!-- cli-command: policy check -->
<!-- cli-command: policy context -->
<!-- cli-command: policy weakening -->
<!-- cli-command: public-api -->
<!-- cli-command: public-api capture -->
<!-- cli-command: public-api diff -->
<!-- cli-command: public-api migrate -->
<!-- cli-command: public-api update -->
<!-- cli-command: report -->
<!-- cli-command: report pr -->
<!-- cli-command: topology -->
<!-- cli-command: topology capture -->
<!-- cli-command: topology diff -->
<!-- cli-command: topology verify -->
<!-- cli-command: scaffold -->
<!-- cli-command: scaffold cli-command -->
<!-- cli-command: schema -->
<!-- cli-command: schema list -->
<!-- cli-command: schema print -->

| Command | Purpose |
| --- | --- |
| `arch-linter-net [options]` | Validate architecture; strict is the default mode. |
| `arch-linter-net badge` | Render badge payloads and manage optional publication integrations. |
| `arch-linter-net badge architecture-policy --input <strict.json>` | Render the legacy strict-validation badge. |
| `arch-linter-net badge architecture-health --input <health.json>` | Render the canonical Health badge. |
| `arch-linter-net badge repository-metrics --input <validation.json>` | Render Source lines and optional grouped Repository/Structure badges. |
| `arch-linter-net badge architecture-health setup` | Preview/generate built-in setup; Relay remains experimental. |
| `arch-linter-net badge architecture-health doctor` | Diagnose built-in publication configuration. |
| `arch-linter-net badge architecture-health apply-handoff` | Verify and apply a private setup handoff on its exact base. |
| `arch-linter-net badge architecture-health lifecycle` | Manage the experimental Relay lifecycle. |
| `arch-linter-net baseline` | Manage reviewed finding debt and metric baselines. |
| `arch-linter-net baseline generate` | Capture current findings and selected scalar metric baselines. |
| `arch-linter-net baseline migrate` | Migrate supported baseline identity formats. |
| `arch-linter-net baseline update` | Propose adding current finding debt; requires review. |
| `arch-linter-net baseline prune` | Propose removing obsolete finding entries. |
| `arch-linter-net baseline diff` | Compare without writing or gating. |
| `arch-linter-net baseline verify` | Check baseline integrity and applicability without writing. |
| `arch-linter-net cache` | Operate on the opt-in exact-request cache. |
| `arch-linter-net cache inspect --cache <auto\|path>` | Inspect cache state. |
| `arch-linter-net cache clear --cache <auto\|path>` | Clear the selected cache with containment checks. |
| `arch-linter-net change` | Create/compare complete architecture snapshots. |
| `arch-linter-net change snapshot --policy <path> --output <path>` | Analyze one revision and write its snapshot. |
| `arch-linter-net change report --base <path> --current <path> --execution-context <id>` | Compare existing snapshots without analyzing again. |
| `arch-linter-net coverage` | Post-process validation artifacts. |
| `arch-linter-net coverage report --input <validation.json>` | Render coverage Markdown. |
| `arch-linter-net coverage extract --input <combined.json> --mode <mode> --output <path>` | Extract one mode from combined validation JSON. |
| `arch-linter-net explain --source <id> --target <id>` | Explain a namespace/type dependency path. |
| `arch-linter-net gate` | Analyze reviewed debt and policy weakening for a gate decision. |
| `arch-linter-net health` | Analyze and summarize current governance as Gate and Health. |
| `arch-linter-net health revalidate-publication` | Refresh temporal publication evidence from serialized Health without a new analysis. |
| `arch-linter-net graph` | Export JSON, DOT or Mermaid dependency graphs. |
| `arch-linter-net measure` | Measure declared architecture metrics without budget violations. |
| `arch-linter-net history` | Investigate architecture history. |
| `arch-linter-net history analyze --from <ref> --to <ref>` | Analyze an explicit Git range; select JSON or Markdown, repeat `--report` for one-analysis multi-output, and use `--timings` for phase evidence. |
| `arch-linter-net policy` | Inspect/review policy. |
| `arch-linter-net policy check --policy <path>` | Validate static configuration, not architecture compliance. |
| `arch-linter-net policy context --policy <path> --format <json\|markdown>` | Export effective policy facts. |
| `arch-linter-net policy weakening --base-context <path> --current-context <path>` | Compare policy contexts for relaxation. |
| `arch-linter-net public-api` | Manage reviewed API signatures. |
| `arch-linter-net public-api capture` | Capture selected current API for review. |
| `arch-linter-net public-api diff` | Compare selected API with its reviewed snapshot. |
| `arch-linter-net public-api migrate` | Migrate supported snapshot grammar/identity. |
| `arch-linter-net public-api update` | Explicitly update a reviewed API snapshot. |
| `arch-linter-net report` | Render existing canonical artifacts. |
| `arch-linter-net report pr --health <health.json> --change <change.json>` | Render local PR Markdown; does not call GitHub. |
| `arch-linter-net topology` | Observe and review component maps. |
| `arch-linter-net topology capture --subject-kind <kind>` | Capture observations; does not write a policy. |
| `arch-linter-net topology diff` | Compare a declared map with observed evidence. |
| `arch-linter-net topology verify` | Verify through ordinary validation semantics. |
| `arch-linter-net scaffold` | Repository-development scaffolding, not consumer adoption. |
| `arch-linter-net scaffold cli-command` | Scaffold a CLI module in this codebase. |
| `arch-linter-net schema` | Inspect installed schemas. |
| `arch-linter-net schema list` | List logical IDs and packaged schema identities. |
| `arch-linter-net schema print <logical-id>` | Print the exact installed schema bytes. |

### History analysis options

`history analyze` accepts an explicit Git range and can publish one analysis in
multiple formats with repeatable `--report` sinks.

| Option | Meaning |
| --- | --- |
| `--from`, `--to` | Required authored range endpoints. `--from` is exclusive and `--to` is inclusive. |
| `--repository`, `--policy` | Repository root (defaults to the current directory) and optional analysis policy. |
| `--format` | Select the legacy single-output format: `json` (default) or `markdown`. |
| `--report` | Repeatable `format=destination` sink; destination is `stdout`, `stderr`, or a file path. Supplying it selects only the requested formats and ignores `--format`. |
| `--timings` | Write phase timings and the ingestion invocation count to standard error. |

## Normal validation

```bash
arch-linter-net --policy architecture/arch.yml --mode strict --ensure-built \
  --report json=artifacts/architecture.json --report sarif=artifacts/architecture.sarif
```

Create the artifact directory first. The default policy path is
`architecture/dependencies.arch.yml`; examples using `architecture/arch.yml`
pass it explicitly.

### Core validation options

| Option | Meaning |
| --- | --- |
| `--policy`, `-p` | Root policy path. |
| `--mode`, `-m` | `strict`, `audit`, or supported combined `strict,audit`; default strict. |
| `--strict` / `--audit` | Single-mode shortcuts. |
| `--contract` | Select a contract ID; repeat for several. |
| `--condition-set` | Named source-analysis condition set. |
| `--baseline` | Reviewed finding baseline. |
| `--ensure-built` | Build the selected graph and verify its receipt before analysis. |
| `--no-restore` | With CLI-owned preparation, fail instead of restoring missing prerequisites. |
| `--configuration`, `--framework`, `--platform`, `--runtime` | Select compatible build state. |
| `--publish-prepared-receipts` | Producer preparation; may build unless a valid completed-build handoff exists. |
| `--use-prepared-receipts` | Verify prepared receipts without building/restoring. |
| `--waiver-evaluation-date` | Explicit UTC date for supported waiver-boundary evaluation. |
| `--external-evidence` | Logical SARIF binding; see [integration](../guides/sarif-integration.md). |
| `--evidence-repository`, `--evidence-revision`, `--evidence-scope` | Current assessment context, distinct from producer context. |
| `--cache` | Opt-in exact-request cache; eligibility is not guaranteed. |
| `--max-parallelism` | Positive scanning bound; `1` selects sequential execution. |
| `--timings`, `--profile` | Human timings and machine profile; see [performance diagnosis](../usage/timings.md). |
| `--format`, `-f`, `--json` | Primary output selection. |
| `--report` | Repeatable `format=destination` sinks from one completed validation. |

These are validation options, not flags accepted by every subcommand. For
example, history supports `--format` plus repeatable `--report` sinks, while
Health has human/JSON output and no SARIF renderer. Command help remains
version-specific.

### Build-state behavior

Ordinary mode uses available outputs and does not silently build.
`--ensure-built` owns preparation; prepared receipts are a separate producer/
consumer path. Read [prepared receipts](../usage/timings.md#prepared-receipts)
before replacing a build step. That feature and `health --change-snapshot` are
present in `0.9.0-preview.1`, not an implied capability of `0.8.2`.

## Policy review workflow

```bash
arch-linter-net policy check --policy architecture/arch.yml
arch-linter-net policy context --policy architecture/arch.yml --format json > current-policy.json
```

For weakening, export base and current contexts from their respective revisions
with the same CLI. The [review workflow](../guides/single-tool-workflow.md)
shows the actual worktree sequence.

`--public-api-approval` is optional. It binds the exact context digests,
contract and complete approved additions. When supplied, the current CLR API
must match the reviewed snapshot. It cannot approve removals, signature changes,
selector changes or a different comparison mode. See [public API contracts](../contracts/public-api-surface.md).

## Baseline workflow

Use [migration baselines](../guides/migration-baselines.md) for generation,
review, update, prune, diff, verify and migration. Writing a baseline is a
separate reviewed operation, never a normal CI response to a failed gate.

## No-new-debt gate

```bash
arch-linter-net gate --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml --mode all --ensure-built
```

`gate` requires an explicit baseline. Paired base/current contexts add the
policy-weakening guardrail. An explicitly empty baseline is allowed for zero
accepted finding debt; absence of a required input is not zero debt.

## Architecture health

`health` runs analysis and reports both `gate` and `health`. It does not consume
a previous validation JSON as a shortcut. Where supported, add
`--change-snapshot artifacts/current.json` to obtain the current snapshot from
that same analysis session. See the [complete recipe](../guides/single-tool-workflow.md)
and [Health reference](../reference/architecture-health.md).

`--base-context` and `--current-context` must be supplied together. Repeat
required external-evidence bindings on Health; a previous process does not
transfer its inputs automatically. Exit 2 may accompany a valid unassessable
Health document, not only a command error.

## Architecture pull-request report

```bash
arch-linter-net report pr --health artifacts/health.json \
  --change artifacts/change.json --output artifacts/pr.md --max-details 20
```

Both artifacts must have compatible mode and execution-context evidence.
`--output` is optional. `--max-details` bounds each section while retaining
canonical totals and omitted counts. Legacy Health without the reporting envelope
renders unavailable drill-down, not invented zeros.

Optional `--repository-url`, `--head-sha` and `--artifact-url` supply validated
GitHub bundle navigation. They do not change the decision. The renderer never
calls GitHub; a separate [publisher](../guides/ci-integration.md#secure-unified-architecture-pr-report-publication)
checks the current head, producer/run attempt, schema, size and hash.

## Change snapshots

A snapshot records the revision actually analyzed. Naming two outputs `base`
and `current` while staying in one checkout does not create a comparison.
Use the [two-revision recipe](../guides/single-tool-workflow.md#produce-a-review-from-two-exact-revisions).
Do not rebuild a current snapshot separately when Health already produced it.

## Coverage artifacts

```bash
arch-linter-net coverage report --input architecture-strict.json \
  --changed-files changed-files.txt --repo-root . --output architecture-coverage.md
```

This renders validation evidence; it does not replace [coverage contracts](../contracts/coverage.md).
Use `coverage extract` first when a consumer requires one mode from combined JSON.

## Dependency investigation

`graph --level namespace --format mermaid` exports dependencies;
`explain --source MyApp.Application --target MyApp.Infrastructure --level namespace`
explains a path. Supply the selected policy. `explain` supports namespace/type
levels; assembly topology is available through `graph --level assembly`.
For change over time, use [history forensics](../guides/history-forensics.md).

## Public API

[Public API surface contracts](../contracts/public-api-surface.md) own membership
and reviewed snapshots. Capture/update are intentional review operations; diff
is the read-only check. Do not make a CI job approve its own API changes.

## Topology review

Use [capture → review → declaration → diff/verify](../guides/topology-review-workflow.md).
Capture output is not a policy to apply automatically.

## Cache

See [eligibility and misses](../usage/timings.md#cache-eligibility-and-misses).
Enabling `--cache` does not prove a hit; unchanged output is not hit evidence.

## Packaged schemas

```bash
arch-linter-net schema list
arch-linter-net schema print policy-root
```

Use installed schema identities, not a schema URL guessed from package SemVer.

## Architecture Health badge

```bash
arch-linter-net badge architecture-health --input artifacts/health.json \
  --output artifacts/badge.json
```

The renderer uses canonical Health and policy inventory. It does not recalculate
counts or choose hosting. Missing/unassessable evidence remains
`UNASSESSABLE · ? ignores · ? rules`, not zero. Otherwise its exit preserves
Health's gate category. See [badge adoption](../guides/badge-adoption.md).

## Legacy architecture-policy badge

`badge architecture-policy --input architecture-strict.json` remains the
narrower strict-validation projection, not Architecture Health.

## Measure-first metrics

```bash
arch-linter-net measure --policy architecture/arch.yml --ensure-built --format json
```

Declare metrics first. Complete measurements exit 0; incomplete required scope
exits 2 with applicability evidence. `--metric`, `--max-contributors` and
`--all-contributors` select/bound the report. Scalar metric baselines and finding
baselines are separate; see [budgets](../policy-format/architecture-metrics.md).
Informational [repository metrics](../reference/repository-metrics.md) are a
different projection and do not create budget violations.

## Output guidance

Use JSON for complete structured evidence, SARIF for its supported code-scanning
projection and human output for diagnosis. Some supplemental failures do not
appear as ordinary SARIF results. Preserve the exit code and JSON as well;
see [output formats](../usage/output-formats.md) and [exit codes](../usage/exit-codes.md).
