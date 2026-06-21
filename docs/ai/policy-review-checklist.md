# Policy Review Checklist

Use this checklist before opening or approving an AI-generated policy change.

## Repository Facts

- [ ] Every layer maps to an existing namespace prefix or documented runtime namespace.
- [ ] Every `external_dependencies` group maps to a real vendor/framework namespace or type prefix.
- [ ] Every target assembly name is real for the repository being validated.
- [ ] `assembly_search_paths` point to real build output directories when needed.
- [ ] `source_roots` are included when method-body source scanning needs non-default roots.

## Contract Safety

- [ ] Strict contracts pass today or intentionally enforce an accepted no-new-debt gate.
- [ ] Future-state or discovery rules are in audit groups, not strict groups.
- [ ] Allow-only rules are used for pure layers where every first-party dependency should be explicit.
- [ ] Vendor/framework leakage rules use `strict_external` / `audit_external` instead of pseudo-layers unless there is a deliberate compatibility reason.
- [ ] Ordered layer contracts do not mix broad aggregate layers with overlapping child layers.
- [ ] Module independence rules reflect real module boundaries, not idealized names.

## Ignores And Debt

- [ ] Every `ignored_violations` entry is narrow enough to freeze known debt only.
- [ ] Every ignore has a reason with a migration note or issue reference.
- [ ] No ignore uses broad `*` patterns unless a human explicitly accepted that baseline.
- [ ] New violations are not hidden by expanding an unrelated ignore.

## Schema And Capability Fit

- [ ] The policy uses only fields supported by `schema/dependencies.arch.schema.json`.
- [ ] The policy uses only contract families listed in `archlinternet.capabilities.json`.
- [ ] No unsupported fields such as `regex`, `severity`, `from`, `to`, custom groups, or unsupported glob syntax (`**`, `?`, `Feature*`) were invented.
- [ ] Any layer `namespace` using `*` uses it as a full segment and still maps to real repeated namespaces in the repository.
- [ ] Documentation and sample policy snippets match executable YAML.

## Local Validation

- [ ] The policy validates against the JSON Schema when a schema validator is available.
- [ ] Strict validation was run locally.
- [ ] Audit validation was run locally if audit rules changed.
- [ ] Any failures are explained in the PR instead of hidden by broad ignores.
