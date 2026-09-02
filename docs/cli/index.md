# CLI Reference

The executable command tree is the authority for command availability. This page is mechanically checked against `src/ArchLinterNet.Cli/Commands`: if a command is added or removed without updating the markers below, `make lint-docs` fails.

Run `arch-linter-net --help` or `arch-linter-net <command> --help` for the exact options accepted by the installed tool.

## Command map

| Command | Purpose |
| --- | --- |

<!-- cli-command: validate -->

| `arch-linter-net [options]` | Normal architecture validation. |

<!-- cli-command: badge -->

| `arch-linter-net badge` | Badge payload workflows. |

<!-- cli-command: badge architecture-policy -->

| `arch-linter-net badge architecture-policy --input <strict-result.json>` | Project an existing strict JSON result to a Shields endpoint payload; does not rerun analysis. |

<!-- cli-command: baseline -->

| `arch-linter-net baseline` | Migration-baseline lifecycle. |

<!-- cli-command: baseline generate -->

| `arch-linter-net baseline generate ...` | Capture current violations into a reviewed baseline. |

<!-- cli-command: baseline migrate -->

| `arch-linter-net baseline migrate ...` | Migrate supported baseline formats/identity. |

<!-- cli-command: baseline update -->

| `arch-linter-net baseline update ...` | Refresh a baseline from current findings under the requested policy. |

<!-- cli-command: baseline prune -->

| `arch-linter-net baseline prune ...` | Remove baseline entries that are no longer current. |

<!-- cli-command: baseline diff -->

| `arch-linter-net baseline diff ...` | Compare current findings with a baseline. |

<!-- cli-command: baseline verify -->

| `arch-linter-net baseline verify ...` | Verify baseline integrity/current applicability. |

<!-- cli-command: cache -->

| `arch-linter-net cache` | Persistent analysis-cache operations. |

<!-- cli-command: cache inspect -->

| `arch-linter-net cache inspect --cache <auto|path>` | Inspect the selected cache. |

<!-- cli-command: cache clear -->

| `arch-linter-net cache clear --cache <auto|path>` | Clear the selected cache with containment checks. |

<!-- cli-command: change -->

| `arch-linter-net change` | Complete architecture change snapshots/reports. |

<!-- cli-command: change snapshot -->

| `arch-linter-net change snapshot --policy <path> --output <path>` | Write a complete architecture change snapshot; use build-state options when a consumer requires post-build analysis. |

<!-- cli-command: change report -->

| `arch-linter-net change report --base <path> --current <path> --execution-context <id>` | Compare two architecture snapshots into a correlatable report artifact. |

<!-- cli-command: coverage -->

| `arch-linter-net coverage` | Architecture coverage artifact utilities. |

<!-- cli-command: coverage report -->

| `arch-linter-net coverage report --input <validation.json> ...` | Render a Markdown coverage report from strict validation JSON. |

<!-- cli-command: coverage extract -->

| `arch-linter-net coverage extract --input <combined.json> --mode <mode> --output <path>` | Extract one validation mode from combined JSON. |

<!-- cli-command: explain -->

| `arch-linter-net explain --source <id> --target <id> ...` | Explain a dependency path at namespace/type granularity. |

<!-- cli-command: gate -->

| `arch-linter-net gate ...` | Fail CI on new architecture debt and error-severity policy weakening. |

<!-- cli-command: health -->

| `arch-linter-net health ...` | Project the canonical non-compensating architecture-health/v1 summary. |

<!-- cli-command: graph -->

| `arch-linter-net graph ...` | Export dependency graphs as JSON, DOT, or Mermaid at supported granularities. |

<!-- cli-command: measure -->

| `arch-linter-net measure ...` | Read-only, deterministic report of declared architecture metrics. |

<!-- cli-command: history -->

| `arch-linter-net history` | Architecture history forensics. |

<!-- cli-command: history analyze -->

| `arch-linter-net history analyze ...` | Analyze architecture evidence/history for the requested repository range. |

<!-- cli-command: policy -->

| `arch-linter-net policy` | Policy-only inspection/review workflows. |

<!-- cli-command: policy check -->

| `arch-linter-net policy check --policy <path>` | Validate policy/static configuration without claiming architecture compliance. |

<!-- cli-command: policy context -->

| `arch-linter-net policy context --policy <path> --format <json|markdown>` | Export effective policy facts for humans/agents. |

<!-- cli-command: policy weakening -->

| `arch-linter-net policy weakening --base-context <path> --current-context <path>` | Compare exported contexts for typed policy relaxations. |

<!-- cli-command: public-api -->

