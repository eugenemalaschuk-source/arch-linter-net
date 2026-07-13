# Policy Import Format Draft

> Design status: approved format draft for issue #280. ArchLinterNet does not
> implement `imports` yet. Runtime and public schema support belong to #281 and
> provenance support belongs to #282. Publish end-user guidance only after
> those changes ship.

ArchLinterNet policy imports split one policy into focused local YAML fragments
without creating additional roots. A run still receives exactly one policy
path:

```shell
arch-linter-net --policy architecture/arch.yml --mode strict
```

`architecture/arch.yml` is the recommended root name and `*.arch.yml` is the
recommended fragment convention. Both are naming aids only. The explicit
command or Testing adapter argument makes a document the root; following an
`imports` entry makes a document a fragment.

## Syntax

Use an ordered top-level `imports` sequence. Entries are explicit, non-empty
relative paths resolved from the file containing the entry:

```yaml
version: 1
name: Example Architecture Policy

imports:
  - policy/layers.arch.yml
  - policy/contracts/application.arch.yml

analysis:
  target_assemblies:
    - Example.Application
    - Example.Domain

contracts:
  strict_cycles:
    - id: no-layer-cycles
      name: no-layer-cycles
      layers: [application, domain]
```

Fragments use the normal top-level section wrappers. There are no typed import
destinations and no manifest-only root:

```yaml
layers:
  application:
    namespace: Example.Application
  domain:
    namespace: Example.Domain
```

Fragments may import other fragments. Import paths use a portable grammar before
the host filesystem sees them:

- paths are Unicode NFC, use `/` only, and contain one or more non-empty
  segments;
- `.` and `..` are allowed relative segments, then remain subject to physical
  canonicalization and the repository-boundary check;
- normal segments cannot contain control characters or `<`, `>`, `:`, `"`,
  `/`, `\`, `|`, `?`, or `*`, cannot end in dot or space, and cannot be a
  Windows reserved device name such as `CON` or `LPT1`;
- backslashes, leading slashes, empty segments, drive/URI colons, non-NFC
  strings, `${...}`, `$(...)`, `%...%`, and leading `~` are invalid.

This rejects Unix absolute paths, Windows drive paths, UNC/device paths, URLs,
globs, environment expressions, optional imports, and non-scalar entries
before platform-specific path resolution.

## Document roles

The root source schema requires `version` and `name`. It may also contain
`imports` and every existing policy section, but it does not require sections
that a fragment can contribute.

A fragment may contain:

- `imports`
- `layers`
- `external_dependencies`
- `packages`
- `legacy_runtime_layers`
- `analysis`
- `contracts`
- `classification`

A fragment must contain at least one mergeable section or a non-empty
`imports` sequence. It must not contain `version`, `name`, baseline content, or
an unknown top-level field. A fragment is never a separately executable policy
within the importing run.

After every source passes its graph-assigned role/shape schema, the composed
document must pass the full effective-policy schema. That schema requires
`version`, `name`, `layers`, `analysis`, and `contracts`, matching the current
production policy schema. This happens before fallback contract IDs and
semantic/family validators. A root can therefore defer `layers`, `analysis`, or
`contracts` to fragments, but the complete graph cannot omit them.

## Expansion and ordering

The composed source order is deterministic:

1. Take the root's inline content.
1. Visit each root import in declared order.
1. For each fragment, take its inline content, then recursively visit its
   imports in declared order.

For root imports `A`, `B`, where `A` imports `C`, `D`, the order is:

```text
root, A, C, D, B
```

The physical position of the top-level `imports` key within a YAML file does
not change this order. The implementation does not enumerate directories or
sort filenames.

## Composition rules

Root-inline and fragment content use identical rules. Neither side overrides
the other.

| Category | Fields | Composition |
| --- | --- | --- |
| Root identity | `version`, `name` | Root-only; fragments reject them. |
| Keyed definitions | `layers`, `external_dependencies`, `packages`, `analysis.condition_sets` | Union by ordinal case-sensitive key. Any repeated key fails, including identical values. |
| Ordered lists | `legacy_runtime_layers`; analysis assembly, path, and project lists; model-bound classification lists; every strict/audit contract list | Concatenate in composed source order without sorting, merging, or de-duplication. |
| Singletons | Analysis scalar settings and `classification.precedence` | At most one explicit declaration in the complete graph. Defaults apply after composition. |
| Contract nodes | A contract and its nested `ignored_violations` | Remain one atomic list item with one source. |

Fallback contract IDs are assigned after composition. Duplicate IDs compare
case-insensitively and fail within the same contract family and mode, even when
the declarations came from different fragments. Existing compatibility remains:
the same ID may appear in different families or in strict versus audit groups.

`classification.path`, `classification.overrides`, and
`classification.exclusions` are currently raw-YAML-only sections. Composition
retains them with provenance and source order. All `path` entries contribute to
one aggregate `ClassificationPathDeferred` count, so a path declared in a
fragment produces the same visible deferred-support diagnostic as one declared
in the root. `overrides` and `exclusions` remain schema-valid deferred no-ops
until their separate runtime feature exists; they do not become behavior merely
because they were imported.

## Paths and graph limits

Imports are confined to the repository root resolved once from the explicit
root policy. Resolution performs these checks before reading an imported file:

1. Validate the portable import grammar.
1. Resolve the authored relative path from the declaring file.
1. Require exact on-disk path casing.
1. Resolve symbolic links and junctions to a physical canonical path.
1. Require the physical target to remain inside the repository boundary.
1. Reject a target already on the active stack as a cycle.
1. Reject any other repeated physical or case-insensitive portable identity as
   a duplicate import.

The root is depth 0. V1 permits at most 16 import edges in a chain and 256
composed files including the root. The next over-limit file is not read.

## Diagnostics and provenance

Every composed node retains:

- the explicit root identity;
- its canonical repository-relative source path;
- its YAML path.

Conflict diagnostics identify both declarations. Shape and semantic validation
diagnostics identify the fragment and YAML path that introduced the invalid
node. Diagnostic and machine-readable ordering follows composed source order.

Baselines remain separate inputs and are not imported. Future CEL fields are
ordinary scalar values carried by their owning nodes; CEL cannot inspect the
import graph, read files, interpolate imports, or create cross-file anchors.

## Schema and editor support

Runtime support will publish two schemas that share common definitions:

- a root-source schema requiring identity and allowing `imports` plus every
  root section;
- a fragment schema allowing only fragment sections.

The loader then validates the composed result against a full effective-policy
schema requiring the current `version`, `name`, `layers`, `analysis`, and
`contracts` sections.

Runtime chooses by graph role, never by filename. Editors may select a schema
explicitly, including with a YAML language-server directive:

```yaml
# yaml-language-server: $schema=<published-fragment-schema-url>
layers: {}
```

Filename associations may be convenient editor configuration, but they are
not required and cannot affect runtime behavior.

## Compatibility

Policies without `imports` continue to load as one document with the same
defaults, validation pipeline, strict/audit behavior, baselines, graph/explain
behavior, CLI behavior, and Testing adapter behavior. A monolithic policy does
not need to be renamed or migrated; its one source also satisfies the full
effective-policy schema before fallback IDs and semantic validation.

## Non-goals

V1 does not provide multiple roots, remote or package imports, globs, directory
scanning, optional imports, overrides, templating, environment interpolation,
shell execution, scripting, runtime plugins, or cross-file YAML anchors.

## Example files

Fixture-ready examples live in
[`policy-import-examples`](policy-import-examples/README.md). They include
recommended and arbitrary filenames, nested composition, and deterministic
failure cases.
