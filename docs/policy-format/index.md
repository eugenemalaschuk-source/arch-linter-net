# Policy Format

The architecture policy file lives at `architecture/dependencies.arch.yml` (configurable)
and defines the complete validation configuration.

## Top-level structure

```yaml
version: 1                    # Schema version (required)
name: My Architecture Contract # Human-readable name (required)

layers:                        # Named layers with namespace patterns
external_dependencies:         # Named vendor/framework dependency groups
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

### External layers

When a layer references namespaces whose assemblies are not available in the
scan environment (external SDKs, engine types, platform-conditional namespaces),
set `external: true` to suppress the `empty layer namespace` configuration
diagnostic:

```yaml
layers:
  unity_engine:
    namespace: UnityEngine
    external: true

  sentry:
    namespace: Sentry
    external: true
```

External layers remain fully usable as `forbidden` targets, in `allowed` lists,
in `strict_layers`, `strict_cycles`, `strict_independence`, etc. — dependency
scanning uses namespace string matching and does not require target-side types
to be loaded. If types are found for an external layer (e.g. SDK present in
search paths), the linter uses them normally.

For new vendor/framework leakage rules, prefer `external_dependencies` over
`external: true` layers. `external: true` remains supported as a backward-
compatible escape hatch for layer-based policies and namespaces that may be
missing from the scan environment.

## External Dependencies

Use `external_dependencies` to model vendor/framework dependency surfaces that
are not first-party layers:

```yaml
external_dependencies:
  unity_runtime:
    namespace_prefixes:
      - UnityEngine
    type_prefixes: []

  unity_editor:
    namespace_prefixes:
      - UnityEditor
    type_prefixes: []

  infrastructure_sdks:
    namespace_prefixes:
      - Amazon
      - Azure
      - Microsoft.EntityFrameworkCore
    type_prefixes:
      - Stripe.StripeClient
```

`namespace_prefixes` use exact-or-child namespace matching. `type_prefixes`
match full referenced type names by prefix. External dependency matching uses
only referenced type metadata visible from project types. It does not perform
full method-body analysis and does not statically analyze third-party package
internals.

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
  condition_sets: {}          # Optional — named preprocessor symbol sets
  default_condition_set: ''   # Optional — default condition set name
```

`assembly_search_paths` is recommended for standalone CLI hosts.
You can also provide probe paths through the `ARCHITECTURE_ASSEMBLY_SEARCH_PATHS`
environment variable (path-separator delimited).

### Condition sets

Condition sets control which preprocessor symbols (`#if`, `#if !`) are active
during Roslyn source/method-body analysis. Each named set is a list of preprocessor
symbols. When no condition set is selected (or the default resolves to empty), no
symbols are defined and all `#if SYMBOL` blocks are excluded from analysis.

```yaml
analysis:
  condition_sets:
    runtime: []
    editor: [UNITY_EDITOR]
    debug: [DEBUG, UNITY_EDITOR]
  default_condition_set: runtime
```

Condition sets affect Roslyn source/method-body scanning only. Reflection and IL
scanners analyze the assemblies provided to the run and are not reinterpreted under
different symbols.

Select a condition set with `--condition-set <name>` on the CLI, or let the default
apply. Unknown condition set names produce exit code 2.

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
  strict_external: []
  strict_acyclic_siblings: []

  audit: []
  audit_layers: []
  audit_allow_only: []
  audit_cycles: []
  audit_method_body: []
  audit_asmdef: []
  audit_independence: []
  audit_external: []
  audit_acyclic_siblings: []
```

Each contract type has its own structure. See the [Contracts](../contracts/index.md) page
for detailed documentation of each family.