| `arch-linter-net public-api` | Public API snapshot lifecycle. |

<!-- cli-command: public-api capture -->

| `arch-linter-net public-api capture ...` | Capture a reviewed public API snapshot. |

<!-- cli-command: public-api diff -->

| `arch-linter-net public-api diff ...` | Compare public API snapshots/current surface. |

<!-- cli-command: public-api migrate -->

| `arch-linter-net public-api migrate ...` | Migrate supported snapshot grammar/identity. |

<!-- cli-command: public-api update -->

| `arch-linter-net public-api update ...` | Update a reviewed public API snapshot. |

<!-- cli-command: report -->

| `arch-linter-net report` | Render reports from canonical local architecture artifacts. |

<!-- cli-command: report pr -->

| `arch-linter-net report pr --health <architecture-health.json> --change <architecture-change.json>` | Render a deterministic architecture-only pull-request Markdown report from canonical artifacts; does not rerun analysis or call GitHub. |

<!-- cli-command: scaffold -->

| `arch-linter-net scaffold` | Repository-development scaffolding. |

<!-- cli-command: scaffold cli-command -->

| `arch-linter-net scaffold cli-command --module <name> --command <name> ...` | Scaffold a CLI command module in this codebase. |

<!-- cli-command: schema -->

| `arch-linter-net schema` | Installed schema-registry discovery. |

<!-- cli-command: schema list -->

| `arch-linter-net schema list` | List packaged logical schemas. |

<!-- cli-command: schema print -->

| `arch-linter-net schema print <logical-id>` | Print one packaged schema for offline tooling/editors. |

## Normal validation

```bash
arch-linter-net \
  --policy architecture/arch.yml \
  --mode strict \
  --ensure-built
```

The CLI compatibility default for `--policy` is `architecture/dependencies.arch.yml`. The documentation uses `architecture/arch.yml` as a concise recommended convention, so examples pass it explicitly.

### Core validation options

| Option | Meaning |
| --- | --- |
| `-p, --policy <path>` | Selected root policy. |
| `-m, --mode <strict|audit>` | Validation mode; default is strict. |
| `--strict` / `--audit` | Mode shortcuts. |
| `--contract <id>` | Restrict execution to a contract ID; repeat where supported. |
| `--condition-set <name>` | Select a configured preprocessor symbol set for source analysis. |
| `--baseline <path>` | Merge reviewed baseline identities with policy ignores. |
| `--ensure-built` | Explicitly build the selected project graph once, verify its build receipt, then validate. |
| `--no-restore` | In ensure-built mode, fail closed if restore is required. |
| `--configuration <name>` | Build-state configuration selector. |
| `--framework <tfm>` | Target-framework selector. |
| `--platform <name>` | Platform selector. |
| `--runtime <rid>` | Runtime identifier selector. |
| `--max-parallelism <n>` | Bound parallel assembly/fact scanning; `1` is supported sequential execution. |
| `--waiver-evaluation-date <yyyy-MM-dd>` | Use a fixed UTC calendar date for waiver expiry evaluation. |
| `--cache <auto|path>` | Opt into persistent analysis-cache/v1; disabled by default. |
| `--timings` | Print phase timing information to stderr. |
| `--profile <stdout|stderr|path>` | Emit analysis-profile/v1 JSON independently from normal reports. |
| `-f, --format <human|json|sarif>` | Primary stdout format. |
| `--json` | Shortcut for JSON stdout. |
| `--report <format=destination>` | Add repeatable human/JSON/SARIF sinks to stdout, stderr, or a file. |
| `-h, --help` | Help. |
| `-v, --version` | Tool version. |

Exit codes for normal validation are `0` passed, `1` architecture/policy findings failed the run, and `2` runtime/argument/input error. See [Exit codes](../usage/exit-codes.md) for command-specific details.

### Build-state behavior

`--ensure-built` is never implicit. It exists to make build provenance explicit and reproducible; normal validation can consume already-built outputs.

The Apple Silicon self-dogfood failure tracked in #639 is fixed on current `main` by #648. Evergreen docs describe the fixed behavior. If reproducing an older release artifact, use the release-provenance workflow instead of assuming the historical defect still exists.

## Policy review workflow

Static policy validation:

```bash
arch-linter-net policy check --policy architecture/arch.yml
```

Effective policy facts:

```bash
arch-linter-net policy context \
  --policy architecture/arch.yml \
  --format json > current-policy.json
```

Base/current weakening review:

```bash
arch-linter-net policy weakening \
  --base-context base-policy.json \
  --current-context current-policy.json
```

