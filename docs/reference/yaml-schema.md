# YAML Schema Reference

The machine-readable JSON Schema lives at
`schema/dependencies.arch.schema.json`. Use it when authoring policies with
schema-aware editors or AI agents.

> Note: the current runtime YAML loader ignores unmatched properties while
> deserializing. Validate against the JSON Schema before opening policy PRs if
> you need unsupported fields to fail fast.

## `version`

**Required**. Must be `1`.

```yaml
version: 1
```

## `name`

**Required**. Human-readable name for the contract document.

```yaml
name: My Architecture Contract
```

## `layers`

**Required**. Map of layer names to namespace patterns.

```yaml
layers:
  <layer-name>:
    namespace: <string>          # Required — namespace pattern for matching types
    namespace_suffix: <string>   # Optional — suffix appended to namespace during matching
    external: <bool>             # Optional — suppress empty-layer config diagnostic
```

Each layer name must be a unique identifier used to reference the layer in contracts.

`namespace` supports either:

- a literal namespace prefix such as `MyApp.Domain`, or
- a constrained glob pattern using `*` as a complete namespace segment, such as
  `MyApp.Features.*`.

Glob semantics:

- `*` matches exactly one namespace segment.
- Descendants of the resolved prefix still match.
- `namespace_suffix` composes with glob patterns, but the suffix becomes
  position-fixed immediately after the full resolved namespace pattern.
- `**`, `?`, character classes, partial-segment wildcards, bare `*`, and
  leading wildcard patterns such as `*.Features` are invalid.

When `external: true`, the linter skips the `empty layer namespace` configuration
check for that layer. Use this for namespaces whose assemblies may not be available
in the scan environment (external SDKs, engine namespaces, platform-conditional
assemblies). All contract checks still apply normally — dependency scanning works
via namespace string matching on source-side types.

For new vendor/framework leakage controls, prefer `external_dependencies` over
modeling framework namespaces as pseudo-layers.

## `external_dependencies`

Optional. Map of named vendor/framework dependency groups.

```yaml
external_dependencies:
  <group-name>:
    namespace_prefixes: [<string>]   # Optional — exact or child namespace match
    type_prefixes: [<string>]        # Optional — full type-name prefix match
```

External dependency matching uses only referenced type metadata visible from
project types. It does not statically analyze third-party internals and does
not guarantee detection when external assemblies are unresolved enough that the
current scanner cannot observe referenced type names.

## `legacy_runtime_layers`

Optional. List of namespace prefixes that are runtime-only or otherwise not
modeled as named layers. These entries are checked as namespaces when a
dependency contract sets `forbidden_legacy_runtime: true`.

```yaml
legacy_runtime_layers:
  - Legacy.Runtime.Namespace
```

## `analysis`

Assembly resolution and linter behavior configuration.

```yaml
analysis:
  target_assemblies:            # Required, unless solution/projects discovery resolves assemblies — list of assembly names to scan
    - <assembly-name>
  assembly_search_paths: []     # Optional — additional probe directories
  source_roots: []              # Optional — source directory roots for Roslyn resolution
  solution: ''                  # Optional — path to a .sln or .slnx file for project discovery
  projects: []                  # Optional — explicit list of .csproj paths for project discovery
  project_include: []           # Optional — glob patterns to narrow solution-discovered projects
  project_exclude: []           # Optional — glob patterns to remove solution-discovered projects
  configuration: Debug          # Optional — build configuration used to locate project outputs
  target_framework: ''          # Optional — disambiguates multi-targeted project output selection
  unmatched_ignored_violations: error  # Optional — error | warn | off (default: error)
  condition_sets: {}            # Optional — named preprocessor symbol sets
  default_condition_set: ''     # Optional — default condition set name
```

Condition sets control which preprocessor symbols (`#if`) are active during
Roslyn source/method-body scanning. Reflection and IL scanners analyze the
assemblies provided to the run and are not affected by condition sets.

### Solution and project discovery

When `target_assemblies` is empty, the linter can discover assemblies from a
`.sln`/`.slnx` file or an explicit list of `.csproj` files instead of requiring
every assembly name to be hand-listed:

```yaml
analysis:
  solution: MyApp.slnx
  project_exclude:
    - "**/*.Tests/**"
```

