# ArchLinterNet

ArchLinterNet is a YAML-first architecture governance tool for .NET repositories. It turns architecture decisions into executable contracts that can be checked locally, in tests, and in CI.

**One ArchLinterNet CLI covers the complete static architecture-governance cycle:** policy validation, applicability/completeness, architecture validation, topology, visible contract surfaces, debt and structured waivers, metrics/budgets, imported SARIF evidence, architecture change, Architecture Health, PR Markdown, machine artifacts, and the real Architecture Health badge. CI invokes and transports these product artifacts; it is not a second governance implementation.

The public documentation describes the behavior of the current supported tool line. Runtime validators, the CLI command tree, and the packaged policy schemas are the highest-precedence sources of truth; the machine-readable `archlinternet.capabilities.json` inventory and this site are checked against them by `make lint-docs`.

## Start here

For a first adoption, follow this path:

1. [Install ArchLinterNet](installation/index.md), preferably as a repository-local .NET tool.
1. Create a minimal root policy such as `architecture/arch.yml`.
1. Run `arch-linter-net policy check --policy architecture/arch.yml`.
1. Build the selected projects, or opt in to `--ensure-built`.
1. Run strict validation and inspect applicability/completeness evidence.
1. If the repository has existing debt, capture a reviewed migration baseline instead of weakening rules.
1. Migrate manual exceptions to [structured waivers](policy-format/structured-waivers.md) and keep waiver debt distinct from finding debt.
1. Add coverage, topology, contract-surface, metrics/budgets, and external evidence where they express real governance decisions.
1. Project [Architecture Health](reference/architecture-health.md), architecture change, PR Markdown, JSON/SARIF, and the Health badge from canonical CLI artifacts.
1. Put the same product workflow in CI; keep CI glue limited to invocation, evidence transport, integrity checks, and publication.

The [Getting Started](getting-started/index.md) guide covers the minimal path. The [complete single-tool workflow](guides/single-tool-workflow.md) covers the full v0.8 governance cycle. Existing adopters should use the [real-repository workflow](guides/real-repository-workflow.md), evergreen [adopt-or-upgrade guide](guides/upgrading.md), and focused [v0.7 to v0.8 migration](guides/v07-to-v08-adoption.md).

## Complete governance cycle

```text
install/pin
  -> declare policy
  -> policy check
  -> analyze + prove applicability/completeness
  -> validate topology and visible contract surfaces
  -> govern finding debt, waiver debt, new debt and weakening
  -> measure architecture and enforce budgets
  -> bind required current-context SARIF evidence
  -> inspect architecture change
  -> Architecture Health
  -> PR Markdown / JSON / SARIF / Health badge
```

Architecture Health keeps gate and health separate. The gate is `pass`, `fail`, or `unassessable`; Health is `healthy`, `debt`, `degrading`, `failing`, or `unassessable`. These are deterministic non-compensating states, not a weighted score, letter grade, or universal percentage.

## What ArchLinterNet can govern

The current contract model covers more than namespace layering:

- layer, allow-only, protected-surface, cycle, independence, assembly, and Unity `.asmdef` boundaries;
- project metadata, NuGet package, framework reference, external dependency, and reusable source-set governance;
- type placement, source-layout conventions, attribute usage, inheritance, interface implementation, composition roots, and public API surfaces;
- semantic classification, selector-backed layers, contextual dependencies, contextual allow-only rules, and semantic port/ACL boundaries;
- architecture coverage for namespaces, projects, assemblies, dependency edges, rule inputs, and semantic roles;
- native declared topology with capture/diff/verify and exhaustive completeness evidence;
- recursive visible contract-surface exposure and version-isolation governance;
- architecture metrics, absolute budgets, and baseline-relative no-worse-than/delta budgets;
- repository-local vendor-neutral SARIF evidence with current repository/revision/scope binding;
- migration baselines, structured waiver lifecycle, policy inventory, policy consistency, policy-context export and weakening review;
- change snapshots/reports, no-new-debt gates, Architecture Health, deterministic PR Markdown, and Health badge projection;
- history forensics, dependency graph export, dependency-path explanation, deterministic JSON/SARIF/human output, analysis profiles, optional persistent analysis cache, and CI-oriented report sinks.

