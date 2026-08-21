# Policy Weakening Review

Policy changes deserve a separate review gate: a green current validation run
does not prove that governed scope or a boundary was retained. Configure the
change-time comparison severity under `analysis`:

```yaml
analysis:
  policy_weakening: error # error, warn, or off
```

This setting is read only by `arch-linter-net policy weakening`; it does not
change strict or audit validation. For a reviewed brownfield migration, use
`warn` temporarily with a narrow contract/ignore `reason` that names the
migration or issue. The comparison still emits the evidence — it is not
baseline debt and it must not be hidden by a broad exception.

Produce the base and current policy-context JSON artifacts from their own
repository states, then compare them. Do not pass two policy YAML files from
the current checkout and call one historical evidence.

```bash
# In the base repository/policy state
arch-linter-net policy context --policy architecture/dependencies.arch.yml --format json > base.json

# In the current repository/policy state
arch-linter-net policy context --policy architecture/dependencies.arch.yml --format json > current.json
arch-linter-net policy weakening --base-context base.json --current-context current.json --format human
```

The guardrail proves direction for same-ID strict-to-audit/removal, resolved
source-set and explicit analysis scope reduction, required-to-optional source
sets, source expansions, or rule inputs, matched subtractive exclusions, explicit
permission/prohibition inventories, and universal ignores. It recognizes a
universal ignore from the context artifact's typed `source_type` and
`forbidden_reference` matchers, never from the display string.
Changes to type, role, attribute, inheritance, CEL, or public-API selectors are
not guessed: without complete evaluator membership evidence they are reported
as `impact_not_proven` for review, with no fabricated affected types. Treat a
Shared, Common, or Utils-style exemption as a warning sign to narrow and
explain, not as a substitute for fixing code.

Semantic inventory comparison is explicit and shape-aware: only known scalar
sets of exact identities may prove a relaxation, while a boolean prohibition
has its own `true` → `false` direction. Prefixes, globs, call patterns, and
cross-field location allowances are `impact_not_proven` until a containment or
trusted effective-membership comparator exists. They are never silently
discarded or presented as proof of affected architecture subjects.

`analysis.project_include` and `analysis.project_exclude` are globs. A context
alone does not prove whether one glob contains another, so a changed glob is
also `impact_not_proven` unless complete resolved project membership evidence
is available. Do not treat the addition or removal of a glob string as a
semantic scope change.