For each discovered project, the linter looks for an existing build output at
`bin/{configuration}/{target_framework}/{AssemblyName}.dll` relative to the
project's directory — it never invokes `dotnet build`. If a project targets
multiple frameworks and more than one has a build output on disk, set
`analysis.target_framework` to pick one; otherwise the linter reports the
ambiguity as a configuration error rather than guessing. Discovered project
directories are also used as `source_roots` when `source_roots` is not set
explicitly. Explicit `target_assemblies`, `assembly_search_paths`, and
`source_roots` always take precedence over discovery results.

```yaml
analysis:
  condition_sets:
    runtime: []
    editor: [UNITY_EDITOR]
    debug: [DEBUG, UNITY_EDITOR]
  default_condition_set: runtime
```

### `unmatched_ignored_violations`

Controls behavior when an `ignored_violations` entry no longer matches any current
dependency violation. A stale ignore entry suggests the original debt has been
resolved but the baseline was not cleaned up.

| Value | Behavior |
|-------|----------|
| `error` (default) | Unmatched ignores fail validation (exit code 1) |
| `warn` | Unmatched ignores are reported but do not affect the exit code |
| `off` | Unmatched detection is skipped entirely |

## `contracts`

Container for all contract definitions. Two groups at the top level:

```yaml
contracts:
  strict: []                    # Blocking contracts
  strict_layers: []
  strict_layer_templates: []    # Blocking layer templates
  strict_allow_only: []
  strict_cycles: []
  strict_method_body: []
  strict_asmdef: []
  strict_independence: []
  strict_protected: []
  strict_external: []
  strict_acyclic_siblings: []

  audit: []                     # Non-blocking contracts (same types)
  audit_layers: []
  audit_layer_templates: []     # Audit layer templates
  audit_allow_only: []
  audit_cycles: []
  audit_method_body: []
  audit_asmdef: []
  audit_independence: []
  audit_protected: []
  audit_external: []
  audit_acyclic_siblings: []
```

### Dependency contract

```yaml
- id: <string>                  # Optional — stable identifier for CLI selection
  name: <string>                # Required — unique contract name
  source: <layer-name>          # Required — source layer
  forbidden: [<layer-name>]     # Required — list of forbidden target layers
  allowed_types: []             # Optional — exceptions to the forbidden rule
  forbidden_legacy_runtime: false  # Optional — also check legacy runtime layers
  dependency_depth: direct      # Optional — "direct" (default) or "transitive"
  reason: <string>              # Recommended — human-readable justification
```

When `id` is omitted it is derived automatically from `name` (lowercased with
hyphens replacing spaces). Explicit IDs are recommended for stable CLI
references.

When `dependency_depth` is `"transitive"`, the linter follows the type dependency
graph via BFS and reports violations at any depth. Each violation includes a
`DependencyPaths` collection showing the full dependency chain from source to
forbidden type. The default is `"direct"`, which checks only immediate type
references (1 level).

### Layer order contract

```yaml
- id: <string>                  # Optional
  name: <string>
  layers: [<layer-name>]        # Required — ordered outermost to innermost
  reason: <string>
```

### Layer template contract

Reusable layer order contracts that apply the same layering to multiple namespace
containers. Each template expands into one concrete layer order contract per
container. Relative layer names are resolved by prepending the container namespace.

```yaml
- id: <string>                  # Optional
  name: <string>                # Required — template name
  containers: [<string>]        # Required — namespace prefixes for each container
  layers:                       # Required — ordered template layer definitions
    - name: <string>            # Required — relative layer name
      optional: <bool>          # Optional — if absent, no empty-namespace diagnostic
  reason: <string>
```

When a layer has `optional: true`, the expander silently skips it if no types are
found. If present, the layer must still obey dependency direction. Required layers
with zero types produce a configuration violation.

```yaml
strict_layer_templates:
  - name: feature-clean-architecture
    containers:
      - FirstIce.Features.Fishing
      - FirstIce.Features.Inventory
    layers:
      - name: Presentation
      - name: Application
        optional: true
      - name: Domain
    reason: Every feature follows the same internal dependency direction.
```

### Allow-only contract

