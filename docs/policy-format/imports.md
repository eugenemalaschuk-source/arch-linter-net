# Policy Imports

Split a large architecture policy into focused local YAML fragments while
keeping one policy entry point. Every CLI, test-adapter, graph, or explain run
still selects exactly one root policy:

```bash
arch-linter-net --policy architecture/arch.yml --mode strict
```

`architecture/arch.yml` is the recommended concise root convention, and names
such as `layers.arch.yml`, `coverage.arch.yml`, or `sales-domain.arch.yml` are
recommended fragment conventions. They are not required filenames or runtime
patterns. `architecture/dependencies.arch.yml`, `company-policy.yaml`, and even
an explicitly imported `domain.data` behave by the same rules.

The relationship determines each role:

- the path selected by `--policy`, `FromPolicy(...)`, or another API is the one
  root;
- every document reached through `imports` is a fragment;
- a fragment is not another independently executed policy in that run.

## Root syntax

Add an ordered top-level `imports` list to a normal policy. The root can keep
small or shared definitions inline:

```yaml
version: 1
name: Shop Architecture

imports:
  - policy/shared/layers.arch.yml
  - policy/bounded-contexts/sales.arch.yml

layers:
  composition_root:
    namespace: Shop.Host.Composition

analysis:
  target_assemblies:
    - Shop.Host
    - Shop.Sales
    - Shop.SharedKernel

contracts:
  strict_composition:
    - id: service-locator-confined-to-host
      name: service-locator-confined-to-host
      allowed_only_in_layers: [composition_root]
      forbidden_apis: [System.IServiceProvider.GetService]
      reason: Service resolution belongs only in the host composition root.
```

Import paths are relative to the document that declares them, not always to the
root. A fragment may import nested fragments with the same syntax.

## Fragment shape

Fragments contain partial policy sections with the normal top-level wrappers:

```yaml
layers:
  shared_kernel:
    namespace: Shop.SharedKernel

contracts:
  strict:
    - id: shared-kernel-does-not-use-sales
      name: shared-kernel-does-not-use-sales
      source: shared_kernel
      forbidden: [sales_domain]
      reason: Shared kernel code must not depend on a bounded context.
```

A fragment may contain one or more of:

- `imports`
- `layers`
- `external_dependencies`
- `packages`
- `legacy_runtime_layers`
- `analysis`
- `contracts`
- `classification`

It must contain a mergeable section or a non-empty `imports` list. It cannot
declare root-only `version` or `name`, baseline content, or unknown top-level
fields. Baselines remain separate inputs.

The root source must declare `version` and `name`. The complete composed policy
must provide `version`, `name`, `layers`, `analysis`, and `contracts` before
normal semantic validation starts.

## Deterministic expansion order

Composition always starts with root-inline content. Imports then expand in
declared order using depth-first pre-order. A fragment's inline content comes
before its own nested imports.

If the root imports A and B, while A imports C and D, the source order is:

```text
root, A, C, D, B
```

Moving the `imports` key within a YAML document does not change that order.
ArchLinterNet does not enumerate directories, expand globs, or sort filenames.

## How sections compose

Root-inline content and fragments use the same conflict rules. There is no root
precedence, imported precedence, or last-write-wins behavior.

| Category | Fields | Composition rule |
| --- | --- | --- |
| Root identity | `version`, `name` | Root-only. Fragments reject them. |
| Keyed definitions | `layers`, `external_dependencies`, `packages`, `analysis.condition_sets` | Union by case-sensitive key. Every repeated key fails, even when values are identical. |
| Ordered collections | `legacy_runtime_layers`; analysis assembly, path, and project lists; classification mapping lists; every strict/audit contract list | Append in composed source order without sorting, merging, or de-duplication. |
| Singleton settings | Analysis scalar settings and `classification.precedence` | At most one explicit declaration across the graph. Defaults apply after composition. |
| Contract nodes | Each contract and its `ignored_violations` | Remain one atomic item owned by one source. |

Fallback contract IDs are assigned after composition. IDs are compared
case-insensitively and must be unique within one contract family and mode across
the whole graph. The same ID remains valid in another family or in strict versus
audit mode, matching monolithic-policy behavior.

Conflicts identify the original and conflicting source. Fix the duplicate; do
not rely on file or import order to select a winner.

## Local path rules and limits

Each import is an explicit, non-empty, Unicode NFC relative path using `/`.
Authored casing must match the on-disk path.

ArchLinterNet resolves one import boundary from the selected root before it
reads any fragment:

- if the selected root lives directly under a directory named `architecture`,
  that directory's parent is the boundary;
- otherwise, the selected root's own directory is the boundary.

Relative imports, including `.` and `..`, are allowed only when their
physically resolved target stays inside that resolved boundary. ArchLinterNet
validates path grammar before filesystem resolution, then checks the physical
target, boundary, active cycle stack, and completed canonical identities before
reading it. Symlink or junction escapes are rejected.