See [Contract families](contracts/index.md) and [Supported capabilities and non-goals](policy-format/supported-capabilities.md) for the reviewed inventory.

## High-value workflows

### Validate architecture

```bash
arch-linter-net --policy architecture/arch.yml --mode strict --ensure-built
```

`--ensure-built` is explicit: without it ArchLinterNet validates available build outputs and reports stale or missing state rather than silently rebuilding.

### Review policy changes

Export contexts from the actual base and candidate revisions with the same pinned CLI version:

```bash
# Reviewed base checkout/worktree
arch-linter-net policy context \
  --policy architecture/arch.yml \
  --format json > /shared/artifacts/policy-base.json

# Candidate checkout/worktree
arch-linter-net policy context \
  --policy architecture/arch.yml \
  --format json > artifacts/policy-current.json

arch-linter-net policy weakening \
  --base-context artifacts/policy-base.json \
  --current-context artifacts/policy-current.json
```

Use policy context and weakening analysis to review whether a policy edit relaxes governance. This does not replace normal architecture validation.

### Gate new debt and weakening

```bash
arch-linter-net gate \
  --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml \
  --base-context artifacts/policy-base.json \
  --current-context artifacts/policy-current.json \
  --mode all \
  --ensure-built
```

The gate combines no-new-debt comparison with policy-weakening review when both context artifacts are supplied. It never writes a baseline.

### Project Architecture Health

```bash
arch-linter-net health \
  --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml \
  --base-context artifacts/policy-base.json \
  --current-context artifacts/policy-current.json \
  --mode strict \
  --execution-context local-review \
  --format json > architecture-health.json
```

Use the canonical Health artifact for reviewer reports and the real Architecture Health badge instead of rebuilding Health semantics in scripts. Add the policy-required external-evidence bindings to this invocation when configured.

### Investigate architecture changes

```bash
# Create before.json in the reviewed base checkout and after.json in the candidate checkout.
arch-linter-net change snapshot --policy architecture/arch.yml --mode strict --output before.json
arch-linter-net change snapshot --policy architecture/arch.yml --mode strict --output after.json
arch-linter-net change report \
  --base before.json \
  --current after.json \
  --execution-context local-review \
  --format json \
  --output architecture-change.json
```

For dependency investigation, use `graph`, `explain`, and `history analyze`. For public API governance, use the `public-api` workflow. The [complete workflow](guides/single-tool-workflow.md) shows how compatible change and Health artifacts feed `report pr`.

## Documentation map

- [Getting Started](getting-started/index.md)
- [Complete single-tool governance workflow](guides/single-tool-workflow.md)
- [Installation](installation/index.md)
- [CLI reference](cli/index.md)
- [Policy format](policy-format/index.md)
- [Structured waivers](policy-format/structured-waivers.md)
- [YAML schema reference](reference/yaml-schema.md)
- [Architecture Health](reference/architecture-health.md)
- [Contract families](contracts/index.md)
- [Supported capabilities and non-goals](policy-format/supported-capabilities.md)
- [CI integration](guides/ci-integration.md)
- [Migration baselines](guides/migration-baselines.md)
- [Adopt or upgrade](guides/upgrading.md)
- [v0.7 to v0.8 adoption](guides/v07-to-v08-adoption.md)
- [Real-repository workflow](guides/real-repository-workflow.md)
- [AI policy authoring](ai/index.md)

## Non-goals

ArchLinterNet is a static architecture-governance tool. It does not prove runtime dependency-injection behavior, authorization/security correctness, code ownership, runtime serialization behavior, or arbitrary semantic data flow, and it does not accept undocumented custom contract families outside the packaged schema.