```yaml
- id: <string>                  # Optional
  name: <string>
  source: <layer-name>
  allowed: [<layer-name>]       # Required — whitelist of allowed target layers
  allowed_types: []             # Optional — type-level exceptions
  reason: <string>
```

### Cycle contract

```yaml
- id: <string>                  # Optional
  name: <string>
  layers: [<layer-name>]        # Required — set of layers to check for cycles
  reason: <string>
```

### Acyclic sibling contract

```yaml
- id: <string>                  # Optional — stable identifier for CLI selection
  name: <string>                # Required
  ancestors: [<string>]         # Required — one or more namespace prefixes
  ignored_violations: []        # Optional — baseline known violations
  reason: <string>              # Recommended — human-readable justification
```

Scans loaded types under each ancestor namespace and groups them by immediate
child namespace segment. Detects dependency cycles between sibling groups.
Descendant dependencies are attributed to the direct sibling group. Each ancestor
is evaluated independently.

### Method-body contract

```yaml
- id: <string>                  # Optional
  name: <string>
  source: <layer-name>
  forbidden_calls:              # Required — list of fully qualified type/namespace names
    - <fully-qualified-type-name>
  reason: <string>
```

### asmdef contract

```yaml
- id: <string>                  # Optional
  name: <string>
  source_assemblies: [<string>]   # Required — assemblies to scan
  forbidden_editor_refs: <bool>   # Block references to Unity editor assemblies
  forbidden_asmdef_prefixes:      # Block references matching these prefixes
    - <prefix>
  reason: <string>
```

### Independence contract

```yaml
- id: <string>                  # Optional
  name: <string>
  layers: [<layer-name>]        # Required — layers that must not cross-reference
  reason: <string>
```

### Protected surface contract

```yaml
- id: <string>                  # Optional
  name: <string>
  protected: [<layer-name>]     # Required — layers to protect
  allowed_importers: [<layer-name>]  # Required — layers allowed to reference protected layers
  allowed_types: []             # Optional — source-type-level exceptions
  ignored_violations: []        # Optional — baseline known violations
  reason: <string>
```

Protected contracts enforce that only explicitly allowed importer layers may reference
protected layers. Self-references within a protected layer are implicitly allowed and
need not be listed in `allowed_importers`.

### External dependency contract

```yaml
- id: <string>                  # Optional
  name: <string>
  source: <layer-name>          # Required — first-party source layer
  forbidden: [<group-name>]     # Required — external dependency groups
  ignored_violations: []        # Optional — baseline known violations
  reason: <string>
```

External dependency contracts use named groups from `external_dependencies` and
report references from source types into those vendor/framework surfaces.

### Ignored violations

Dependency, layer, allow-only, cycle, acyclic sibling, method-body, independence, protected, and external contracts
may include an `ignored_violations` block (asmdef contracts do not support this):

```yaml
- name: app-boundaries
  source: app
  forbidden: [infrastructure]
  ignored_violations:
    - source_type: <string>            # Full type name
      forbidden_reference: <string>    # Forbidden reference pattern
      reason: <string>                 # Issue tracker reference
```

Ignored violations support exact values and narrow glob-like patterns. Prefer
fully qualified type names and avoid broad entries such as `source_type: "*"`
unless they are explicitly accepted as temporary migration debt.

### Unmatched ignored violation detection

When `analysis.unmatched_ignored_violations` is `error` or `warn`, the linter
detects `ignored_violations` entries whose patterns match no current dependency
violation. Each stale entry is reported as a separate diagnostic with its
contract name, ignore index, source type pattern, forbidden reference pattern,
and stored reason.

In human output, unmatched ignores appear in a separate `Unmatched ignored violations:`
section. In JSON output, they appear in the `unmatched_ignored_violations` array
at the top level alongside `violations` and `cycles`:

```json
{
  "passed": false,
  "mode": "strict",
  "violations": [],
  "cycles": [],
  "unmatched_ignored_violations": [
    {
      "contract": "domain-no-infra",
      "contract_id": "domain",
      "ignore_index": 0,
      "source_type": "MyApp.Legacy.Service",
      "forbidden_reference": "MyApp.Infrastructure.*",
      "reason": "Tracked in #1234"
    }
  ]
}
```
