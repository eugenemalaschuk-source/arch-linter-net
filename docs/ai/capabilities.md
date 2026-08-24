# Capabilities for AI Policy Authors

AI agents must use the same capability truth as human users. This page intentionally does not maintain a second contract-family matrix.

The machine-readable capability inventory is `archlinternet.capabilities.json`. The human inventory is [Supported capabilities and non-goals](../policy-format/supported-capabilities.md), and the exact family/group mapping is [Contract families](../contracts/index.md). Runtime validators, the CLI tree, and packaged schemas take precedence if a discrepancy is discovered.

## Safe authoring workflow

Before editing a policy:

1. Read the selected root policy and imports.
2. Read `archlinternet.capabilities.json`.
3. Use the packaged/root JSON Schema instead of inventing fields.
4. Run `policy check` for static policy validity.
5. Export `policy context` when the change depends on effective selectors, source sets, exceptions, or semantic evidence.
6. Run normal strict/audit validation after the edit.
7. For base/current review, run `policy weakening`; treat `impact_not_proven` as a review requirement, not proof of safety.

## Layer semantics

A layer may be:

- namespace-backed;
- selector-backed;
- both namespace- and selector-backed.

Selector-only layers are supported. A combined layer uses AND semantics. Selector metadata uses the documented exact/operator semantics for its location; never infer regex or arbitrary expression support.

## Implemented coverage scopes

<!-- coverage-scope: namespace -->
- `namespace`
<!-- coverage-scope: project -->
- `project`
<!-- coverage-scope: assembly -->
- `assembly`
<!-- coverage-scope: dependency_edge -->
- `dependency_edge`
<!-- coverage-scope: rule_input -->
- `rule_input`
<!-- coverage-scope: semantic_role -->
- `semantic_role`

Do not describe any of these six scopes as reserved or unsupported. See [Coverage contracts](../contracts/coverage.md).

## Semantic governance

Implemented classification evidence includes type attributes, assembly attributes, inheritance, and namespace rules. Selector-backed layers, contextual dependency/allow-only contracts, semantic port boundaries, and semantic-role coverage consume the effective role/metadata model.

Schema-accepted fields explicitly documented as deferred/no-op remain deferred. Presence in YAML schema alone is not proof that runtime analysis is implemented.

## Bounded reasoning rules

When authoring or reviewing:

- never assume a source-set glob widens analysis beyond declared inputs;
- never treat a changed project discovery glob as a literal resolved project inventory without evidence;
- never infer policy safety from absence of a finding when coverage does not govern the relevant inventory;
- never silently broaden ignored violations or exclusions;
- never treat `policy check` or `policy weakening` as substitutes for architecture validation;
- never treat public API surface checks as binary/package compatibility proof;
- never treat attribute placement checks as authorization/security correctness.

## Where to look next

- [Policy format](../policy-format/index.md)
- [YAML schema reference](../reference/yaml-schema.md)
- [Contract families](../contracts/index.md)
- [CLI reference](../cli/index.md)
- [AI policy authoring guide](policy-authoring-guide.md)
- [Policy weakening review](policy-weakening-review.md)
