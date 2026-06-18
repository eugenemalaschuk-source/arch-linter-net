# Policy Format

The architecture policy file lives at `architecture/dependencies.arch.yml` (configurable)
and defines the complete validation configuration.

## Top-level structure

```yaml
version: 1                    # Schema version (required)
name: My Architecture Contract # Human-readable name (required)

layers:                        # Named layers with namespace patterns
analysis:                      # Assembly resolution configuration
contracts:                     # Validation contracts
```

## Layers

Each layer defines a namespace pattern for matching types:

```yaml
layers:
  app:
    namespace: MyApp.Application
  domain:
    namespace: MyApp.Domain
  infrastructure:
    namespace: MyApp.Infrastructure
```

You can also define legacy runtime layer names (for runtime-only assemblies):

```yaml
legacy_runtime_layers:
  - third-party
```

## Analysis configuration

```yaml
analysis:
  target_assemblies:          # Required — assemblies to scan
    - MyApp.Application
    - MyApp.Domain
  assembly_search_paths: []   # Optional — additional probe directories
```

`assembly_search_paths` is recommended for standalone CLI hosts.
You can also provide probe paths through the `ARCHITECTURE_ASSEMBLY_SEARCH_PATHS`
environment variable (path-separator delimited).

## Contracts

Contracts are divided into **strict** and **audit** groups at the top level:

```yaml
contracts:
  strict: []
  strict_layers: []
  strict_allow_only: []
  strict_cycles: []
  strict_method_body: []
  strict_asmdef: []
  strict_independence: []

  audit: []
  audit_layers: []
  audit_allow_only: []
  audit_cycles: []
  audit_method_body: []
  audit_asmdef: []
  audit_independence: []
```

Each contract type has its own structure. See the [Contracts](contracts/index.md) page
for detailed documentation of each family.