`policy weakening` compares exported contexts. It is a bounded change-time guardrail, not a second architecture evaluator; `impact_not_proven` means review is required.

## Baseline workflow

Capture current debt:

```bash
arch-linter-net baseline generate \
  --config architecture/arch.yml \
  --output architecture/baseline.arch.yml \
  --reason "Reviewed adoption baseline"
```

Use `update`, `prune`, `diff`, and `verify` during normal maintenance. Use `migrate` when moving supported baseline identity/format forward. See [Migration baselines](../guides/migration-baselines.md).

## No-new-debt gate

```bash
arch-linter-net gate \
  --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml \
  --mode all \
  --ensure-built
```

`gate` can also consume exported base/current policy contexts so CI catches both new findings and error-severity policy weakening.

## Architecture health

```bash
arch-linter-net health \
  --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml \
  --mode all \
  --format json
```

`health` is a read-only projection of canonical architecture-governance authorities. It reports
the ordered `architecture-health/v1` dimensions and their reasons in human or JSON output. The
projection is non-compensating: it has no score, percentage, letter grade, badge, pull-request
rendering, or SARIF output. A valid but unassessable result is emitted as a health document rather
than a command-error document.

For topology, metric budgets, and imported external diagnostics, `evaluable` means only that the
authority could assess the control; the health dimension still reflects that authority's resulting
strict finding or clean receipt. Each reason retains canonical family, control, policy, and evidence
references so automation can drill into the source receipt. A stale or otherwise blocking waiver
lifecycle record remains a failing lifecycle result; a resolved baseline entry remains visible as
baseline hygiene but is not classified as new architecture debt.

Coverage retains its existing severity semantics in Health: `analysis.coverage: error` is failing,
while `warn` remains non-blocking reportable evidence. For `--mode audit` and `--mode all`,
`audit_evidence` preserves audit-only diagnostics without turning the Health gate into a strict
failure.

## Architecture pull-request report

Render a reviewer-oriented Markdown report from a canonical Health artifact and a canonical
architecture-change report:

```bash
arch-linter-net report pr \
  --health architecture-health.json \
  --change architecture-change.json \
  --output architecture-pr-report.md \
  --max-details 20
```

`--output` is optional; without it, Markdown is written to standard output. `--max-details` is
optional and must be a positive count. It bounds each detailed evidence section independently while
retaining canonical totals and making omitted details explicit. The report is deterministic and
architecture-only: it reads the supplied artifacts, does not run or recreate analysis, and does not
inspect or call GitHub.

The Health input must be an `architecture-health/v1` document from a supported CLI, including its
versioned canonical reporting evidence and non-empty execution context. The change input is the
versioned canonical architecture-change JSON report with the same execution context. `report pr`
rejects legacy Health or change artifacts and any pair whose execution identifier, condition set, or
mode receipt does not correlate. The command does not reopen snapshots or compare them again.

Create the pair from the real producers using one workflow-owned identifier:

```bash
# Base and candidate checkouts/worktrees, respectively
arch-linter-net change snapshot --policy architecture/arch.yml --mode strict --output base-snapshot.json
arch-linter-net change snapshot --policy architecture/arch.yml --mode strict --output current-snapshot.json

arch-linter-net change report \
  --base base-snapshot.json \
  --current current-snapshot.json \
  --execution-context pr-123 \
  --format json \
  --output architecture-change.json

arch-linter-net health \
  --policy architecture/arch.yml \
  --format json \
  --execution-context pr-123 \
  > architecture-health.json
```

The report headline repeats direct Health/projection facts such as `gate` and `health`; it is not a
score, percentage, grade, or compensating quality calculation. Rule/effective-control counts,
applicability completeness, topology evidence, and external evidence remain separate sections and
must not be combined or inferred from one another. Missing or incomplete canonical evidence is
rendered as unavailable or unassessable, never as zero or pass. Canonical identities and provenance
are retained where supplied so reviewers can drill back to the source artifacts.

This command only renders the local report. GitHub comment publication, workflow/event orchestration,
security permissions, and related integration remain outside this command's boundary in #681.

## Change snapshots

```bash
arch-linter-net change snapshot \
  --policy architecture/arch.yml \
  --mode strict \
  --ensure-built --configuration Debug --framework net10.0 \
  --output base-snapshot.json

arch-linter-net change snapshot \
  --policy architecture/arch.yml \
  --mode strict \
  --ensure-built --configuration Debug --framework net10.0 \
  --output current-snapshot.json

arch-linter-net change report \
  --base base-snapshot.json \
  --current current-snapshot.json \
  --execution-context local-review \
  --format human
```

