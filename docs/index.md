# ArchLinterNet

ArchLinterNet is a YAML-first architecture linter for .NET repositories.

It lets teams define architecture boundaries once in a repository policy file and run those rules from the CLI, tests, or CI. The goal is practical architecture governance: strict gates for rules that must pass today, audit diagnostics for migration work, and readable output for both humans and automation.

## Product documentation only

This site is the public product documentation for ArchLinterNet. It is the only documentation intended for GitHub Pages publication and NuGet.org documentation links.

Internal project documentation such as backlog governance, OpenSpec archives, implementation planning, and repository-agent instructions remains in GitHub Markdown files outside this published site.

## Core workflow

```text
architecture/dependencies.arch.yml
        ↓
ArchLinterNet validation
        ↓
strict gate or audit report
        ↓
CI result, JSON artifact, or test failure
```

## Start here

- [Getting Started](getting-started/index.md) — first run and mental model.
- [Installation](installation/index.md) — .NET tool, local tool, and package usage.
- [First policy](getting-started/first-policy.md) — a minimal working YAML policy.
- [CI integration](guides/ci-integration.md) — strict blocking + audit non-blocking workflow.

## Author policies

- [Policy format](policy-format/index.md) — top-level YAML structure.
- [Layers and namespace patterns](policy-format/layers-and-namespaces.md) — literal prefixes, constrained globs, suffixes, and external layers.
- [External dependencies](policy-format/external-dependencies.md) — vendor/framework leakage modeling.
- [Condition sets](policy-format/condition-sets.md) — conditional compilation for source/method-body analysis.
- [Supported capabilities and non-goals](policy-format/supported-capabilities.md) — what the tool can and cannot validate.

## Contract families

ArchLinterNet supports strict and audit variants for dependency, ordered layer, allow-only, cycle, acyclic sibling, independence, protected surface, external dependency, method-body, Unity `.asmdef`, and layer-template contracts.

See the [Contracts](contracts/index.md) section for the full reference.

## AI-assisted policy authoring

The [AI section](ai/index.md) documents how AI coding agents and human reviewers should inspect repositories, author safe YAML, avoid unsupported fields, and review generated policies before opening a PR.

## What ArchLinterNet is not

ArchLinterNet is not a runtime analyzer, security analyzer, semantic data-flow engine, ownership system, or enterprise architecture dashboard. It is intentionally focused on executable static architecture contracts that can run locally and in CI.
