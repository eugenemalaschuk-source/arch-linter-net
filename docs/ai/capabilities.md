# Capabilities for AI Policy Authors

AI agents must use the same capability truth as human users. This page intentionally does not maintain a second contract-family matrix.

The machine-readable capability inventory is `archlinternet.capabilities.json`. The human inventory is [Supported capabilities and non-goals](../policy-format/supported-capabilities.md), and the exact family/group mapping is [Contract families](../contracts/index.md). Runtime validators, the CLI tree, and packaged schemas take precedence if a discrepancy is discovered.

## Safe authoring workflow

Before editing a policy:

1. Read the selected root policy and imports.
1. Read `archlinternet.capabilities.json`.
1. Use the packaged/root JSON Schema instead of inventing fields.
1. Run `policy check` for static policy validity.
1. Export `policy context` when the change depends on effective selectors, source sets, exceptions, or semantic evidence.
1. Run normal strict/audit validation after the edit.
1. For base/current review, run `policy weakening`; treat `impact_not_proven` as a review requirement, not proof of safety.

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

For annotation-first adoption, package compatibility, and behavior-preserving
migration, use the public [semantic annotation guide](../guides/semantic-annotations.md)
and [canonical reference](../reference/semantic-annotation-catalog.md). The
manifest-backed capability projection identifies the supported role identities;
do not recreate that catalog manually in policy advice.

### Canonical semantic annotations (v0.10.0 development)

The v0.10.0 annotation-aware tool recognizes the 49 canonical role attributes listed in the manifest-backed `semanticAnnotations.roleAnnotations` capability projection. Matching uses the exact fully qualified name (FQN), with these role names and scopes:

- assembly scope: `DomainLayer`, `ApplicationLayer`, `InfrastructureLayer`, `PresentationLayer`, `ApiLayer`, `PersistenceLayer`, `IntegrationLayer`, `SharedKernel`, `UnityRuntime`, and `UnityEditor`;
- type scope: `Entity`, `AggregateRoot`, `ValueObject`, `DomainService`, `DomainEvent`, `Repository`, `Specification`, `Factory`, `Command`, `CommandHandler`, `Query`, `QueryHandler`, `Event`, `EventHandler`, `IntegrationEvent`, `ReadModel`, `Controller`, `Endpoint`, `RequestDto`, `ResponseDto`, `ApiContract`, `Validator`, `Port`, `Adapter`, `AntiCorruptionLayer`, `DbContext`, `RepositoryImplementation`, `ExternalClient`, `MessageBusAdapter`, `FileSystemAdapter`, `ClockAdapter`, `View`, `ViewModel`, `Presenter`, `MonoBehaviourAdapter`, `ScriptableObjectAsset`, `InputAdapter`, and `SceneAdapter`;
- both type and assembly scope: `CompositionRoot`.

Every canonical FQN is under the reserved `ArchLinterNet.Annotations` namespace. Do not add `classification.attributes` or `classification.assembly_attributes` mappings for that namespace, including redundant mappings of a known role. The assembly must carry exactly one compatible generation 1 catalog marker for package range `[0.10.0,0.11.0)`; missing, malformed, duplicate, unsupported, or unknown reserved identities fail closed with compatibility diagnostics. Canonical annotations provide semantic evidence only: they do not create policy rules, grant reviewed API membership, or turn custom-mapping-only/deferred role names or metadata-only context attributes into role annotations.

The source-only `ArchLinterNet.Annotations` package is prepared for stable 0.10.x releases. It is excluded from v0.10 previews and earlier package sets; a matching 0.10.x tool is required. Released v0.9.1 tools do not recognize these canonical identities.

When advising a code or policy change:

- Prefer a canonical annotation only when its role exactly expresses the type's
  intent; otherwise preserve the existing user-owned mapping.
- Keep one primary semantic role and express context through the six supported
  metadata keys. Do not add multiple roles to make separate contracts match.
- Treat annotations as evidence only. YAML selects and governs that evidence;
  annotations do not grant permissions or reviewed API membership.
- Keep `ApiContract` as a primary role only where it describes the type. The
  authored public-API selector decides membership independently, so a selected
  `ValueObject` remains a `ValueObject`.

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
