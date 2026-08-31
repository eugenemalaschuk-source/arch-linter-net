# Supported Capabilities and Non-goals

This page is the concise human capability boundary. The machine-readable inventory is `archlinternet.capabilities.json`; detailed syntax belongs in the policy schema and contract reference.

`make lint-docs` checks this page, the contract index, CLI reference, navigation, and selected schema invariants against executable/machine sources so a newly implemented or removed capability cannot silently leave stale public guidance behind.

## Capability authority

Use this precedence when reviewing a discrepancy:

1. runtime/CLI implementation and packaged JSON Schema;
1. `archlinternet.capabilities.json`;
1. mechanically verified reference pages;
1. task guides and examples.

## Policy model

Supported policy features include:

- one selected root policy plus deterministic local imports;
- namespace/glob layers and selector-only semantic layers;
- exact role/metadata selectors, with namespace + selector using AND semantics;
- explicit external dependency, NuGet package, and framework-reference groups;
- reusable bounded `source_sets` for layers, assemblies, and projects;
- opt-in native declared topology with stable nodes, bounded mappings, directional edges, explicit partial/exhaustive scope, and reviewed out-of-scope declarations;
- project/solution discovery, source roots, condition sets, and build selectors;
- strict and audit contract groups;
- narrow ignored violations and reviewed migration baselines;
- CEL-backed `when` predicates only at the documented closed locations.

## Contract governance

The current machine inventory is rendered as the reviewed [Contract family index](../contracts/index.md). Major capabilities include:

- namespace/layer and assembly dependency governance;
- cycles, independence, protected surfaces, reusable layer templates, and module containers;
- project metadata, packages, framework references, and external dependencies;
- method-body forbidden calls and Unity `.asmdef` references;
- type placement, layout conventions, attributes, inheritance, interfaces, and composition roots;
- public API snapshots/surfaces;
- semantic contextual dependency/allow-only rules and port/ACL boundaries;
- architecture coverage.

Do not infer a family from a YAML-looking field that is absent from the machine inventory/schema.

## Implemented coverage scopes

<!-- coverage-scope: namespace -->

- `namespace` — first-party namespace inventory.

<!-- coverage-scope: project -->

- `project` — discovered project inventory.

<!-- coverage-scope: assembly -->

- `assembly` — first-party assembly inventory.

<!-- coverage-scope: dependency_edge -->

- `dependency_edge` — observed layer-to-layer dependency edges that must be governed.

<!-- coverage-scope: rule_input -->

- `rule_input` — declared rule inputs that must resolve and stay non-stale, with exact reviewed optional-empty support.

<!-- coverage-scope: semantic_role -->

- `semantic_role` — discovered semantic roles/metadata that must be governed or explicitly excluded.

All six scopes are runtime-supported. See [Coverage contracts](../contracts/coverage.md).

## Semantic classification and selectors

Implemented classification evidence includes:

- type attributes;
- assembly attributes;
- inheritance;
- namespace rules.

The normal precedence is `type_attribute > assembly_attribute > inheritance > namespace`. A layer may use `namespace`, `selector`, or both. Selector-only layers are supported; a combined layer requires both predicates.

Contextual dependency/allow-only and semantic port-boundary contracts use discovered role/metadata evidence directly rather than routing through a named layer.

Fields documented as deferred or reserved are not silently promoted to support just because a schema accepts them.

## CLI and governance workflows

The CLI supports normal validation plus:

- `policy check`, `policy context`, and `policy weakening`;
- baseline generate/update/prune/diff/verify/migrate;
- `gate` for no-new-debt and policy-weakening CI enforcement;
- `change snapshot` / `change report`;
- `coverage report` / `coverage extract`;
- `history analyze`;
- `public-api` capture/diff/update/migrate;
- `graph` and `explain`;
- `measure` for read-only, deterministic declared-metric reports before a budget is authored;
- persistent cache inspect/clear;
- packaged schema list/print;
- architecture-policy badge payload generation;
- CLI-command scaffolding for repository development.

See [CLI reference](../cli/index.md) for the executable command map.

## Build-state behavior

Validation can consume current build outputs or explicitly opt in to `--ensure-built`. Build-state preflight fails closed on missing, stale, or ambiguous inputs. `--no-restore` prevents implicit restore in the ensure-built workflow. Configuration/framework/platform/runtime selectors are explicit.

## Outputs

Supported output includes human, normalized JSON, SARIF where applicable, repeatable report sinks, timing diagnostics, analysis-profile JSON, architecture coverage summaries, cache/build-state evidence, and versioned Human/JSON metric measurement reports. Metric applicability counts are evidence completeness, not a quality score or threshold finding.

See [Output formats](../usage/output-formats.md) and [Exit codes](../usage/exit-codes.md).

## Non-goals

ArchLinterNet does not claim to provide:

- runtime dependency-injection correctness;
- authorization/security-policy correctness;
- ownership/CODEOWNERS enforcement;
- arbitrary semantic data-flow or taint analysis;
- regex-based arbitrary layer definitions;
- arbitrary user-defined contract families outside the packaged schema;
- runtime behavior verification of third-party packages;
- automatic architectural design decisions.

Use the tool to encode reviewed static architecture decisions, not to replace security testing, runtime integration testing, or design review.