Supported graphs are bounded:

- the root is depth 0;
- a chain may contain at most 16 import edges;
- a graph may contain at most 256 files including the root;
- importing the same canonical file twice fails instead of silently
  de-duplicating it.

## Unsupported forms

The first import version intentionally supports local explicit composition, not
a general YAML preprocessor. These examples are invalid:

```yaml
# Remote and package imports are unsupported.
imports:
  - https://example.com/policy.yml
  - nuget:Company.Policy/1.0.0
```

```yaml
# Globs, environment interpolation, home expansion, and platform-native
# backslashes are unsupported.
imports:
  - policy/*.arch.yml
  - ${POLICY_FRAGMENT}
  - ~/shared-policy.yml
  - policy\layers.arch.yml
```

```yaml
# Arbitrary YAML tags are not import syntax.
imports: !include policy/imports.yml
```

YAML anchors and aliases are document-local. An anchor declared in one file is
not visible in another, so cross-file aliases are unsupported. Imports also do
not provide optional files, directory scanning, multiple roots, silent
overrides, scripting, shell execution, or environment substitution.

A filename cannot turn a selected root into a fragment or an imported fragment
into a root. To validate two roots, run ArchLinterNet twice; do not pass several
root policies to one execution.

## Schema-aware editors

Use the root/effective-policy schema for the selected root:

```text
schema/dependencies.arch.schema.json
```

Use the fragment schema for every imported partial document:

```text
schema/dependencies.arch.fragment.schema.json
```

For VS Code with Red Hat YAML, an inline directive works with any filename:

```yaml
# yaml-language-server: $schema=../../../schema/dependencies.arch.fragment.schema.json
layers: {}
```

You can also configure editor file associations for convenient naming patterns,
but those associations are editor settings only. Runtime role selection always
comes from the selected entry path and import graph.

See the [YAML schema reference](../reference/yaml-schema.md#root-and-fragment-schemas)
for root/fragment validation details.

## Migrate a monolithic policy

Migration does not require a rename and should not change effective behavior:

1. Keep the existing policy as the only selected root.
1. Choose one coherent concern, such as shared layers, coverage, or one bounded
   context. Do not split by line count alone.
1. Move that concern with its existing keys, contract IDs, and list order into
   one fragment. Do not copy root-only `version` or `name`.
1. Add the fragment's explicit relative path to root `imports` at the position
   that preserves the original ordered-contract behavior.
1. Validate the selected root in strict and audit modes.
1. Repeat one concern at a time.

Keep small shared settings inline when that is clearer. Contract IDs remain
global within their family/mode, and keyed definitions cannot be repeated
between root and fragments.

To roll back, inline fragment sections at their composed positions and remove
the corresponding imports. A valid monolithic policy needs no migration merely
to keep working.

## Troubleshooting

| Symptom | Cause and action |
| --- | --- |
| Missing import | Resolve the path from the file that declares it, check exact casing, and confirm it names a regular local file. |
| Import cycle | Follow the reported root-based import chain and remove the back-edge. |
| Duplicate import | One canonical file was reached twice, possibly through `.`/`..`, casing, a link, or two parents. Keep one edge. |
| Composition conflict | The root or two fragments repeat a keyed definition, singleton, or family/mode contract ID. Keep one declaration; order does not provide precedence. |
| Outside import boundary | Keep the fragment inside the selected root's allowed boundary: the parent of `architecture/` for roots selected from that directory, otherwise the selected root's own directory. Avoid escaping symlinks or junctions. |
| Invalid fragment shape | Remove `version`, `name`, baseline/unknown fields, or add a mergeable section/non-empty nested import. |
| Editor marks a fragment as a broken root | Associate the fragment schema explicitly instead of relying on the root schema or filename inference. |
| Editor accepts a file but runtime rejects it | Editor association does not assign runtime role; confirm the file is reached by an import and validate the selected root. |

Policy diagnostics retain the selected root plus the fragment path and YAML
location that introduced a problem. JSON and SARIF output preserve the same
typed source information.

## Executable examples

The repository contains acceptance-tested examples:

- [modular monolith](https://github.com/eugenemalaschuk-source/arch-linter-net/tree/main/samples/policies/imports/modular-monolith)
  split by shared layers and bounded contexts, including an implemented
  role/metadata contextual selector;
- [Unity/client](https://github.com/eugenemalaschuk-source/arch-linter-net/tree/main/samples/policies/imports/unity-client)
  split by runtime, editor, and feature concerns;
- committed naming fixtures pair `architecture/arch.yml` and `*.arch.yml` with
  equivalent arbitrary names to prevent filename-driven behavior.

Future CEL expressions compose as scalar values inside their owning nodes when
their fields are supported. Imports do not grant CEL filesystem, import-graph,
templating, or cross-file-anchor access.
