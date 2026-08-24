# ArchLinterNet

ArchLinterNet is a YAML-first architecture governance tool for .NET repositories. It turns architecture decisions into executable contracts that can be checked locally, in tests, and in CI.

The public documentation describes the behavior of the current supported tool line. Runtime validators, the CLI command tree, and the packaged policy schemas are the highest-precedence sources of truth; the machine-readable `archlinternet.capabilities.json` inventory and this site are checked against them by `make lint-docs`.

## Start here

For a first adoption, follow this path:

1. [Install ArchLinterNet](installation/index.md), preferably as a repository-local .NET tool.
1. Create a minimal root policy such as `architecture/arch.yml`.
1. Run `arch-linter-net policy check --policy architecture/arch.yml`.
1. Build the selected projects, or opt in to `--ensure-built`.
1. Run strict validation.
1. Add the same strict command to CI.
1. If the repository has existing debt, capture a reviewed migration baseline instead of weakening rules.
1. Add coverage contracts so new namespaces, projects, assemblies, dependency edges, rule inputs, and semantic roles cannot silently escape governance.
1. Add advanced contracts only where they express a real architectural decision.

The [Getting Started](getting-started/index.md) guide walks through the complete flow. For an existing production repository, use the [real-repository workflow](guides/real-repository-workflow.md).

## What ArchLinterNet can govern

The current contract model covers more than namespace layering:

- layer, allow-only, protected-surface, cycle, independence, assembly, and Unity `.asmdef` boundaries;
- project metadata, NuGet package, framework reference, external dependency, and reusable source-set governance;
- type placement, source-layout conventions, attribute usage, inheritance, interface implementation, composition roots, and public API surfaces;
- semantic classification, selector-backed layers, contextual dependencies, contextual allow-only rules, and semantic port/ACL boundaries;
- architecture coverage for namespaces, projects, assemblies, dependency edges, rule inputs, and semantic roles;
- migration baselines, policy consistency, policy-context export and weakening review;
- change snapshots/reports, no-new-debt gates, history forensics, dependency graph export, and dependency-path explanation;
- deterministic JSON/SARIF/human output, analysis profiles, optional persistent analysis cache, and CI-oriented report sinks.

See [Contract families](contracts/index.md) and [Supported capabilities and non-goals](policy-format/supported-capabilities.md) for the reviewed inventory.

## High-value workflows

### Validate architecture

```bash
arch-linter-net --policy architecture/arch.yml --mode strict --ensure-built
```

`--ensure-built` is explicit: without it ArchLinterNet validates available build outputs and reports stale or missing state rather than silently rebuilding.

### Review policy changes

```bash
arch-linter-net policy context --policy architecture/arch.yml --format json > current-policy.json
arch-linter-net policy weakening --base-context base-policy.json --current-context current-policy.json
```

Use policy context and weakening analysis to review whether a policy edit relaxes governance. This does not replace normal architecture validation.

### Gate new debt

```bash
arch-linter-net gate \
  --policy architecture/arch.yml \
  --baseline architecture/baseline.arch.yml \
  --mode all
```

The gate combines validation with no-new-debt and policy-weakening checks intended for CI.

### Investigate architecture changes

```bash
arch-linter-net change snapshot --policy architecture/arch.yml --output before.json
arch-linter-net change snapshot --policy architecture/arch.yml --output after.json
arch-linter-net change report --base before.json --current after.json
```

For dependency investigation, use `graph`, `explain`, and `history analyze`. For public API governance, use the `public-api` workflow.

## Documentation map

- [Getting Started](getting-started/index.md)
- [Installation](installation/index.md)
- [CLI reference](cli/index.md)
- [Policy format](policy-format/index.md)
- [YAML schema reference](reference/yaml-schema.md)
- [Contract families](contracts/index.md)
- [Supported capabilities and non-goals](policy-format/supported-capabilities.md)
- [CI integration](guides/ci-integration.md)
- [Migration baselines](guides/migration-baselines.md)
- [Real-repository workflow](guides/real-repository-workflow.md)
- [AI policy authoring](ai/index.md)

## Non-goals

ArchLinterNet is a static architecture-governance tool. It does not prove runtime dependency-injection behavior, authorization/security correctness, code ownership, or arbitrary semantic data flow, and it does not accept undocumented custom contract families outside the packaged schema.
