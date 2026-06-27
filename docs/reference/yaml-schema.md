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
  policy_consistency: error     # Optional — error | warn | off (default: error)
  coverage: error               # Optional — error | warn | off (default: error)
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
ambiguity as a configuration error rather than guessing. If the selected
build output is older than the project's `.csproj` or any of its `*.cs`
source files, the linter reports it as a stale configuration error instead
of silently validating an outdated assembly — rebuild the project to clear
it. Build-output and staleness checks only run when `analysis.target_assemblies`
is empty; an explicit `target_assemblies` policy is never affected by
discovered projects' build state. Discovered project directories are also
used as `source_roots` when `source_roots` is not set explicitly, independent
of whether their build output resolves. Explicit `target_assemblies`, `assembly_search_paths`, and
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

### `policy_consistency`

Controls behavior when the policy-consistency check finds internal
contradictions within the policy document itself — independent of any code
being scanned. This pass runs after `configuration_check` and before contract
execution, on the fully expanded contract set (including layer-template
expansion), and detects:

- Duplicate contract IDs across strict/audit families and expanded layer templates.
- Allow-only contracts that permit a dependency another contract forbids for
  the same source/target layer pair.
- Independence contracts contradicted by an explicit allowed or ordered
  dependency between the same two layers.
- Protected-surface `allowed_importers` that conflict with a strict
  forbidden/protected rule over the same surface and importer.
- Overlapping internal layer definitions where the same concrete type is
  matched by more than one layer without a parent/child namespace
  containment relationship reconciling the overlap (an external layer
  overlapping an internal one is never flagged).
- Contracts referencing a layer whose namespace pattern can never match any
  type (structurally impossible, not just empty today).

| Value | Behavior |
|-------|----------|
| `error` (default) | Policy-consistency findings fail validation (exit code 1) |
| `warn` | Findings are reported but do not affect the exit code |
| `off` | The check still runs internally but produces no diagnostics and never affects the result |

The check always runs regardless of this setting — `policy_consistency` only
controls whether findings affect `Passed`/the exit code and whether they are
emitted as diagnostics. In human output, findings appear in a separate
`Policy consistency findings:` section. In JSON output, they appear in the
`policy_consistency_findings` array at the top level, each entry carrying
`kind`, `check_kind`, `contract`, `contract_id`, `reason`,
`conflicting_contract_ids`, `conflicting_contract_names`, `layers`, and
(for layer-overlap findings) `representative_type`.

### `coverage`

Controls behavior when declared coverage contracts find uncovered namespaces.

| Value | Behavior |
|-------|----------|
| `error` (default) | Coverage findings fail validation (exit code 1) |
| `warn` | Findings are reported but do not affect the exit code |
| `off` | Coverage findings are suppressed |

Coverage is opt-in through `strict_coverage` / `audit_coverage`. Policies that
declare no coverage contracts behave unchanged regardless of this setting.

See [Coverage contracts](../contracts/coverage.md) for public authoring examples and severity behavior.

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
  strict_coverage: []

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
  audit_coverage: []
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

### Coverage contract

Current runtime support covers `scope: namespace` and `scope: rule_input`.

```yaml
- id: <string>                  # Optional — stable identifier
  name: <string>                # Required — human-readable contract name
  scope: namespace              # Required — namespace | rule_input
  roots:                        # Required for scope: namespace — one or more namespace matchers
    - namespace: <string>       # Required — literal prefix or constrained glob
      namespace_suffix: <string>  # Optional — same semantics as layers.<name>.namespace_suffix
  contract_ids:                 # Required for scope: rule_input — referenced contract IDs
    - <string>
  exclude:                      # Optional — explicit exclusions
    - namespace: <string>       # Optional — namespace matcher (scope: namespace only)
      namespace_suffix: <string>  # Optional — suffix-only matcher or refinement (scope: namespace only)
      contract_id: <string>     # Optional — referenced contract ID (scope: rule_input only)
      reason: <string>          # Required when exclude entry exists
  reason: <string>              # Recommended — why this coverage gate exists
```

Namespace coverage (`scope: namespace`) checks first-party namespaces
discovered in the analysis inventory and reports any namespace under `roots`
that is not covered by:

- a declared layer;
- a declared namespace glob layer;
- an expanded layer-template layer; or
- an explicit exclusion.

Rule-input coverage (`scope: rule_input`) resolves each entry in
`contract_ids` to its referenced contract's layer-bearing fields and reports:

- `unresolved` — the referenced field names a layer that is not declared
  under `layers` at all;
- `empty-input` — the referenced field names a declared layer whose namespace
  pattern currently matches zero namespaces in the analysis inventory.

`contract_ids` may reference dependency, layer, allow_only, cycle,
method_body, independence, protected, or external contracts — the families
whose layer-bearing fields are plain `layers` keys. An unknown ID, an
asmdef/acyclic_sibling/layer_template contract ID (their fields are not plain
layer-name references), or an ID belonging to a coverage contract, is
rejected at load time. `exclude` entries for `scope: rule_input` must use
`contract_id` and suppress both `unresolved` and `empty-input` findings for
that referenced contract.

Example:

```yaml
analysis:
  coverage: error

contracts:
  strict_coverage:
    - id: feature-namespace-coverage
      name: feature-namespace-coverage
      scope: namespace
      roots:
        - namespace: MyApp.Features
      exclude:
        - namespace: MyApp.Features.*
          namespace_suffix: Generated
          reason: Generated code is excluded from manual architecture coverage.
      reason: Every feature namespace must be declared as a layer or explicitly excluded.

    - id: rule-input-coverage
      name: rule-input-coverage
      scope: rule_input
      contract_ids: [cli-must-not-depend-on-testing]
      reason: Flag rules whose source/target layers stop matching any code.
```

Current limits:

- `scope: namespace` and `scope: rule_input` are implemented.
- `scope: project`, `scope: assembly`, and `scope: dependency_edge` are
  reserved and fail validation with an actionable error.
- Coverage findings are emitted as a separate coverage section in human output
  and `coverage_findings` array in JSON output.

For a user-oriented guide, see [Coverage contracts](../contracts/coverage.md).

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
