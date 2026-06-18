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

When `external: true`, the linter skips the `empty layer namespace` configuration
check for that layer. Use this for namespaces whose assemblies may not be available
in the scan environment (external SDKs, engine namespaces, platform-conditional
assemblies). All contract checks still apply normally — dependency scanning works
via namespace string matching on source-side types.

## `legacy_runtime_layers`

Optional. List of namespace prefixes that are runtime-only or otherwise not
modeled as named layers. These entries are checked as namespaces when a
dependency contract sets `forbidden_legacy_runtime: true`.

```yaml
legacy_runtime_layers:
  - Legacy.Runtime.Namespace
```

## `analysis`

Assembly resolution configuration.

```yaml
analysis:
  target_assemblies:            # Required — list of assembly names to scan
    - <assembly-name>
  assembly_search_paths: []     # Optional — additional probe directories
  source_roots: []              # Optional — source directory roots for Roslyn resolution
```

## `contracts`

Container for all contract definitions. Two groups at the top level:

```yaml
contracts:
  strict: []                    # Blocking contracts
  strict_layers: []
  strict_allow_only: []
  strict_cycles: []
  strict_method_body: []
  strict_asmdef: []
  strict_independence: []

  audit: []                     # Non-blocking contracts (same types)
  audit_layers: []
  audit_allow_only: []
  audit_cycles: []
  audit_method_body: []
  audit_asmdef: []
  audit_independence: []
```

### Dependency contract

```yaml
- name: <string>                # Required — unique contract name
  source: <layer-name>          # Required — source layer
  forbidden: [<layer-name>]     # Required — list of forbidden target layers
  allowed_types: []             # Optional — exceptions to the forbidden rule
  forbidden_legacy_runtime: false  # Optional — also check legacy runtime layers
  reason: <string>              # Recommended — human-readable justification
```

### Layer order contract

```yaml
- name: <string>
  layers: [<layer-name>]        # Required — ordered outermost to innermost
  reason: <string>
```

### Allow-only contract

```yaml
- name: <string>
  source: <layer-name>
  allowed: [<layer-name>]       # Required — whitelist of allowed target layers
  allowed_types: []             # Optional — type-level exceptions
  reason: <string>
```

### Cycle contract

```yaml
- name: <string>
  layers: [<layer-name>]        # Required — set of layers to check for cycles
  reason: <string>
```

### Method-body contract

```yaml
- name: <string>
  source: <layer-name>
  forbidden_calls:              # Required — list of fully qualified type/namespace names
    - <fully-qualified-type-name>
  reason: <string>
```

### asmdef contract

```yaml
- name: <string>
  source_assemblies: [<string>]   # Required — assemblies to scan
  forbidden_editor_refs: <bool>   # Block references to Unity editor assemblies
  forbidden_asmdef_prefixes:      # Block references matching these prefixes
    - <prefix>
  reason: <string>
```

### Independence contract

```yaml
- name: <string>
  layers: [<layer-name>]        # Required — layers that must not cross-reference
  reason: <string>
```

### Ignored violations

Dependency, layer, allow-only, cycle, method-body, and independence contracts
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
