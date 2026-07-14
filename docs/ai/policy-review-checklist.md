# Policy Review Checklist

Use this checklist before opening or approving an AI-generated policy change.

## Repository facts

- [ ] The review identifies the one selected root and follows its complete import graph.
- [ ] Root/fragment roles come from entry/import relationships, not `arch.yml` or `*.arch.yml` names.
- [ ] Every layer maps to an existing namespace prefix or documented runtime namespace.
- [ ] Every `external_dependencies` group maps to a real vendor/framework namespace or type prefix.
- [ ] Every target assembly name is real for the repository being validated.
- [ ] `assembly_search_paths` point to real build output directories when needed.
- [ ] `source_roots` are included when method-body source scanning needs non-default roots.

## Contract safety

- [ ] Strict contracts pass today or intentionally enforce an accepted no-new-debt gate.
- [ ] Future-state or discovery rules are in audit groups, not strict groups.
- [ ] Allow-only rules are used for pure layers where every first-party dependency should be explicit.
- [ ] Vendor/framework leakage rules use `strict_external` / `audit_external` instead of pseudo-layers unless there is a deliberate compatibility reason.
- [ ] Ordered layer contracts do not mix broad aggregate layers with overlapping child layers.
- [ ] Module independence rules reflect real module boundaries, not idealized names.

## Ignores and debt

- [ ] Every `ignored_violations` entry is narrow enough to freeze known debt only.
- [ ] Every ignore has a reason with a migration note or issue reference.
- [ ] No ignore uses broad patterns unless a human explicitly accepted that baseline.
- [ ] New violations are not hidden by expanding an unrelated ignore.
- [ ] Generated baselines are reviewed before being committed.

## Schema and capability fit

- [ ] The selected root uses `schema/dependencies.arch.schema.json`, and imported partial documents use `schema/dependencies.arch.fragment.schema.json` regardless of filename.
- [ ] The policy uses only contract families listed in `archlinternet.capabilities.json` and public docs.
- [ ] No unsupported fields such as `regex`, `severity`, `from`, `to`, `owner`, custom groups, or unsupported namespace pattern syntax were invented.
- [ ] Any layer `namespace` using `*` uses it as a full segment and still maps to real repeated namespaces in the repository.
- [ ] Documentation and sample policy snippets match executable YAML.
- [ ] Semantic-role changes identify role, metadata, source, evidence, coverage deltas, and new cross-context edges for AI and human review.
- [ ] Ambiguous or uncovered roles remain visible until classified or explicitly excluded with a narrow reason.

## Fragment scope and composition

- [ ] Each edited fragment owns one coherent architecture concern or bounded context.
- [ ] Unrelated fragments were not reformatted, reorganized, or combined only to reduce file count.
- [ ] Small shared settings remain inline in the root when that is clearer.
- [ ] Layer, package, external-dependency, and condition-set keys remain unique across the complete graph.
- [ ] Contract IDs remain unique across the complete graph within each family and strict/audit mode.
- [ ] Root-inline and imported definitions do not rely on precedence or silent overrides.
- [ ] Import and contract list order changed only when the requested behavior requires it.

## Public documentation boundary

- [ ] Product usage docs are in the MkDocs public documentation tree.
- [ ] Internal backlog governance, OpenSpec archives, and repository-agent instructions are not linked as product docs.
- [ ] NuGet-facing links point to the public product docs and GitHub repository, not internal Markdown files.

## Local validation

- [ ] Root and fragment documents validate against their role-specific JSON Schemas when a schema validator is available.
- [ ] Strict validation was run locally through the one selected root.
- [ ] Audit validation was run locally if audit rules changed.
- [ ] Any failures are explained in the PR instead of hidden by broad ignores.
- [ ] Review confirms the change makes no runtime DI, runtime behavior, security, or semantic data-flow claims.