When the policy opts into a shared framework, use `--ensure-built` for both
snapshots and keep `--configuration`, `--framework`, `--platform`, and `--runtime`
consistent across them. `--no-restore` preserves an offline, fail-closed build
when the consumer has already restored its prerequisites.

Snapshots are architecture evidence; reports compare two complete snapshots rather than reparsing arbitrary prose.

## Coverage artifacts

Contract coverage runs during validation. The `coverage` command family post-processes validation JSON:

```bash
arch-linter-net coverage report \
  --input architecture-strict.json \
  --changed-files changed-files.txt \
  --repo-root . \
  --output architecture-coverage.md
```

Implemented policy coverage scopes are documented in [Coverage contracts](../contracts/coverage.md).

## Dependency investigation

Export a graph:

```bash
arch-linter-net graph \
  --policy architecture/arch.yml \
  --mode all \
  --level namespace \
  --format mermaid
```

Explain a path:

```bash
arch-linter-net explain \
  --policy architecture/arch.yml \
  --source MyApp.Application \
  --target MyApp.Infrastructure \
  --level namespace
```

`explain` supports namespace/type granularity. For assembly-level topology, use `graph --level assembly`.

Use `history analyze` when the question is how architecture evidence changed over repository history rather than how the current graph is connected.

## Public API

The `public-api` command family supports capture, diff, update, and migration of reviewed public API snapshots used by public API surface contracts. See [Public API surface contracts](../contracts/public-api-surface.md).

## Cache

Persistent analysis cache is opt-in:

```bash
arch-linter-net cache inspect --cache auto
arch-linter-net cache clear --cache auto
```

An explicit directory is also supported and is validated for safe containment. Validation itself enables the cache only when `--cache` is supplied.

## Packaged schemas

```bash
arch-linter-net schema list
arch-linter-net schema print policy-root
```

Use these commands for installed/offline schema discovery rather than deriving schema identity from package SemVer.

## Architecture-policy badge

```bash
arch-linter-net badge architecture-policy \
  --input architecture-strict.json
```

This projects an existing strict result into badge endpoint JSON. It does not rerun architecture analysis.

## Measure-first metrics

```bash
arch-linter-net measure --policy architecture/dependencies.arch.yml
arch-linter-net measure --format json --metric application-outgoing
arch-linter-net measure --all-contributors
```

`measure` is read-only: it evaluates only policy-owned metric definitions and
does not create a budget violation, rewrite a policy/baseline, or produce a
SARIF report. Human output is the default; JSON uses
`schema_id: "architecture-metrics-report/v1"` and `schema_version: 1`, and
contains the native subject, effective scope, exact value when evaluable, and
canonical contributors. By default, each contributor list is bounded to 20;
use `--max-contributors <n>` to set another positive bound or
`--all-contributors` to emit every contributor. JSON retains the full
`contributor_count` and a `contributors_truncated` marker whenever it bounds a
list.

A complete measurement, including a trusted value of zero, exits 0. If a
required metric scope is incomplete, the command still reports its typed shared
applicability evidence but exits 2. That result is evidence completeness, not
an architecture violation or quality score.

## Output guidance

- Use human output for local diagnosis.
- Use JSON when downstream tooling needs the complete normalized finding/coverage/build-state model.
- Use `measure --format json` for the separate, versioned read-only metric-report model.
- Use SARIF for supported code-scanning projections, noting that not every non-SARIF finding category is representable there.
- Use repeatable `--report` sinks when CI needs multiple formats from one validation run.

When a policy provides applicability evidence, all three formats add the same deterministic completion
projection: canonical control identity and provenance, membership/state records, and
`required`/`evaluable`/`unassessable`/`not_applicable` counts. JSON exposes it in
`assessment_completion` and additive `applicability_findings`; SARIF places the completion data in
the run properties and the normalized findings in SARIF results. These counts show evidence
completeness—not architecture quality—and do not replace the separately owned effective-rule count.

When Core produces policy-inventory evidence, human and JSON validation output
also disclose the canonical effective-control count and explicit waiver debt.
The `policy_inventory` JSON object is the source for downstream architecture
Health/report/badge consumers; do not recalculate either number from policy
YAML, findings, or exclusion syntax. Its strict/audit/coverage count is
repository-level for the selected policy even when validation evaluates only one
finding mode. Its waiver records and debt totals use the same selected
repository scope; mode-local waiver output still governs only that mode's
validation result. Missing inventory evidence is not a zero-debt result.

See [Output formats](../usage/output-formats.md) and [Timings](../usage/timings.md).
