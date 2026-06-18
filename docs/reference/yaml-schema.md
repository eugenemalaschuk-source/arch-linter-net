# YAML Schema Reference

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
    namespace: <string>      # Required — namespace pattern for matching types
```

Each layer name must be a unique identifier used to reference the layer in contracts.

## `legacy_runtime_layers`

Optional. Additional layers for runtime-only assemblies that are not in source.

```yaml
legacy_runtime_layers:
  <layer-name>:
    namespace: <string>
```

## `analysis`

Assembly resolution configuration.

```yaml
analysis:
  target_assemblies:          # Required — list of assembly names to scan
    - <assembly-name>
  assembly_search_paths: []   # Optional — additional probe directories
```

## `contracts`

Container for all contract definitions. Two groups at the top level:

```yaml
contracts:
  strict: []                  # Blocking contracts
  strict_layers: []
  strict_allow_only: []
  strict_cycles: []
  strict_method_body: []
  strict_asmdef: []
  strict_independence: []

  audit: []                   # Non-blocking contracts (same types)
  audit_layers: []
  audit_allow_only: []
  audit_cycles: []
  audit_method_body: []
  audit_asmdef: []
  audit_independence: []
```

### Dependency contract

```yaml
- name: <string>              # Required — unique contract name
  source: <layer-name>        # Required — source layer
  forbidden: [<layer-name>]   # Required — list of forbidden target layers
  reason: <string>            # Recommended — human-readable justification
```

### Layer order contract

```yaml
- name: <string>
  layers: [<layer-name>]      # Required — ordered outermost to innermost
  reason: <string>
```

### Allow-only contract

```yaml
- name: <string>
  source: <layer-name>
  allowOnly: [<layer-name>]   # Required — whitelist of allowed target layers
  reason: <string>
```

### Cycle contract

```yaml
- name: <string>
  layers: [<layer-name>]      # Required — set of layers to check for cycles
  reason: <string>
```

### Method-body contract

```yaml
- name: <string>
  source: <layer-name>
  forbidden:
    - type: <string>          # Fully qualified type name
    - namespace: <string>     # Namespace pattern
  reason: <string>
```

### asmdef contract

```yaml
- name: <string>
  source: <layer-name>
  forbidden: [<layer-name>]
  reason: <string>
```

### Independence contract

```yaml
- name: <string>
  layers: [<layer-name>]      # Required — layers that must not cross-reference
  reason: <string>
```

### Ignored violations

Any contract type may include an `ignored_violations` block:

```yaml
- name: app-boundaries
  source: app
  forbidden: [infrastructure]
  ignored_violations:
    - source_type: <string>          # Full type name
      forbidden_reference: <string>  # Forbidden reference pattern
      reason: <string>               # Issue tracker reference
```
