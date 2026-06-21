# First Policy

A good first ArchLinterNet policy should be small, mapped to real namespaces, and easy to verify.

## 1. Pick real layers

Start from namespaces that already exist in source or compiled assemblies:

```yaml
layers:
  application:
    namespace: MyApp.Application
  domain:
    namespace: MyApp.Domain
  infrastructure:
    namespace: MyApp.Infrastructure
```

Do not create aspirational layer names unless they map to real code. The linter can only validate static references it can see.

## 2. Configure target assemblies

```yaml
analysis:
  target_assemblies:
    - MyApp.Application
    - MyApp.Domain
    - MyApp.Infrastructure
```

When running from a standalone CLI host, add `assembly_search_paths` if the assemblies are not loadable from the default probing context.

## 3. Add one blocking rule

```yaml
contracts:
  strict:
    - id: application-not-infrastructure
      name: application-must-not-depend-on-infrastructure
      source: application
      forbidden: [infrastructure]
      reason: Application code must depend on abstractions, not concrete infrastructure.
```

Run it:

```bash
arch-linter-net --policy architecture/dependencies.arch.yml --mode strict
```

Exit code `0` means the rule passed. Exit code `1` means at least one violation was found. Exit code `2` means the run failed before validation because of invalid arguments, invalid configuration, or missing files.

## 4. Add an ordered layer rule

Layer order contracts list layers from outermost to innermost:

```yaml
contracts:
  strict_layers:
    - id: clean-architecture-layering
      name: clean-architecture-layering
      layers:
        - infrastructure
        - application
        - domain
      reason: Dependencies must point inward toward the domain.
```

This catches dependencies from an inner layer back outward.

## 5. Use audit for future-state rules

If a rule describes a target architecture that does not pass yet, start with audit:

```yaml
contracts:
  audit:
    - id: audit-ui-bypassing-application
      name: audit-ui-bypassing-application
      source: ui
      forbidden: [domain]
      reason: Discover UI code that bypasses application use cases before making this strict.
```

Then run:

```bash
arch-linter-net --mode audit --json > architecture-audit.json
```

## Complete example

```yaml
version: 1
name: My Architecture Contract

layers:
  application:
    namespace: MyApp.Application
  domain:
    namespace: MyApp.Domain
  infrastructure:
    namespace: MyApp.Infrastructure

analysis:
  target_assemblies:
    - MyApp.Application
    - MyApp.Domain
    - MyApp.Infrastructure

contracts:
  strict:
    - id: application-not-infrastructure
      name: application-must-not-depend-on-infrastructure
      source: application
      forbidden: [infrastructure]
      reason: Application code must depend on abstractions, not concrete infrastructure.

  strict_layers:
    - id: clean-architecture-layering
      name: clean-architecture-layering
      layers:
        - infrastructure
        - application
        - domain
      reason: Dependencies must point inward toward the domain.
```

## Next steps

- Add [CI integration](../guides/ci-integration.md).
- Read the [contract overview](../contracts/index.md).
- Check [supported capabilities and non-goals](../policy-format/supported-capabilities.md) before adding advanced rules.
