# YAML Schema Reference

The machine-readable root/effective-policy schema is `schema/dependencies.arch.schema.json`. Imported fragments use `schema/dependencies.arch.fragment.schema.json`.

This page explains the stable shape that users need most often; the JSON Schema and `arch-linter-net schema print` remain the exact field/type authority.

## Discover the installed schemas

```bash
arch-linter-net schema list
arch-linter-net schema print policy-root
```

Persisted format schemas are versioned independently from package SemVer. Do not synthesize schema URLs from the tool package version.

Immutable release-qualified schema IDs remain valid when the version is itself the machine contract. Policy root/fragment v1 currently use `https://archlinternet.dev/schema/0.6.1/dependencies.arch.schema.json` and `https://archlinternet.dev/schema/0.6.1/dependencies.arch.fragment.schema.json`; other frozen registry entries retain their immutable `0.5.1` identities. These numbers are schema-contract identities, not the identity of this evergreen page.

## Root and fragment schemas

| Document | Repository schema | Purpose |
| --- | --- | --- |
| Selected root/effective policy | `schema/dependencies.arch.schema.json` | The one path passed to validation or `policy check`. |
| Imported fragment | `schema/dependencies.arch.fragment.schema.json` | Mergeable content reached through the root's ordered `imports`. |

The root schema requires `version`, `name`, `layers`, `analysis`, and `contracts`. A root may keep an empty container when imported fragments contribute its entries.

## Minimal root

```yaml
version: 1
name: My Architecture Contract

layers:
  domain:
    namespace: MyApp.Domain

analysis:
  solution: MyApp.sln

contracts: {}
```

`version` is currently `1`. `name` is human-readable policy identity.

## Layers

A layer can be matched by namespace, by semantic selector, or by both.

<!-- layer-selector-only-supported -->

```yaml
layers:
  domain:
    namespace: MyApp.Domain

  commands:
    selector:
      role: command
      metadata:
        bounded_context: Sales

  sales_commands:
    namespace: MyApp.Sales
    selector:
      role: command
```

`namespace` is **not** required when a valid `selector` is present. Selector-only layers are runtime-supported.

When both `namespace` and `selector` are present, matching uses AND semantics: a type must satisfy both.

Relevant layer fields include:

```yaml
layers:
  <name>:
    namespace: MyApp.Features.*       # optional when selector is present
    namespace_suffix: Application     # optional; requires namespace
    selector:                         # optional when namespace is present
      role: application_service
      metadata:
        bounded_context: Sales
      when: "subject.type.kind == 'class'"
    external: false
    exclude:
      - namespace: MyApp.Features.Generated
        reason: Generated code is outside this boundary.
    overlaps_with: [another_layer]
```

Namespace matching supports literal prefixes and constrained full-segment `*` globs. It does not provide arbitrary regular expressions. See [Layers and namespace patterns](../policy-format/layers-and-namespaces.md) and [Semantic classification](../policy-format/semantic-classification.md).

## Imports

`imports` is an ordered list of repository-local fragment paths. Runtime composition resolves imports deterministically and validates the effective policy.

```yaml
imports:
  - policy/domain.arch.yml
  - policy/infrastructure.arch.yml
```

Fragments use the fragment schema and contain only mergeable policy sections. See [Policy imports](../policy-format/imports.md).

## External, package, and framework groups

Policies can name reusable dependency groups:

```yaml
external_dependencies:
  logging:
    namespace_prefixes: [Serilog]

packages:
  persistence:
    package_ids: [Npgsql]
    package_prefixes: [Microsoft.EntityFrameworkCore]

framework_references:
  aspnet:
    framework_names: [Microsoft.AspNetCore.App]
```

Use the corresponding contract families rather than modeling project/package facts as fake namespaces.

## Source sets

`source_sets` provide bounded reusable layer, assembly, or project inputs:

```yaml
source_sets:
  modules:
    kind: assembly
    globs: [MyApp.Modules.*]
```

Supported families can expand `sources`/`source_sets` into one concrete contract instance per resolved source; selected list-shaped fields union resolved set members instead. Resolution never widens beyond the policy's declared analysis universe and fails closed on unknown, mismatched, or unreviewed empty inputs.

## Classification

Semantic classification can derive role/metadata from implemented attribute, assembly-attribute, inheritance, and namespace rules. Selector-backed layers and contextual contracts consume the resulting per-run index.

```yaml
classification:
  namespace:
    - namespace: MyApp.Features.*
      role: feature
```

The full implemented/deferred semantics are documented in [Semantic classification](../policy-format/semantic-classification.md). Schema presence alone is not proof that an analysis source is implemented.

## Analysis

Common analysis fields include:

```yaml
analysis:
  solution: MyApp.sln
  projects: []
  project_include: []
  project_exclude:
    - "**/*.Tests/**"

  target_assemblies: []
  assembly_search_paths: []
  source_roots: []

  configuration: Debug
  target_framework: ""

  condition_sets: {}
  default_condition_set: ""

  unmatched_ignored_violations: error
  policy_consistency: error
  coverage: error
  waiver_lifecycle_profile: strict # strict or compatibility; v2 defaults to strict
```

Policies may use explicit target assemblies or project/solution discovery. Normal validation does not silently build. `--ensure-built` is an explicit CLI workflow that builds the selected project graph, verifies the build receipt, then validates.

### Architecture-waiver lifecycle

`ignored_violations` retains its legacy matcher shape for version-1 policies. A
version-2 policy defaults to the `strict` waiver-lifecycle profile: every
manually authored waiver must name an immutable target fingerprint, owner,
tracking issue, introduced date, and expiry date. Use `compatibility`
explicitly only while migrating existing legacy entries.

```yaml
ignored_violations:
  - id: ARCH-IGN-042
    source_type: MyApp.Application.Legacy.LegacyUseCase
    forbidden_reference: MyApp.Infrastructure.LegacyGateway
    target:
      fingerprint: sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef
    reason: Temporary migration seam while the gateway is extracted.
    owner: architecture-team
    issue: ARCH-231
    introduced: 2026-08-01
    expires: 2026-10-01
```

Use `ArchitectureWaiverTargetFingerprint.Create` from the Core API to create a
target value from the exact violation identity. The linter does not treat the
display matchers as the target: the fingerprint prevents the waiver from
silently covering another occurrence that happens to have the same text. A
fingerprint is `sha256:` followed by 64 lowercase hexadecimal characters;
uppercase hexadecimal is rejected as non-canonical. An incomplete manual
waiver is reported as fail-closed `invalid` lifecycle evidence and does not
suppress a finding.

## Contracts

`contracts` contains strict and audit groups. Every currently supported family, its group names, and its dedicated reference are listed in [Contract families](../contracts/index.md).

Examples:

```yaml
contracts:
  strict:
    - id: app-no-infra
      name: app-no-infra
      source: application
      forbidden: [infrastructure]
      reason: Keep the application boundary independent of infrastructure.

  strict_coverage:
    - id: namespace-coverage
      name: namespace-coverage
      scope: namespace
      roots:
        - namespace: MyApp
      reason: Every first-party namespace must be governed.
```

Do not invent group names or fields; use the machine inventory and schema.

## Validate authoring before architecture analysis

```bash
arch-linter-net policy check --policy architecture/arch.yml
```

`policy check` validates policy syntax, imports, composition, static declarations, references, and static configuration without claiming architecture compliance for checks that require project/assembly/source facts.

For the effective policy facts seen by an agent or reviewer:

```bash
arch-linter-net policy context --policy architecture/arch.yml --format json
```

Then run normal strict/audit validation.

## Unknown and deferred fields

Use schema validation even though some deserialization paths are intentionally tolerant for compatibility. Raw validators additionally fail closed for selected high-risk vocabularies.

A field explicitly documented as deferred/no-op remains unsupported behavior even if the schema accepts it. The [capability boundary](../policy-format/supported-capabilities.md) distinguishes implemented behavior from non-goals.
